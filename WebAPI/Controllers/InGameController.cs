using Domain.APIClass;
using Domain.HubInterface;
using Domain.Interfaces;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Infrastructure.DistributedCacheService;
using Domain.DBEntities;
using MongoDB.Driver.Core.Connections;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Globalization;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InGameController: Controller
    {
        private readonly IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> _hub;
        private readonly ICacheService redisCacheService;
        private readonly Explanation explanation;
        public InGameController(IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> hub,
            ICacheService redisCacheService)
        {
            _hub = hub;
            this.redisCacheService = redisCacheService;
            explanation = new Explanation();
        }

        [HttpPost("CreateAGame")]
        public async Task<ActionResult> createAGame(GameOnData data)
        {
            if (double.TryParse(data.christans, out double christans) && double.TryParse(data.judaisms, out double judaisms))
            {
                if (await redisCacheService.createAGameAndAssignIdentities(data.groupName, (int)christans, (int)judaisms))
                {
                    List<OnlineUsers> users = await redisCacheService.getAllUsers(data.groupName);
                    foreach (OnlineUsers user in users)
                    {
                        List<string>? ex = explanation.getExplanation(user.identity);
                        if (ex == null)
                        {
                            return BadRequest();
                        }
                        await _hub.Clients.Client(user.connectionId).updatePlayersIdentities(user.identity.ToString());
                        await _hub.Clients.Client(user.connectionId).IdentitiesExplanation(ex);
                    }
                    return Ok();
                }
                else
                {
                    return BadRequest();
                }
            }
            return BadRequest();
        }
        [HttpPost("IdentityViewingState/{groupName}/{playerName}")]
        public async Task<ActionResult> IdentityViewingState(string groupName, string playerName)
        {
            bool wait = await redisCacheService.waitOnOtherPlayersActionInGroup(groupName);
            // true, then finish view identity and wait for others.
            // False, then all user finished viewing ready to go to next step.
            if (wait)
            {
                OnlineUsers? user = await redisCacheService.getOneUserFromGroup(groupName, playerName);
                if(user != null)
                {
                    await _hub.Clients.Client(user.connectionId).finishedViewIdentityAndWaitOnOtherPlayers(true);
                }
                else
                {
                    return BadRequest();
                }
            }
            else
            {
                await _hub.Clients.Group(groupName).finishedViewIdentityAndWaitOnOtherPlayers(false);
                await _hub.Clients.Group(groupName).nextStep(new NextStep("discussing"));
                await whoIsDiscussing(groupName);
            }
            return Ok();
        }
        [HttpGet("WhoIsDiscussing/{groupName}/{name}")]
        public async Task<ActionResult> whoIsDiscussing(string groupName, string name = "")
        {
            OnlineUsers? currentUser = await redisCacheService.getOneUserFromGroup(groupName, name);
            // put the one who finished disscuss to wait
            if(currentUser != null)
            {
                string? connectionId = await redisCacheService.whoIsDiscussingNext(groupName, name);
                // if there is still user who did not discuss
                if (connectionId != null)
                {
                    // current user goes to waiting state
                    await _hub.Clients.Client(currentUser.connectionId).currentUserInDiscusstion("FinishDisscussionWaitOthers");
                    // next user goes to discuss state
                    await _hub.Clients.Client(connectionId).currentUserInDiscusstion("InDisscussion");
                    return Ok();
                }
                // all user finish discussing
                else
                {
                    await _hub.Clients.Group(groupName).currentUserInDiscusstion("AllUserFinishDisscussion");
                    await _hub.Clients.Group(groupName).nextStep(new NextStep("vote"));
                    return Ok();
                }
            }
            // name = "", then put all people on wait just first user talking
            else
            {
                OnlineUsers? user = await redisCacheService.getFirstUser(groupName);
                if(user != null)
                {
                    await _hub.Clients.GroupExcept(groupName, user.connectionId).currentUserInDiscusstion("FinishDisscussionWaitOthers");
                    await _hub.Clients.Client(user.connectionId).currentUserInDiscusstion("InDisscussion");
                    return Ok();
                }
                else
                {
                    return BadRequest();
                }
            }
        }

        // ---------------- //
        // Daylight Actions //
        // ---------------- //
        [HttpPost("voteHimOrHer/{groupName}/{votePerson}/{fromWho}")]
        public async Task<ActionResult<string>> voteHimOrHer(string groupName, string votePerson, string fromWho)
        {
            (bool, int) result = await redisCacheService.votePerson(groupName, votePerson, fromWho);
            GamesGroupsUsersMessages? group = await redisCacheService.GetGroupAsync(groupName);
            // if everyone finish voting, then announce the result to every users in group.
            if (result.Item1)
            {
                if(result.Item2 == 1)
                {
                    await _hub.Clients.Groups(groupName).finishVoteWaitForOthersOrVoteResult(false, "A Christian has lost all his/her vote weight!");
                }
                else if(result.Item2 == 2)
                {
                    await _hub.Clients.Groups(groupName).finishVoteWaitForOthersOrVoteResult(false, "A Judasim has lost all his/her vote weight!");
                }
                else
                {
                    await _hub.Clients.Groups(groupName).finishVoteWaitForOthersOrVoteResult(false, "There is a tie! No one have lost voted weight!");
                }
                if(group != null)
                {
                    await _hub.Clients.Group(groupName).nextStep(new NextStep("SetUserToNightWaiting"));
                    // if it is the first day night, then assign the prist and ROTS
                    if (group.day == 1)
                    {
                        await assignPriestAndRulerOfTheSynagogue(groupName);
                    }
                    // if not, then it is the ROTS's round. 
                    else
                    {
                        /*await _hub.Clients.Group(groupName).nextStep(new NextStep("RulerofTheSynagogueInformation", new List<string>()));*/
                    }
                }
                else
                {
                    return BadRequest("(voteHimOrHer) cannot get group.");
                }
                return Ok("");
            }
            // if there are users still did not finish vote, then put current user on hold.
            else
            {
                OnlineUsers? user = await redisCacheService.getOneUserFromGroup(groupName, fromWho);
                if(user != null)
                {
                    await _hub.Clients.Client(user.connectionId).finishVoteWaitForOthersOrVoteResult(true, "");
                }
                else
                {
                    return BadRequest("(voteHimOrHer) cannot get user.");
                }
                return Ok("");
            }
        }
        [HttpGet("assignPriestAndRulerOfTheSynagogue/{groupName}")]
        public async Task<ActionResult<bool>> assignPriestAndRulerOfTheSynagogue(string groupName)
        {
            List<OnlineUsers>? users = await redisCacheService.assignPriestAndRulerOfTheSynagogue(groupName);
            OnlineUsers? Nico = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);
            if(users != null && Nico != null)
            {
                if (users.Count != 0)
                {
                    // announce the Priest and ROTS. 
                    /* await _hub.Clients.Client(userConnectionIds[0].connectionId).PriestRound(true, userConnectionIds[0].name);
                    await _hub.Clients.Client(userConnectionIds[1].connectionId).AssignRulerOfTheSynagogue(true);*/
                    await _hub.Clients.Clients(
                        new List<string>() { users[0].connectionId, users[1].connectionId, Nico.connectionId })
                        .PriestROTSNicoMeet(users[1].name, users[0].name, Nico.name);
                    await _hub.Clients.Client(users[0].connectionId).PriestRound();
                    await _hub.Clients.Client(users[1].connectionId).RulerOfTheSynagogueMeeting();
                    await _hub.Clients.Client(Nico.connectionId).NicoMeeting();

                }
                return Ok(true);
            }
            else
            {
                return BadRequest(false);
            }
        }

        // ------------------------ //
        // Priest actions and round //
        // ------------------------ //
        [HttpPut("aboutToExileHimOrHer/{groupName}/{exileName}")]
        public async Task<ActionResult> exileHimOrHer(string groupName, string exileName)
        {
            bool result = await redisCacheService.setExile(groupName, true, exileName);
            /*OnlineUsers? priest = await redisCacheService.getPriest(groupName);
            if(priest != null)
            {
                await _hub.Clients.Client(priest.connectionId).PriestRound(false, priest.name);
            }*/
            // if priest try to exile someone who is not Nicodemus, then go to Nicodemus saving round.
            if (result)
            {
                await NicodemusSavingRoundBegin(groupName, exileName);
            }
            else
            {
                await JohnFireRoundBegin(groupName);
            }
            return Ok();
        }

        // ---------------------------- //
        // Nicodemus actions and round  //
        // ---------------------------- //
        private async Task<bool> NicodemusSavingRoundBegin(string groupName, string exileName)
        {
            OnlineUsers? Nicodemus = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);

            if (Nicodemus != null)
            {
                if (Nicodemus.nicodemusProtection)
                {
                    await _hub.Clients.Client(Nicodemus.connectionId)
                        .nextStep(new NextStep("NicodemusSavingRound", new List<string>() { exileName }));
                }
                else
                {
                    await JohnFireRoundBegin(groupName);
                }
                return true;
            }
            return false;
        }
        [HttpPut("NicodemusAction/{groupName}/{saveOrNot}")]
        public async Task<ActionResult> NicodemusAction(string groupName, bool saveOrNot)
        {
            bool result = await redisCacheService.setExile(groupName, !saveOrNot);
            if(saveOrNot)
            {
                await redisCacheService.NicodemusSetProtection(groupName, false);
            }
            if(result)
            {
                await JohnFireRoundBegin(groupName);
                return Ok();
            }
            else
            {
                return BadRequest();
            }
        }

        // ---------------------- //
        // John actions and round //
        // ---------------------- //
        // didUserChoose is true and fireName = "" that means John did not choose to fire
        [HttpPut("JohnFireRound/{groupName}/{fireName}/{didUserChoose}")]
        public async Task<ActionResult> JohnFireRoundBegin(string groupName, string fireName = "", bool didUserChoose = false)
        {
            OnlineUsers? John = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.John);
            if(John != null)
            {
                // if John is in game and he did not fire all the user, then proceed to John's turn.
                if(John.inGame && !await redisCacheService.checkJohnFireAllOrNot(groupName))
                {
                    // if this method is invoked by the progarm, then let the user to choose.
                    if(!didUserChoose)
                    {
                        List<string>? list = await redisCacheService.GetJohnCannotFireList(groupName);
                        if(list != null)
                        {
                            list.Add(John.name);
                            await _hub.Clients.Client(John.connectionId).nextStep(new NextStep("JohnFireRound", list));
                        }
                        return Ok();
                    }
                    else
                    {
                        if(!string.IsNullOrEmpty(fireName) && !John.disempowering)
                        {
                            await redisCacheService.changeVote(groupName, name: fireName, option: "half");
                            await redisCacheService.AddToJohnFireList(groupName, fireName);
                        }
                        await _hub.Clients.Client(John.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                    }
                }
                await JudasCheckRound(groupName);
                return Ok();
            }
            return NoContent();
        }

        // ----------------------- //
        // Judas actions and round //
        // ----------------------- //
        [HttpPut("JudasCheckRound/{groupName}/{checkName}")]
        public async Task<ActionResult> JudasCheckRound(string groupName, string checkName = "")
        {
            OnlineUsers? Judas = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Judas);
            if (Judas != null)
            {
                if(Judas.inGame)
                {
                    if (string.IsNullOrEmpty(checkName))
                    {
                        await _hub.Clients.Client(Judas.connectionId).nextStep(new NextStep("JudasCheckRound", new List<string>() { Judas.name }));
                    }
                    else
                    {
                        if(Judas.disempowering)
                        {
                            await _hub.Clients.Client(Judas.connectionId).JudasCheckResult(false);
                        }
                        else
                        {
                            await _hub.Clients.Client(Judas.connectionId).JudasCheckResult(
                                await redisCacheService.JudasCheck(groupName, checkName));
                        }
                    }
                }
                return Ok();
            }
            return BadRequest();
        }

        [HttpPut("NightRoundEnd/{groupName}")]
        public async Task<ActionResult> NightRoundEnd(string groupName)
        {
            await _hub.Clients.Group(groupName).changeDay(await redisCacheService.increaseDay(groupName));
            OnlineUsers? user = await redisCacheService.checkIfAnyoneOutOfGame(groupName);
            await redisCacheService.PeterIncreaseVoteWeightByOneOrNot(groupName);
            await _hub.Clients.Group(groupName).nextStep(new NextStep("quitNightWaiting"));
            if (user != null)
            {
                await _hub.Clients.Group(groupName).updateExiledUsers(
                    await redisCacheService.collectAllExiledUserName(groupName));
                await _hub.Clients.Group(groupName).announceExile(user.name);
            }
            else
            {
                await _hub.Clients.Group(groupName).announceExile("No one");
            }
            return Ok();
        }
    }
}
