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
                await _hub.Clients.Group(groupName).nextStep(new NextStep("discussing", new List<string>()));
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
                    await _hub.Clients.Group(groupName).nextStep(new NextStep("vote", new List<string>()));
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
                    // if it is the first day night, then assign the prist and ROTS
                    if (group.day == 1)
                    {
                        await _hub.Clients.Group(groupName).nextStep(new NextStep("SetUserToNightWaiting", new List<string>()));
                        await assignPriestAndRulerOfTheSynagogue(groupName);
                    }
                    // if not, then it is the ROTS's round. 
                    else
                    {
                        /*await _hub.Clients.Group(groupName).nextStep(new NextStep("RulerofTheSynagogueInformation", new List<string>()));*/
                    }
                    /*(bool, int) whoWins = await redisCacheService.whoWins(groupName);
                    if(whoWins.Item1)
                    {
                        if(whoWins.Item2 == 1)
                        {
                            await _hub.Clients.Group(groupName).nextStep(new NextStep("Wins", new List<string>(){ "ChristianWins" }));
                        }
                        else if(whoWins.Item2 == 2)
                        {
                            await _hub.Clients.Group(groupName).nextStep(new NextStep("Wins", new List<string>(){ "JudaismWins" }));
                        }
                        else
                        {
                            return BadRequest("(voteHimOrHer) whoWins error.");
                        }
                    }
                    else
                    {
                        if (group.day == 1)
                        {
                            await _hub.Clients.Group(groupName).nextStep(new NextStep("AssignPristAndRulerofTheSynagogue", new List<string>()));
                        }
                        else
                        {
                            await _hub.Clients.Group(groupName).nextStep(new NextStep("RulerofTheSynagogueInformation", new List<string>()));
                        }
                    }*/
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
            List<OnlineUsers>? userConnectionIds = await redisCacheService.assignPriestAndRulerOfTheSynagogue(groupName);
            if(userConnectionIds != null)
            {
                if (userConnectionIds.Count != 0)
                {
                    // announce the Priest and ROTS. 
                    await _hub.Clients.Client(userConnectionIds[0].connectionId).PriestRound(true, userConnectionIds[0].name);
                    await _hub.Clients.Client(userConnectionIds[1].connectionId).AssignRulerOfTheSynagogue(true);
                    return Ok(true);
                }
                else
                {
                    return Ok(true);
                }
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
        public async Task<ActionResult<bool>> exileHimOrHer(string groupName, string exileName)
        {
            bool result = await redisCacheService.setExile(groupName, true, exileName);
            OnlineUsers? priest = await redisCacheService.getPriest(groupName);
            if(priest != null)
            {
                await _hub.Clients.Client(priest.connectionId).PriestRound(false, priest.name);
            }
            if(await NicodemusSavingRoundBegin(groupName, exileName) && result)
            {
                return Ok(result);
            }
            return BadRequest(false);
        }

        // ---------------------------- //
        // Nicodemus actions and round  //
        // ---------------------------- //
        private async Task<bool> NicodemusSavingRoundBegin(string groupName, string exileName)
        {
            OnlineUsers? Nicodemus = await redisCacheService.getSpecificIdentityFromGroup(groupName, Identities.Nicodemus);

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
        [HttpPut("JohnFireRound/{groupName}/{fireName}")]
        public async Task<ActionResult> JohnFireRoundBegin(string groupName, string fireName = "")
        {
            OnlineUsers? John = await redisCacheService.getSpecificIdentityFromGroup(groupName, Identities.John);
            if(John != null)
            {
                if(John.inGame)
                {
                    if(string.IsNullOrEmpty(fireName))
                    {
                        await _hub.Clients.Client(John.connectionId).nextStep(new NextStep("JohnFireRound", new List<string>()));
                    }
                    else
                    {
                        await redisCacheService.changeVote(groupName, name: fireName, option: "half");
                    }
                }
                else
                {
                    /*await JudasCheckRound(groupName);*/
                }
                return Ok();
            }
            return NoContent();
        }

        
    }
}
