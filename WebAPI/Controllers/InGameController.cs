using Domain.APIClass;
using Domain.HubInterface;
using Domain.Interfaces;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Domain.DBEntities;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InGameController : Controller
    {
        private readonly IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> _hub;
        private readonly ICacheService redisCacheService;
        private readonly Explanation explanation;
        private readonly IQuestionsService questionsService;
        public InGameController(IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> hub,
            ICacheService redisCacheService,
            IQuestionsService questionsService)
        {
            _hub = hub;
            this.redisCacheService = redisCacheService;
            explanation = new Explanation();
            this.questionsService = questionsService;
        }

        [HttpPost("CreateAGame/{groupName}")]
        public async Task<ActionResult> createAGame(string groupName)
        {
            if (await redisCacheService.createAGameAndAssignIdentities(groupName))
            {
                List<OnlineUsers> users = await redisCacheService.getAllUsers(groupName);
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
        [HttpPost("IdentityViewingState/{groupName}/{playerName}")]
        public async Task<ActionResult> IdentityViewingState(string groupName, string playerName)
        {
            bool wait = await redisCacheService.waitOnOtherPlayersActionInGroup(groupName);
            // true, then finish view identity and wait for others.
            // False, then all user finished viewing ready to go to next step.
            if (wait)
            {
                OnlineUsers? user = await redisCacheService.getOneUserFromGroup(groupName, playerName);
                if (user != null)
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
        [HttpGet("WhoIsDiscussing/{groupName}")]
        public async Task<ActionResult> whoIsDiscussing(string groupName)
        {
            OnlineUsers? nextDicussingUser = await redisCacheService.whoIsDiscussingNext(groupName);
            // if there is still user who did not discuss
            if (nextDicussingUser != null)
            {
                // next user goes to discuss state
                await _hub.Clients.GroupExcept(groupName, nextDicussingUser.connectionId)
                    .currentUserInDiscusstion("Waiting", nextDicussingUser.name);
                await _hub.Clients.Client(nextDicussingUser.connectionId).currentUserInDiscusstion("InDisscussion");
                return Ok();
            }
            // all user finish discussing
            else
            {
                await _hub.Clients.Group(groupName).currentUserInDiscusstion("AllUserFinishDisscussion");
                // New Rule: we do not vote on day one.
                if(await redisCacheService.getDay(groupName) != 1)
                {
                    await _hub.Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName)).nextStep(new NextStep("vote"));
                }
                else
                {
                    await _hub.Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName))
                        .nextStep(new NextStep("SetUserToNightWaiting"));
                    await FirstNightPriestNicoPhariseeMeetingRound(groupName);
                }
                return Ok();
            }
        }

        // ---------------- //
        // Daylight Actions //
        // ---------------- //
        [HttpPost("voteHimOrHer/{groupName}/{votePerson}/{fromWho}")]
        public async Task<ActionResult<string>> voteHimOrHer(string groupName, string votePerson, string fromWho)
        {
            bool wait = await redisCacheService.waitOnOtherPlayersActionInGroup(groupName);
            int result = await redisCacheService.votePerson(groupName, votePerson, fromWho, wait);
            if (!wait)
            {
                switch (result)
                {
                    case 1:
                        await _hub.Clients.Groups(groupName).finishVoteWaitForOthersOrVoteResult(false, "A Christian has lost all his/her vote weight!");
                        break;
                    case 2:
                        await _hub.Clients.Groups(groupName).finishVoteWaitForOthersOrVoteResult(false, "A Judasim has lost all his/her vote weight!");
                        break;
                    case 3:
                        await _hub.Clients.Groups(groupName).finishVoteWaitForOthersOrVoteResult(false, "There is a tie! No one have lost voted weight!");
                        break;
                    default:
                        return BadRequest();
                }
                int winner = await redisCacheService.whoWins(groupName);
                if (winner == 1)
                {
                    await _hub.Clients.Group(groupName).announceWinner(1);
                    await announceGameHistoryAndCleanUp(groupName);
                } 
                else if (winner == 2)
                {
                    await _hub.Clients.Group(groupName).announceWinner(2);
                    await announceGameHistoryAndCleanUp(groupName);
                }
                else
                {
                    await _hub.Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName))
                        .nextStep(new NextStep("SetUserToNightWaiting"));
                    await ROTSInfoRound(groupName);
                }
                return Ok();
            }
            else
            {
                OnlineUsers? user = await redisCacheService.getOneUserFromGroup(groupName, fromWho);
                if (user != null)
                {
                    await _hub.Clients.Client(user.connectionId).finishVoteWaitForOthersOrVoteResult(true);
                }
                else
                {
                    return BadRequest("(voteHimOrHer) cannot get user.");
                }
                return Ok("");
            }
        }
        public async Task<ActionResult> ROTSInfoRound(string groupName)
        {
            OnlineUsers? ROTS = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Pharisee);
            if (ROTS != null)
            {
                if(ROTS.inGame)
                {
                    if(!ROTS.disempowering)
                    {
                        OnlineUsers? lastExiledPlayer = await redisCacheService.getLastNightExiledPlayer(groupName);
                        if (lastExiledPlayer != null &&
                            (lastExiledPlayer.identity == Identities.John ||
                            lastExiledPlayer.identity == Identities.Peter ||
                            lastExiledPlayer.identity == Identities.Laity))
                        {
                            await _hub.Clients.Client(ROTS.connectionId).announceLastExiledPlayerInfo(true, lastExiledPlayer.name);
                        }
                        else
                        {
                            await _hub.Clients.Client(ROTS.connectionId).announceLastExiledPlayerInfo(false);
                        }
                    }
                    else
                    {
                        await _hub.Clients.Client(ROTS.connectionId).announceLastExiledPlayerInfo(false);
                    }      
                }
                // New Rule: Judas and Priest can meet after day 3.
                if(await redisCacheService.getDay(groupName) >= 3)
                {
                    return await JudasMeetWithPriest(groupName);
                }
                else
                {
                    OnlineUsers? Priest = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
                    if (Priest != null)
                    {
                        await _hub.Clients.Client(Priest.connectionId).PriestRound();
                        return Ok();
                    }
                    else
                    {
                        return BadRequest();
                    } 
                }
            }
            else
            {
                return BadRequest();
            }
        }
        [HttpGet("justForTestMeeting/{groupName}")]
        public async Task FirstNightPriestNicoPhariseeMeetingRound(string groupName)
        {
            OnlineUsers? Preist = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
            OnlineUsers? Nico = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);
            OnlineUsers? Pharisee = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Pharisee);
            if(Preist != null && Nico != null && Pharisee != null) 
            {
                await _hub.Clients.Clients(
                        new List<string>() { Preist.connectionId, Nico.connectionId, Pharisee.connectionId })
                        .PriestROTSNicoMeet(Pharisee.name, Preist.name, Nico.name);
                await _hub.Clients.Client(Preist.connectionId).PriestRound();
                await _hub.Clients.Client(Pharisee.connectionId).RulerOfTheSynagogueMeeting();
                await _hub.Clients.Client(Nico.connectionId).NicoMeeting();
            }
            
        }
        [HttpPost("JudasMeetWithPriest/{groupName}/{JudasHint}")]
        public async Task<ActionResult> JudasMeetWithPriest(string groupName, string JudasHint = "")
        {
            OnlineUsers? Judas = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Judas);
            OnlineUsers? Priest = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);

            if (Priest != null && Judas != null)
            {
                if(Judas.inGame)
                {
                    if (string.IsNullOrEmpty(JudasHint))
                    {
                        await _hub.Clients.Client(Judas.connectionId).JudasGivePriestHint(Priest.name);
                    }
                    else
                    {
                        await _hub.Clients.Client(Judas.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                        await _hub.Clients.Client(Priest.connectionId).PriestReceiveHint(Judas.name, JudasHint);
                        await _hub.Clients.Client(Priest.connectionId).PriestRound();
                    }
                }
                else
                {
                    await _hub.Clients.Client(Priest.connectionId).PriestRound();
                }
                return Ok();
            }
            else
            {
                return BadRequest();
            }
        }
        // ------------------------ //
        // Priest actions and round //
        // ------------------------ //
        [HttpPut("aboutToExileHimOrHer/{groupName}/{exileName}")]
        public async Task<ActionResult> exileHimOrHer(string groupName, string exileName)
        {
            bool result = await redisCacheService.setExile(groupName, true, exileName);
            // Nico cannot be exiled. result will be false.
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
                if (Nicodemus.nicodemusProtection && !Nicodemus.disempowering)
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
                    if(didUserChoose)
                    {
                        if (fireName != "NULL" && !John.disempowering)
                        {
                            await redisCacheService.changeVote(groupName, name: fireName, option: "half");
                            await redisCacheService.AddToJohnFireList(groupName, fireName);
                            await _hub.Clients.Client(John.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                        }
                        else
                        {
                            await redisCacheService.AddNewMessageIntoGroupAndSave(groupName, "John did not use his ability!");
                        }
                    }
                    else
                    {
                        List<string>? list = await redisCacheService.GetJohnCannotFireList(groupName);
                        if (list != null)
                        {
                            list.Add(John.name);
                            await _hub.Clients.Client(John.connectionId).nextStep(new NextStep("JohnFireRound", list));
                        }
                        return Ok();
                    }
                }
                // New Rule: Judas can use his ability after day 2.
                if (await redisCacheService.getDay(groupName) >= 2)
                {
                    await JudasCheckRound(groupName);
                }
                else
                {
                    await NightRoundEnd(groupName);
                }
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
                        await _hub.Clients.Client(Judas.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                        if (Judas.disempowering)
                        {
                            await _hub.Clients.Client(Judas.connectionId).JudasCheckResult(false);
                        }
                        else
                        {
                            await _hub.Clients.Client(Judas.connectionId).JudasCheckResult(
                                await redisCacheService.JudasCheck(groupName, checkName));
                        }
                    }
                } else {
                    // If Judas is out of game, we do not want ending night round fast because player will release that Judas is out.
                    Thread.Sleep(3000);
                    await NightRoundEnd(groupName);
                }
                return Ok();
            }
            return BadRequest();
        }

        [HttpPut("NightRoundEnd/{groupName}")]
        public async Task<ActionResult> NightRoundEnd(string groupName)
        {
            
            OnlineUsers? user = await redisCacheService.checkAndSetIfAnyoneOutOfGame(groupName);
            await redisCacheService.PeterIncreaseVoteWeightByOneOrNot(groupName);
            List<OnlineUsers> notInGameUsers = await redisCacheService.collectAllExiledUserName(groupName);
            List<string> notInGameUsersConnectiondIds = notInGameUsers.Select(x => x.connectionId).ToList();
            await _hub.Clients.Group(groupName).changeDay(await redisCacheService.increaseDay(groupName));
            await _hub.Clients.Group(groupName).nextStep(new NextStep("quitNightWaiting"));
            if (user != null)
            {

                await _hub.Clients.Group(groupName).updateExiledUsers(notInGameUsers);
                await _hub.Clients.Group(groupName).announceExile(user.name);
            }
            else
            {
                await _hub.Clients.Group(groupName).announceExile("No one");
            }
            return Ok();
        }

        [HttpPost("finishedToViewTheExileResult/{groupName}")]
        public async Task<ActionResult> finishedToViewTheExileResult(string groupName)
        {
            bool wait = await redisCacheService.waitOnOtherPlayersActionInGroup(groupName);
            if(!wait)
            {
                switch(await redisCacheService.whoWins(groupName))
                {
                    case 1:
                        // clear the previous vote result, so it will not present in the winning modal.
                        await _hub.Clients.Groups(groupName).finishVoteWaitForOthersOrVoteResult(false, "");
                        await _hub.Clients.Group(groupName).announceWinner(1);
                        await announceGameHistoryAndCleanUp(groupName);
                        break;
                    case 2:
                        await _hub.Clients.Groups(groupName).finishVoteWaitForOthersOrVoteResult(false, "");
                        await _hub.Clients.Group(groupName).announceWinner(2);
                        await announceGameHistoryAndCleanUp(groupName);
                        break;
                    default:
                        await spiritualQuestionAnsweredCorrectOrNot(groupName);
                        break;
                }
            }
            return Ok();
        }
        [HttpPost("spiritualQuestionAnsweredCorrectOrNot/{groupName}/{name}/{playerChoiceCorrectOrNot}")]
        public async Task<ActionResult> spiritualQuestionAnsweredCorrectOrNot(string groupName, string name = "", string playerChoiceCorrectOrNot = "")
        {
            if(!string.IsNullOrEmpty(name))
            {
                if(bool.TryParse(playerChoiceCorrectOrNot, out bool result) && result)
                {
                    await redisCacheService.changeVote(groupName, name, changedVote: 0.5, option: "add");
                }
            }
            OnlineUsers? nextDicussingUser = await redisCacheService.whoIsDiscussingNext(groupName);
            // if there is still user who did not answer questions
            if (nextDicussingUser != null)
            {
                Questions? q = await questionsService.RandomSelectAQuestion();
                if (q != null)
                {
                    await _hub.Clients.Group(groupName).inAnswerQuestionName(nextDicussingUser.name);
                    await _hub.Clients.Client(nextDicussingUser.connectionId).getAQuestion(q);
                }
                return Ok();
            }
            // all user finish answer questions
            else
            {
                await _hub.Clients.Group(groupName).inAnswerQuestionName();
                await _hub.Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName))
                    .nextStep(new NextStep("discussing", new List<string>() { randomPickATopic() }));
                await whoIsDiscussing(groupName);
                return Ok();
            }
        }
        private async Task announceGameHistoryAndCleanUp(string groupName)
        {
            await _hub.Clients.Group(groupName).announceGameHistory(await redisCacheService.getGameHistory(groupName) ?? null);
            await redisCacheService.cleanUp(groupName);
        }
        private async Task<List<string>> getNotInGameUsersConnectiondIds(string groupName)
        {
            List<OnlineUsers> notInGameUsers = await redisCacheService.collectAllExiledUserName(groupName);
            return notInGameUsers.Select(x => x.connectionId).ToList();
        }
        private string randomPickATopic()
        {
            Random rand = new Random();
            switch(rand.Next(0, 5))
            {
                case 0:
                    return "Who is the Priest?";
                case 1:
                    return "Who is the Pharisees?";
                case 2:
                    return "Who is Judas?";
                case 3:
                    return "Who is Peter?";
                case 4:
                    return "Who is John?";
                default:
                    return "error";
            }
        }
    }
}
