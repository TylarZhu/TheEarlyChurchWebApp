using Domain.APIClass;
using Domain.HubInterface;
using Domain.Interfaces;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Domain.DBEntities;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

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
            await redisCacheService.beforeGameCleanUp(groupName);
            if (await redisCacheService.createAGameAndAssignIdentities(groupName))
            {
                List<Users> users = await redisCacheService.getAllUsers(groupName);
                foreach (Users user in users)
                {
                    List<string>? ex = explanation.getExplanation(user.identity);
                    if (ex == null)
                    {
                        return BadRequest();
                    }
                    await _hub.Clients.Client(user.connectionId).identityModalOpen(true);
                    await _hub.Clients.Client(user.connectionId).updatePlayersIdentities(user.identity.ToString());
                    await _hub.Clients.Client(user.connectionId).IdentitiesExplanation(ex);
                }
                await redisCacheService.turnOnGameInProgress(groupName);
                await redisCacheService.changeCurrentGameStatus(groupName, "IdentityViewingState");
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
            await redisCacheService.setViewedResultToTrue(groupName, playerName);
            List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
            if (didNotViewedUsers == null)
            {
                return BadRequest();
            }
            else if (didNotViewedUsers.Any())
            {
                await _hub.Clients.Group(groupName).stillWaitingFor(didNotViewedUsers);
                string? currentConnection = await redisCacheService.getConnectionIdByName(groupName, playerName);
                if (currentConnection == null)
                {
                    return BadRequest();
                }
                await _hub.Clients.Group(groupName).updateWaitingProgess(
                    await redisCacheService.getWaitingProgessPercentage(groupName, didNotViewedUsers.Count));
                await _hub.Clients.Client(currentConnection).finishedViewIdentityAndWaitOnOtherPlayers(true);
            }
            else
            {
                await _hub.Clients.Group(groupName).finishedViewIdentityAndWaitOnOtherPlayers(false);
                await _hub.Clients.Group(groupName).updateWaitingProgess(0.0);
                // didNotViewedUsers should be empty
                await redisCacheService.resetAllViewedResultState(groupName);
                await redisCacheService.changeCurrentGameStatus(groupName, "DiscussingState");
                await _hub.Clients.Group(groupName).nextStep(new NextStep("discussing"));
                await whoIsDiscussing(groupName);
            }
            return Ok();
        }
        [HttpGet("WhoIsDiscussing/{groupName}")]
        public async Task<ActionResult> whoIsDiscussing(string groupName)
        {
            Users? nextDicussingUser = await redisCacheService.whoIsDiscussingNext(groupName);
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
                    await redisCacheService.changeCurrentGameStatus(groupName, "VoteState");
                    await _hub.Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName)).nextStep(new NextStep("vote"));
                }
                else
                {
                    await redisCacheService.changeCurrentGameStatus(groupName, "PriestRoundState");
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
            /*bool wait = await redisCacheService.decreaseWaitingUsers(groupName);*/
            await redisCacheService.setViewedResultToTrue(groupName, fromWho);
            List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
            if (didNotViewedUsers == null)
            {
                return BadRequest();
            }
            else if (didNotViewedUsers.Any())
            {
                // set everyoneFinishVoting = true, because not all user finish voting.
                await redisCacheService.votePerson(groupName, votePerson, fromWho, true);
                await _hub.Clients.Group(groupName).stillWaitingFor(didNotViewedUsers);
                // This user is finish voting, set this user to waiting state
                await _hub.Clients.Client(await redisCacheService.getConnectionIdByName(groupName, fromWho) ?? "")
                    .finishVoteWaitForOthersOrVoteResult(true);
                await _hub.Clients.Group(groupName).updateWaitingProgess(
                    await redisCacheService.getWaitingProgessPercentage(groupName, didNotViewedUsers.Count));
                return Ok("");
            }
            else
            {
                int result = await redisCacheService.votePerson(groupName, votePerson, fromWho, false);
                switch (result)
                {
                    case 1:
                        await _hub.Clients.Group(groupName).finishVoteWaitForOthersOrVoteResult(false, "A Christian has lost all his/her vote weight!");
                        await redisCacheService.setVoteResult(groupName, "A Christian has lost all his/her vote weight!");
                        await redisCacheService.setGameMessageHistory(groupName, "A Christian has lost all his/her vote weight!");
                        await _hub.Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                        break;
                    case 2:
                        await _hub.Clients.Group(groupName).finishVoteWaitForOthersOrVoteResult(false, "A Judasim has lost all his/her vote weight!");
                        await redisCacheService.setVoteResult(groupName, "A Judasim has lost all his/her vote weight!");
                        await redisCacheService.setGameMessageHistory(groupName, "A Judasim has lost all his/her vote weight!");
                        await _hub.Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                        break;
                    case 3:
                        await _hub.Clients.Group(groupName).finishVoteWaitForOthersOrVoteResult(false, "There is a tie! No one have lost voted weight!");
                        await redisCacheService.setVoteResult(groupName, "There is a tie! No one have lost voted weight!");
                        await redisCacheService.setGameMessageHistory(groupName, "There is a tie! No one have lost voted weight!");
                        await _hub.Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                        break;
                    default:
                        return BadRequest();
                }
                int winner = await redisCacheService.whoWins(groupName);
                if (winner == 1)
                {
                    /*await redisCacheService.changeCurrentGameStatus(groupName, "WinnerAfterVoteState");
                    await redisCacheService.setWhoWins(groupName, 1);*/
                    await _hub.Clients.Group(groupName).announceWinner(1);
                    await announceGameHistoryAndCleanUp(groupName);
                }
                else if (winner == 2)
                {
                   /* await redisCacheService.changeCurrentGameStatus(groupName, "WinnerAfterVoteState");
                    await redisCacheService.setWhoWins(groupName, 2);*/
                    await _hub.Clients.Group(groupName).announceWinner(2);
                    await announceGameHistoryAndCleanUp(groupName);
                }
                else
                {
                    // changeCurrentGameStatus before reset viewedResult, in case user reconnect and put on waiting again.
                    /*await redisCacheService.changeCurrentGameStatus(groupName, "ROTSInfoRoundState");*/
                    await redisCacheService.resetAllViewedResultState(groupName);
                    await _hub.Clients.Group(groupName).updateWaitingProgess(0.0);
                    await _hub.Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName))
                        .nextStep(new NextStep("SetUserToNightWaiting"));
                    await ROTSInfoRound(groupName);
                }
                return Ok();
            }
        }
        public async Task<ActionResult> ROTSInfoRound(string groupName)
        {
            Users? ROTS = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Pharisee);
            if (ROTS != null)
            {
                if(ROTS.inGame && !ROTS.offLine)
                {
                    if(!ROTS.disempowering)
                    {
                        Users? lastExiledPlayer = await redisCacheService.getLastNightExiledPlayer(groupName);
                        if (lastExiledPlayer != null &&
                            (lastExiledPlayer.identity == Identities.John ||
                            lastExiledPlayer.identity == Identities.Peter ||
                            lastExiledPlayer.identity == Identities.Laity))
                        {
                            await _hub.Clients.Client(ROTS.connectionId).announceLastExiledPlayerInfo(true, lastExiledPlayer.name);
                        }
                    }
                    await _hub.Clients.Client(ROTS.connectionId).announceLastExiledPlayerInfo(false);
                }
                // New Rule: Judas and Priest can meet after day 3.
                if(await redisCacheService.getDay(groupName) >= 3)
                {
                    await redisCacheService.changeCurrentGameStatus(groupName, "JudasMeetWithPriestState");
                    return await JudasMeetWithPriest(groupName);
                }
                else
                {
                    Users? Priest = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
                    if (Priest != null)
                    {
                        await redisCacheService.changeCurrentGameStatus(groupName, "PriestRoundState");
                        await PriestRound(groupName);
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
        private async Task FirstNightPriestNicoPhariseeMeetingRound(string groupName)
        {
            Users? Preist = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
            Users? Nico = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);
            Users? Pharisee = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Pharisee);
            if(Preist != null && Nico != null && Pharisee != null) 
            {
                await _hub.Clients.Clients(
                        new List<string>() { Preist.connectionId, Nico.connectionId, Pharisee.connectionId })
                        .PriestROTSNicoMeet(Pharisee.name, Preist.name, Nico.name);
                await PriestRound(groupName);
                await _hub.Clients.Client(Pharisee.connectionId).RulerOfTheSynagogueMeeting();
                await _hub.Clients.Client(Nico.connectionId).NicoMeeting();
            }
            
        }
        [HttpPost("JudasMeetWithPriest/{groupName}/{JudasHint}")]
        public async Task<ActionResult> JudasMeetWithPriest(string groupName, string JudasHint = "")
        {
            Users? Judas = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Judas);
            Users? Priest = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);

            if (Priest != null && Judas != null)
            {
                if(Judas.inGame && !Judas.offLine)
                {
                    if (string.IsNullOrEmpty(JudasHint))
                    {
                        await _hub.Clients.Client(Judas.connectionId).JudasGivePriestHint(Priest.name);
                        return Ok();
                    }
                    else
                    {
                        await _hub.Clients.Client(Judas.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                        await _hub.Clients.Client(Priest.connectionId).PriestReceiveHint(Judas.name, JudasHint);
                    }
                }
                await redisCacheService.changeCurrentGameStatus(groupName, "PriestRoundState");
                await PriestRound(groupName);
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
                await redisCacheService.changeCurrentGameStatus(groupName, "NicodemusSavingRoundBeginState");
                await NicodemusSavingRoundBegin(groupName, exileName);
            }
            else
            {
                await redisCacheService.changeCurrentGameStatus(groupName, "JohnFireRoundBeginState");
                return await JohnFireRoundBegin(groupName);
            }
            return Ok();
        }

        // ---------------------------- //
        // Nicodemus actions and round  //
        // ---------------------------- //
        private async Task<bool> NicodemusSavingRoundBegin(string groupName, string exileName)
        {
            Users? Nicodemus = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);

            if (Nicodemus != null)
            {
                if (!Nicodemus.offLine && Nicodemus.nicodemusProtection && !Nicodemus.disempowering)
                {
                    await _hub.Clients.Client(Nicodemus.connectionId)
                        .nextStep(new NextStep("NicodemusSavingRound", new List<string>() { exileName }));
                }
                else
                {
                    await redisCacheService.changeCurrentGameStatus(groupName, "JohnFireRoundBeginState");
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
                await redisCacheService.changeCurrentGameStatus(groupName, "JohnFireRoundBeginState");
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
            Users? John = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.John);
            if(John != null)
            {
                // if John is not offline and he is in game and did not disempowered, then proceed to John's turn.
                if(!John.offLine && John.inGame && !John.disempowering && !await redisCacheService.checkJohnFireAllOrNot(groupName))
                {
                    // if this method is invoked by the progarm, then let the user to choose.
                    if(didUserChoose)
                    {
                        if (fireName != "NULL")
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
                    await redisCacheService.changeCurrentGameStatus(groupName, "JudasCheckRoundState");
                    await JudasCheckRound(groupName);
                }
                else
                {
                    /*await redisCacheService.changeCurrentGameStatus(groupName, "NightRoundEndState");*/
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
            Users? Judas = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Judas);
            if (Judas != null)
            {
                if(!Judas.offLine && Judas.inGame)
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
                    /*Thread.Sleep(3000);*/
                    await redisCacheService.changeCurrentGameStatus(groupName, "NightRoundEndState");
                    await NightRoundEnd(groupName);
                }
                return Ok();
            }
            return BadRequest();
        }

        [HttpPut("NightRoundEnd/{groupName}")]
        public async Task<ActionResult> NightRoundEnd(string groupName)
        {
            Users? user = await redisCacheService.checkAndSetIfAnyoneOutOfGame(groupName);
            List<Users> notInGameUsers = await redisCacheService.collectAllExileUserName(groupName);
            await _hub.Clients.Group(groupName).nextStep(new NextStep("quitNightWaiting"));
            if (user != null)
            {
                await redisCacheService.setGameMessageHistory(groupName, $"{user.name} has been exiled!");
                await _hub.Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                await _hub.Clients.Group(groupName).updateExiledUsers(notInGameUsers);
                await _hub.Clients.Group(groupName).announceExile(user.name);
            }
            else
            {
                await redisCacheService.setGameMessageHistory(groupName, $"No one has been exiled!");
                await _hub.Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                await _hub.Clients.Group(groupName).announceExile("No one");
            }
            // increase day at last, because history message need to record first.
            await _hub.Clients.Group(groupName).changeDay(await redisCacheService.increaseAndGetDay(groupName));
            await redisCacheService.PeterIncreaseVoteWeightByOneOrNot(groupName);
            await redisCacheService.changeCurrentGameStatus(groupName, "finishedToViewTheExileResultState");
            return Ok();
        }

        [HttpPost("finishedToViewTheExileResult/{groupName}/{playerName}")]
        public async Task<ActionResult> finishedToViewTheExileResult(string groupName, string playerName)
        {
            await redisCacheService.setViewedResultToTrue(groupName, playerName);
            List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
            if (didNotViewedUsers == null)
            {
                return BadRequest();
            }
            else if(didNotViewedUsers.Any())
            {
                await _hub.Clients.Group(groupName).stillWaitingFor(didNotViewedUsers);
                await _hub.Clients.Client(await redisCacheService.getConnectionIdByName(groupName, playerName) ?? "").openOrCloseExileResultModal(true);
                await _hub.Clients.Group(groupName).updateWaitingProgess(
                    await redisCacheService.getWaitingProgessPercentage(groupName, didNotViewedUsers.Count));
            }
            else
            {
                await _hub.Clients.Group(groupName).openOrCloseExileResultModal(false);
                switch (await redisCacheService.whoWins(groupName))
                {
                    case 1:
                        await _hub.Clients.Group(groupName).announceWinner(1);
                        await announceGameHistoryAndCleanUp(groupName);
                        break;
                    case 2:
                        await _hub.Clients.Group(groupName).announceWinner(2);
                        await announceGameHistoryAndCleanUp(groupName);
                        break;
                    default:
                        await redisCacheService.changeCurrentGameStatus(groupName, "spiritualQuestionAnsweredCorrectOrNotState");
                        await _hub.Clients.Group(groupName).updateWaitingProgess(0.0);
                        await redisCacheService.resetAllViewedResultState(groupName);
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
                    await redisCacheService.setWhoAnswerSpritualQuestionsCorrectly(groupName, name);
                    await redisCacheService.changeVote(groupName, name, changedVote: 0.5, option: "add");
                }
            }
            Users? nextDicussingUser = await redisCacheService.whoIsDiscussingNext(groupName);
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
                List<string>? names = await redisCacheService.getWhoAnswerSpritualQuestionsCorrectly(groupName);
                if(names != null && names.Count > 0)
                {
                    await redisCacheService.setGameMessageHistory(groupName, messages: names);
                    await _hub.Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                    await redisCacheService.resetWhoAnswerSpritualQuestionsCorrectly(groupName);
                }
                await _hub.Clients.Group(groupName).inAnswerQuestionName();
                string topic = await randomPickATopic(groupName);
                await _hub.Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName))
                    .nextStep(new NextStep("discussing", new List<string>() { topic }));
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
            List<Users> notInGameUsers = await redisCacheService.collectAllExiledAndOfflineUserName(groupName);
            return notInGameUsers.Select(x => x.connectionId).ToList();
        }
        private async Task<string> randomPickATopic(string groupName)
        {
            Random rand = new Random();
            string topic = "";
            switch(rand.Next(0, 5))
            {
                case 0:
                    topic = "Who is the Priest?";
                    break;
                case 1:
                    topic = "Who is the Pharisees?";
                    break;
                case 2:
                    topic = "Who is Judas?";
                    break;
                case 3:
                    topic = "Who is Peter?";
                    break;
                case 4:
                    topic = "Who is John?";
                    break;
                default:
                    topic = "error";
                    break;
            }
            await redisCacheService.setDiscussingTopic(groupName, topic);
            return topic;
        }

        [HttpPost("PriestRound/{groupName}")]
        public async Task PriestRound(string groupName)
        {
            Users? Priest = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
            if( Priest != null )
            {
                if(Priest.offLine)
                {
                    Users? exileUser = await redisCacheService.chooseARandomPlayerToExile(groupName);
                    if(exileUser != null )
                    {
                        await exileHimOrHer(groupName, exileUser.name);
                    }
                }
                else
                {
                    await _hub.Clients.Client(Priest.connectionId).PriestRound();
                }
            }
        }
    }
}
