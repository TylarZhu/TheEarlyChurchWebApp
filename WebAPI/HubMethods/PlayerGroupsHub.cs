using Domain.APIClass;
using Domain.Common;
using Domain.DBEntities;
using Domain.Interfaces;
using Infrastructure.DBService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver.Core.Connections;
using Redis.OM.Modeling;
using System.Runtime.InteropServices;
using WebAPI.Controllers;

namespace WebAPI.HubMethods
{
    public class PlayerGroupsHub : PlayerGroupsHubBase
    {
        private readonly ICacheService redisCacheService;
        private readonly IQuestionsService questionsService;
        public PlayerGroupsHub(ICacheService redisCacheService, IQuestionsService questionsService)
        {
            this.redisCacheService = redisCacheService;
            this.questionsService = questionsService;
        }

        // group connnection
        public override async Task onConntionAndCreateGroup(CreateNewUser createNewUser)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, createNewUser.groupName);
            await Clients.Caller.CreateNewUserJoinNewGroup(Context.ConnectionId, createNewUser.groupName, createNewUser.name, createNewUser.maxPlayerInGroup);
        }
        public override async Task leaveGroup(string groupName, string name)
        {
            Users? leaveUser = await redisCacheService.removeUserFromGroup(groupName, name);
            // if we cannot find the user, meaning the user refresh the page and the url is still on gameRoom.
            // so this method will not be called. OnDisconnectedAsync will be called.
            if (leaveUser != null)
            {
                await redisCacheService.removeConnectionIdFromGroup(leaveUser.connectionId, groupName);
                await Clients.Group(groupName).updateUserList(
                    await redisCacheService.getAllUsers(groupName));
                // if the group is not empty and the current leaving user is the group leader,
                // then update the next group leader to all users.
                if (!await redisCacheService.isGroupEmpty(groupName))
                {
                    if (leaveUser.groupLeader)
                    {
                        Users? onlineUser = await redisCacheService.getGroupLeaderFromGroup(groupName);
                        await Clients.Group(groupName).updateGroupLeader(new CreateNewUser(onlineUser!.connectionId, onlineUser.name, groupName, "0"));
                    }
                }
                else
                {
                    await redisCacheService.removeGroupInConnectionIdAndGroupName(groupName);
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            }
        }

        // Game methods
        public override async Task<int> IdentityViewingState(string groupName, string playerName)
        {
            await redisCacheService.setViewedResultToTrue(groupName, playerName);
            List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
            if (didNotViewedUsers == null)
            {
                return -1;
            }
            else if (didNotViewedUsers.Any())
            {
                await Clients.Group(groupName).stillWaitingFor(didNotViewedUsers);
                string? currentConnection = await redisCacheService.getConnectionIdByName(groupName, playerName);
                if (currentConnection == null)
                {
                    return -1;
                }
                await Clients.Group(groupName).updateWaitingProgess(
                    await redisCacheService.getWaitingProgessPercentage(groupName, didNotViewedUsers.Count));
                await Clients.Client(currentConnection).finishedViewIdentityAndWaitOnOtherPlayers(true);
            }
            else
            {
                await Clients.Group(groupName).finishedViewIdentityAndWaitOnOtherPlayers(false);
                await Clients.Group(groupName).updateWaitingProgess(0.0);
                // didNotViewedUsers should be empty
                await redisCacheService.resetAllViewedResultState(groupName);
                await redisCacheService.changeCurrentGameStatus(groupName, "DiscussingState");
                await Clients.Group(groupName).nextStep(new NextStep("discussing"));
                await whoIsDiscussing(groupName);
            }
            return 0;
        }
        public override async Task<int> whoIsDiscussing(string groupName)
        {
            Users? nextDicussingUser = await redisCacheService.whoIsDiscussingNext(groupName);
            // if there is still user who did not discuss
            if (nextDicussingUser != null)
            {
                // next user goes to discuss state
                await Clients.GroupExcept(groupName, nextDicussingUser.connectionId)
                    .currentUserInDiscusstion("Waiting", nextDicussingUser.name);
                await Clients.Client(nextDicussingUser.connectionId).currentUserInDiscusstion("InDisscussion");
                return 0;
            }
            // all user finish discussing
            else
            {
                await Clients.Group(groupName).currentUserInDiscusstion("AllUserFinishDisscussion");
                // New Rule: we do not vote on day one.
                if (await redisCacheService.getDay(groupName) != 1)
                {
                    await redisCacheService.changeCurrentGameStatus(groupName, "VoteState");
                    await Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName)).nextStep(new NextStep("vote"));
                }
                else
                {
                    await redisCacheService.changeCurrentGameStatus(groupName, "PriestRoundState");
                    await Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName))
                        .nextStep(new NextStep("SetUserToNightWaiting"));
                    await FirstNightPriestNicoPhariseeMeetingRound(groupName);
                }
                return 0;
            }
        }
        public override async Task<int> voteHimOrHer(string groupName, string votePerson, string fromWho)
        {
            await redisCacheService.setViewedResultToTrue(groupName, fromWho);
            List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
            if (didNotViewedUsers == null)
            {
                return -1;
            }
            else if (didNotViewedUsers.Any())
            {
                // set everyoneFinishVoting = true, because not all user finish voting.
                await redisCacheService.votePerson(groupName, votePerson, fromWho, true);
                await Clients.Group(groupName).stillWaitingFor(didNotViewedUsers);
                // This user is finish voting, set this user to waiting state
                await Clients.Client(await redisCacheService.getConnectionIdByName(groupName, fromWho) ?? "")
                    .finishVoteWaitForOthersOrVoteResult(true);
                await Clients.Group(groupName).updateWaitingProgess(
                    await redisCacheService.getWaitingProgessPercentage(groupName, didNotViewedUsers.Count));
                return 0;
            }
            else
            {
                int result = await redisCacheService.votePerson(groupName, votePerson, fromWho, false);
                switch (result)
                {
                    case 1:
                        await Clients.Group(groupName).finishVoteWaitForOthersOrVoteResult(false, "A Christian has lost all his/her vote weight!");
                        await redisCacheService.setVoteResult(groupName, "A Christian has lost all his/her vote weight!");
                        await redisCacheService.setGameMessageHistory(groupName, "A Christian has lost all his/her vote weight!");
                        await Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                        break;
                    case 2:
                        await Clients.Group(groupName).finishVoteWaitForOthersOrVoteResult(false, "A Judasim has lost all his/her vote weight!");
                        await redisCacheService.setVoteResult(groupName, "A Judasim has lost all his/her vote weight!");
                        await redisCacheService.setGameMessageHistory(groupName, "A Judasim has lost all his/her vote weight!");
                        await Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                        break;
                    case 3:
                        await Clients.Group(groupName).finishVoteWaitForOthersOrVoteResult(false, "There is a tie! No one have lost voted weight!");
                        await redisCacheService.setVoteResult(groupName, "There is a tie! No one have lost voted weight!");
                        await redisCacheService.setGameMessageHistory(groupName, "There is a tie! No one have lost voted weight!");
                        await Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                        break;
                    default:
                        return -1;
                }
                switch(await redisCacheService.whoWins(groupName))
                {
                    case 1:
                        await Clients.Group(groupName).announceWinner(1);
                        await announceGameHistoryAndCleanUp(groupName);
                        break;
                    case 2:
                        await Clients.Group(groupName).announceWinner(2);
                        await announceGameHistoryAndCleanUp(groupName);
                        break;
                    default:
                        // changeCurrentGameStatus before reset viewedResult, in case user reconnect and put on waiting again.
                        await redisCacheService.resetAllViewedResultState(groupName);
                        await Clients.Group(groupName).updateWaitingProgess(0.0);
                        await Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName))
                            .nextStep(new NextStep("SetUserToNightWaiting"));
                        await ROTSInfoRound(groupName);
                        break;
                }
                return 0;
            }
        }
        public override async Task<int> ROTSInfoRound(string groupName)
        {
            Users? ROTS = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Pharisee);
            if (ROTS != null)
            {
                if (!ROTS.offLine && ROTS.inGame && !ROTS.disempowering)
                {
                    (string name, Identities identity)? lastExiledPlayer = await redisCacheService.getLastNightExiledPlayer(groupName);
                    if (lastExiledPlayer == null)
                    {
                        return -1;
                    }
                    if (lastExiledPlayer.Value.identity == Identities.John ||
                        lastExiledPlayer.Value.identity == Identities.Peter ||
                        lastExiledPlayer.Value.identity == Identities.Laity)
                    {
                        await Clients.Client(ROTS.connectionId).announceLastExiledPlayerInfo(true, lastExiledPlayer.Value.name);
                    }
                    else
                    {
                        await Clients.Client(ROTS.connectionId).announceLastExiledPlayerInfo(false, lastExiledPlayer.Value.name);
                    }
                }
                // New Rule: Judas and Priest can meet after day 3.
                if (await redisCacheService.getDay(groupName) >= 3)
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
                        return 0;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }
            else
            {
                return -1;
            }
        }
        private async Task FirstNightPriestNicoPhariseeMeetingRound(string groupName)
        {
            Users? Preist = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
            Users? Nico = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);
            Users? Pharisee = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Pharisee);
            if (Preist != null && Nico != null && Pharisee != null)
            {
                await Clients.Clients(
                        new List<string>() { Preist.connectionId, Nico.connectionId, Pharisee.connectionId })
                        .PriestROTSNicoMeet(Pharisee.name, Preist.name, Nico.name);
                await PriestRound(groupName);
                await Clients.Client(Pharisee.connectionId).RulerOfTheSynagogueMeeting();
                await Clients.Client(Nico.connectionId).NicoMeeting();
            }

        }
        public override async Task<int> JudasMeetWithPriest(string groupName, string JudasHint = "")
        {
            Users? Judas = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Judas);
            Users? Priest = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);

            if (Priest != null && Judas != null)
            {
                if (Judas.inGame && !Judas.offLine)
                {
                    if (string.IsNullOrEmpty(JudasHint))
                    {
                        await Clients.Client(Judas.connectionId).JudasGivePriestHint();
                        return 0;
                    }
                    else
                    {
                        await Clients.Client(Judas.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                        await Clients.Client(Priest.connectionId).PriestReceiveHint(Judas.name, JudasHint);
                    }
                }
                await redisCacheService.changeCurrentGameStatus(groupName, "PriestRoundState");
                await PriestRound(groupName);
                return 0;
            }
            else
            {
                return -1;
            }
        }
        public override async Task<int> exileHimOrHer(string groupName, string exileName)
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
            return 0;
        }
        private async Task<bool> NicodemusSavingRoundBegin(string groupName, string exileName)
        {
            Users? Nicodemus = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);

            if (Nicodemus != null)
            {
                if (!Nicodemus.offLine && Nicodemus.nicodemusProtection && !Nicodemus.disempowering)
                {
                    await Clients.Client(Nicodemus.connectionId)
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
        public override async Task<int> NicodemusAction(string groupName, bool saveOrNot)
        {
            bool result = await redisCacheService.setExile(groupName, !saveOrNot);
            if (saveOrNot)
            {
                await redisCacheService.NicodemusSetProtection(groupName, false);
            }
            if (result)
            {
                await redisCacheService.changeCurrentGameStatus(groupName, "JohnFireRoundBeginState");
                await JohnFireRoundBegin(groupName);
                return 0;
            }
            else
            {
                return -1;
            }
        }
        public override async Task<int> JohnFireRoundBegin(string groupName, string fireName = "", bool didUserChoose = false)
        {
            Users? John = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.John);
            if (John != null)
            {
                // if John is not offline and he is in game and did not disempowered and there are still player who did not been fired
                // then proceed to John's turn.
                if (!John.offLine && John.inGame && !John.disempowering && !await redisCacheService.checkJohnFireAllOrNot(groupName))
                {
                    // if this method is invoked by the progarm, then let the user to choose.
                    if (didUserChoose)
                    {
                        if (fireName != "NULL")
                        {
                            await redisCacheService.changeVote(groupName, name: fireName, option: "half");
                            await redisCacheService.AddToJohnFireList(groupName, fireName);
                            await Clients.Client(John.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
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
                            await Clients.Client(John.connectionId).nextStep(new NextStep("JohnFireRound", list));
                        }
                        return 0;
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
                    await redisCacheService.changeCurrentGameStatus(groupName, "NightRoundEndState");
                    await NightRoundEnd(groupName);
                }
                return 0;
            }
            return -1;
        }
        public override async Task<int> JudasCheckRound(string groupName, string checkName = "")
        {
            Users? Judas = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Judas);
            
            if (Judas != null)
            {
                if (!Judas.offLine && Judas.inGame)
                {
                    if (string.IsNullOrEmpty(checkName))
                    {
                        Users? Priest = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
                        if(Priest != null)
                        {
                            await Clients.Client(Judas.connectionId).nextStep(new NextStep("JudasCheckRound", 
                                new List<string>() { Judas.name, Priest.name }));
                        }
                    }
                    else
                    {
                        await Clients.Client(Judas.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                        if (Judas.disempowering)
                        {
                            await Clients.Client(Judas.connectionId).JudasCheckResult(false);
                        }
                        else
                        {
                            await Clients.Client(Judas.connectionId).JudasCheckResult(
                                await redisCacheService.JudasCheck(groupName, checkName));
                        }
                    }
                }
                else
                {
                    // If Judas is out of game, we do not want ending night round fast because player will release that Judas is out.
                    /*Thread.Sleep(3000);*/
                    await redisCacheService.changeCurrentGameStatus(groupName, "NightRoundEndState");
                    await NightRoundEnd(groupName);
                }
                return 0;
            }
            return -1;
        }
        public override async Task<int> NightRoundEnd(string groupName)
        {
            Users? user = await redisCacheService.checkAndSetIfAnyoneOutOfGame(groupName);
            List<Users> notInGameUsers = await redisCacheService.collectAllExileUserName(groupName);
            await Clients.Group(groupName).nextStep(new NextStep("quitNightWaiting"));
            if (user != null)
            {
                await redisCacheService.setGameMessageHistory(groupName, $"{user.name} has been exiled!");
                await Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                await Clients.Group(groupName).updateExiledUsers(notInGameUsers);
                await Clients.Group(groupName).announceExile(user.name);
            }
            else
            {
                await redisCacheService.setGameMessageHistory(groupName, $"No one has been exiled!");
                await Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                await Clients.Group(groupName).announceExile("No one");
            }
            // increase day at last, because history message need to record first.
            await Clients.Group(groupName).changeDay(await redisCacheService.increaseAndGetDay(groupName));
            await redisCacheService.PeterIncreaseVoteWeightByOneOrNot(groupName);
            /*await redisCacheService.changeCurrentGameStatus(groupName, "finishedToViewTheExileResultState");*/
            return 0;
        }
        public override async Task<int> finishedToViewTheExileResult(string groupName, string playerName)
        {
            await redisCacheService.setViewedResultToTrue(groupName, playerName);
            List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
            if (didNotViewedUsers == null)
            {
                return -1;
            }
            else if (didNotViewedUsers.Any())
            {
                await Clients.Group(groupName).stillWaitingFor(didNotViewedUsers);
                await Clients.Client(await redisCacheService.getConnectionIdByName(groupName, playerName) ?? "").openOrCloseExileResultModal(true);
                await Clients.Group(groupName).updateWaitingProgess(
                    await redisCacheService.getWaitingProgessPercentage(groupName, didNotViewedUsers.Count));
            }
            else
            {
                await Clients.Group(groupName).openOrCloseExileResultModal(false);
                switch (await redisCacheService.whoWins(groupName))
                {
                    case 1:
                        await Clients.Group(groupName).announceWinner(1);
                        await announceGameHistoryAndCleanUp(groupName);
                        break;
                    case 2:
                        await Clients.Group(groupName).announceWinner(2);
                        await announceGameHistoryAndCleanUp(groupName);
                        break;
                    default:
                        await redisCacheService.changeCurrentGameStatus(groupName, "spiritualQuestionAnsweredCorrectOrNotState");
                        await Clients.Group(groupName).updateWaitingProgess(0.0);
                        await redisCacheService.resetAllViewedResultState(groupName);
                        await spiritualQuestionAnsweredCorrectOrNot(groupName);
                        break;
                }
            }
            return 0;
        }
        public override async Task<int> spiritualQuestionAnsweredCorrectOrNot(string groupName, string name = "", bool playerChoiceCorrectOrNot = false)
        {
            if (!string.IsNullOrEmpty(name))
            {
                if (playerChoiceCorrectOrNot)
                {
                    await redisCacheService.setWhoAnswerSpritualQuestionsCorrectly(groupName, name);
                    await redisCacheService.changeVote(groupName, name, changedVote: 0.25, option: "add");
                }
            }
            Users? nextDicussingUser = await redisCacheService.whoIsDiscussingNext(groupName);
            // if there is still user who did not answer questions
            if (nextDicussingUser != null)
            {
                Questions? q = await questionsService.RandomSelectAQuestion();
                if (q != null)
                {
                    await Clients.Group(groupName).inAnswerQuestionName(nextDicussingUser.name);
                    await Clients.Client(nextDicussingUser.connectionId).getAQuestion(q);
                }
                return 0;
            }
            // all user finish answer questions
            else
            {
                List<string>? names = await redisCacheService.getWhoAnswerSpritualQuestionsCorrectly(groupName);
                if (names != null && names.Count > 0)
                {
                    await redisCacheService.setGameMessageHistory(groupName, messages: names);
                    await Clients.Group(groupName).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                    await redisCacheService.resetWhoAnswerSpritualQuestionsCorrectly(groupName);
                }
                await Clients.Group(groupName).inAnswerQuestionName();
                string topic = await randomPickATopic(groupName);
                await Clients.GroupExcept(groupName, await getNotInGameUsersConnectiondIds(groupName))
                    .nextStep(new NextStep("discussing", new List<string>() { topic }));
                await redisCacheService.changeCurrentGameStatus(groupName, "DiscussingState");
                await whoIsDiscussing(groupName);
                return 0;
            }
        }
       
        private async Task announceGameHistoryAndCleanUp(string groupName)
        {
            await Clients.Group(groupName).announceGameHistory(await redisCacheService.getGameHistory(groupName) ?? null);
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
            switch (rand.Next(0, 5))
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
        public override async Task PriestRound(string groupName, bool outOfTime = false)
        {
            Users? Priest = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
            if (Priest != null)
            {
                if (Priest.offLine || outOfTime)
                {
                    Users? exileUser = await redisCacheService.chooseARandomPlayerToExile(groupName);
                    if (exileUser != null)
                    {
                        await exileHimOrHer(groupName, exileUser.name);
                    }
                }
                else
                {
                    await Clients.Client(Priest.connectionId).PriestRound();
                }
            }
        }

        // handel reconnection
        public override async Task reconnectionToGame(string groupName, string name)
        {
            Users? user = await redisCacheService.getOneUserFromGroup(groupName, name);
            // if user != null, meaning the group is in game because OnDisconnectedAsync did not delete user.
            // if user == null, meaning the group is not in game.
            if (user != null && user.connectionId != Context.ConnectionId)
            {
                string newConnectionId = Context.ConnectionId;
                string oldConnectionId = user.connectionId;
                user.connectionId = newConnectionId;
                string gameStatus = await redisCacheService.getCurrentGameStatus(groupName);
                if (!user.offLine)
                {
                    await redisCacheService.removeUserFromGroupByConnectionIdAndSetOfflineTrue(oldConnectionId);
                    await redisCacheService.removeConnectionIdFromGroup(oldConnectionId);
                    if(user.inGame)
                    {
                        await onDisconnectGameOnStateReaction(gameStatus, groupName, user);
                    }
                }
                // hub operations
                await redisCacheService.addConnectionIdToGroup(newConnectionId, groupName);
                await Groups.AddToGroupAsync(newConnectionId, groupName);
                // update group infomation
                await Clients.Client(newConnectionId).getMaxPlayersInGroup(await redisCacheService.getMaxPlayersInGroup(groupName));
                await Clients.Client(newConnectionId).updateUserList(await redisCacheService.getAllUsers(groupName));
                Users? groupLeader = await redisCacheService.getGroupLeaderFromGroup(groupName);
                if (groupLeader != null)
                {
                    await Clients.Client(newConnectionId).updateGroupLeader(new CreateNewUser(groupLeader.connectionId, groupLeader.name, groupName, "0"));
                }

                // basic operations
                await redisCacheService.setOfflineFalse(groupName, name);
                await redisCacheService.setNewConnectionId(groupName, name, newConnectionId);

                Explanation explanation = new Explanation();
                List<string>? ex = explanation.getExplanation(user.identity);
                if (ex != null)
                {
                    await Clients.Client(newConnectionId).updatePlayersIdentities(user.identity.ToString());
                    await Clients.Client(newConnectionId).IdentitiesExplanation(ex);
                    await Clients.Client(newConnectionId).identityModalOpen(false);
                }
                await Clients.Client(newConnectionId).updateExiledUsers(await redisCacheService.collectAllExileUserName(groupName));
                await announceOffLinePlayer(await redisCacheService.getListOfOfflineUsers(groupName), groupName);
                await Clients.Client(newConnectionId).changeDay(await redisCacheService.increaseAndGetDay(groupName, true));
                await Clients.Client(newConnectionId).updateGameMessageHistory(await redisCacheService.getGameMessageHistory(groupName));
                // if user is in game, then send infomation according to identity and day.
                if (user.inGame)
                {
                    await onReconnectionGameOnStateReaction(gameStatus, groupName, user);
                }
            }
        }
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Users? leaveUser = await redisCacheService.removeUserFromGroupByConnectionIdAndSetOfflineTrue(Context.ConnectionId);
            string? removeUserGroupName = await redisCacheService.removeConnectionIdFromGroup(Context.ConnectionId);
            // if leaveUser is null and removeUserGroupName is null, meaning user quit application correctly.
            if (leaveUser != null && removeUserGroupName != null)
            {
                if (await redisCacheService.getGameInProgess(removeUserGroupName))
                {
                    List<Users>? offlineUsers = await redisCacheService.getListOfOfflineUsers(removeUserGroupName);
                    await announceOffLinePlayer(offlineUsers, removeUserGroupName);

                    string currentGameStatus = await redisCacheService.getCurrentGameStatus(removeUserGroupName);
                    if (leaveUser.inGame)
                    {
                        await onDisconnectGameOnStateReaction(currentGameStatus, removeUserGroupName, leaveUser);
                    }
                }
                else
                {
                    await Clients.Group(removeUserGroupName).updateUserList(
                        await redisCacheService.getAllUsers(removeUserGroupName));
                    if (!await redisCacheService.isGroupEmpty(removeUserGroupName) && leaveUser.groupLeader)
                    {
                        Users? onlineUser = await redisCacheService.getGroupLeaderFromGroup(removeUserGroupName);
                        if (onlineUser != null)
                        {
                            await Clients.Group(removeUserGroupName)
                                .updateGroupLeader(new CreateNewUser(onlineUser.connectionId, onlineUser.name, removeUserGroupName, "0"));
                        }
                    }
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, removeUserGroupName);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        // helper methods for reconnection and disconnection
        private async Task announceOffLinePlayer(List<Users>? offlineUsers, string removeUserGroupName)
        {
            List<string> offlineUsersNames = new List<string>();
            if (offlineUsers != null)
            {
                foreach (Users offlineUser in offlineUsers)
                {
                    offlineUsersNames.Add(offlineUser.name);
                }
            }
            await Clients.Group(removeUserGroupName).announceOffLinePlayer(offlineUsersNames);
        }
        /// <summary>
        /// This method helps the game to move on, when players refresh or close tab.
        /// </summary>
        /// <param name="currentGameStatus"></param>
        /// <param name="removeUserGroupName"></param>
        /// <param name="leaveUser"></param>
        /// <returns></returns>
        private async Task onDisconnectGameOnStateReaction(string currentGameStatus, string removeUserGroupName, Users leaveUser)
        {
            if (currentGameStatus == "IdentityViewingState")
            {
                /*Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                if (nextPlayer != null)
                {
                    await Clients.Client(nextPlayer.connectionId).IdentityViewingStateFinish(removeUserGroupName, leaveUser.name);
                }*/
                await IdentityViewingState(removeUserGroupName, leaveUser.name);
            }
            else if (currentGameStatus == "DiscussingState")
            {
                string? currentDissingUserName = await redisCacheService.getWhoIsCurrentlyDiscussing(removeUserGroupName);
                // if the current discussing user leaves, then skip put next user in discusstion.
                if(!string.IsNullOrEmpty(currentDissingUserName) && currentDissingUserName == leaveUser.name)
                {
                    /*Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).DiscussingStateFinish(removeUserGroupName);
                    }*/
                    await whoIsDiscussing(removeUserGroupName);
                }
            }
            else if (currentGameStatus == "VoteState")
            {
                await voteHimOrHer(removeUserGroupName, leaveUser.name, leaveUser.name);
            }
            else if (currentGameStatus == "PriestRoundState")
            {
                if (leaveUser.identity == Identities.Preist)
                {
                    /*Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).PriestRoundStateFinish(removeUserGroupName);
                    }*/
                    await PriestRound(removeUserGroupName);
                }
            }
            else if (currentGameStatus == "JudasMeetWithPriestState")
            {
                if (leaveUser.identity == Identities.Judas)
                {
                    /*Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).JudasMeetWithPriestStateFinish(removeUserGroupName);
                    }*/
                    await JudasMeetWithPriest(removeUserGroupName);
                }
            }
            else if (currentGameStatus == "NicodemusSavingRoundBeginState")
            {
                if (leaveUser.identity == Identities.Nicodemus)
                {
                    /*Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).NicodemusSavingRoundBeginStateFinish(removeUserGroupName);
                    }*/
                    await NicodemusAction(removeUserGroupName, false);
                }
            }
            else if (currentGameStatus == "JohnFireRoundBeginState")
            {
                if (leaveUser.identity == Identities.John)
                {
                    /*Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).JohnFireRoundBeginStateFinish(removeUserGroupName);
                    }*/
                    await JohnFireRoundBegin(removeUserGroupName, "NULL", true);
                }
            }
            else if (currentGameStatus == "JudasCheckRoundState")
            {
                if (leaveUser.identity == Identities.Judas)
                {
                   /*Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).JudasCheckRoundStateFinish(removeUserGroupName);
                    }*/
                   await JudasCheckRound(removeUserGroupName);
                }
            }
            else if (currentGameStatus == "NightRoundEndState")
            {
                /*Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                if (nextPlayer != null)
                {
                    await Clients.Client(nextPlayer.connectionId).finishedToViewTheExileResultStateFinish(removeUserGroupName, leaveUser.name);
                }*/
                await finishedToViewTheExileResult(removeUserGroupName, leaveUser.name);
            }
            else if (currentGameStatus == "spiritualQuestionAnsweredCorrectOrNotState")
            {
                string? currentDissingUser = await redisCacheService.getWhoIsCurrentlyDiscussing(removeUserGroupName);
                // if the current discussing user leaves, then skip put next user in discusstion.
                if (!string.IsNullOrEmpty(currentDissingUser) && currentDissingUser == leaveUser.name)
                {
                    /*Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).spiritualQuestionAnsweredCorrectOrNotStateFinish(removeUserGroupName, leaveUser.name);
                    }*/
                    await spiritualQuestionAnsweredCorrectOrNot(removeUserGroupName, leaveUser.name);
                }
            }
        }
        /// <summary>
        /// Think about currentGameStatus what should display on user interface. 
        /// Do not think about the next step, because the game should automatically 
        /// continue to the next step according to InGameController after the user reconnection.
        /// </summary>
        /// <param name="day"></param>
        /// <param name="currentGameStatus"></param>
        /// <param name="groupName"></param>
        /// <param name="reconnectUser"></param>
        /// <returns></returns>
        private async Task onReconnectionGameOnStateReaction(string currentGameStatus, string groupName, Users? reconnectUser)
        {
            if (reconnectUser != null)
            {
                // PriestNicoPhariseeMeetingState
                if (reconnectUser.identity == Identities.Preist ||
                    reconnectUser.identity == Identities.Nicodemus ||
                    reconnectUser.identity == Identities.Pharisee)
                {
                    Users? Preist = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
                    Users? Nico = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);
                    Users? Pharisee = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Pharisee);
                    if (Preist != null && Nico != null && Pharisee != null)
                    {
                        await Clients.Client(reconnectUser.connectionId).PriestROTSNicoMeet(Pharisee.name, Preist.name, Nico.name);
                        if (reconnectUser.identity == Identities.Preist)
                        {
                            await Clients.Client(reconnectUser.connectionId).PriestMeetingOnReconnect();
                        }
                        if (reconnectUser.identity == Identities.Pharisee)
                        {
                            await Clients.Client(reconnectUser.connectionId).RulerOfTheSynagogueMeeting();
                        }
                        if (reconnectUser.identity == Identities.Nicodemus)
                        {
                            await Clients.Client(reconnectUser.connectionId).NicoMeeting();
                        }
                    }
                }
                // ROTSInfoRound
                if (reconnectUser.identity == Identities.Pharisee && !reconnectUser.disempowering)
                {
                    (string name, Identities identity)? lastExiledPlayer = await redisCacheService.getLastNightExiledPlayer(groupName);
                    if (lastExiledPlayer != null)
                    {
                        if (lastExiledPlayer.Value.identity == Identities.John ||
                           lastExiledPlayer.Value.identity == Identities.Peter ||
                           lastExiledPlayer.Value.identity == Identities.Laity)
                        {
                            await Clients.Client(reconnectUser.connectionId).announceLastExiledPlayerInfo(true, lastExiledPlayer.Value.name);
                        }
                        else
                        {
                            await Clients.Client(reconnectUser.connectionId).announceLastExiledPlayerInfo(false, lastExiledPlayer.Value.name);
                        }
                    }
                }
                if(reconnectUser.identity == Identities.Judas)
                {
                    Users? Preist = await redisCacheService.getSpecificUserFromGroupByIdentity(groupName, Identities.Preist);
                    if (Preist != null)
                    {
                        await Clients.Client(reconnectUser.connectionId).AssignJudasHimselfAndPriestName(Preist.name, reconnectUser.name);
                    }
                }
                if (currentGameStatus == "IdentityViewingState")
                {
                    if (!reconnectUser.viewedResult)
                    {
                        await IdentityViewingState(groupName, reconnectUser.name);
                    }
                    else
                    {
                        List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
                        if (didNotViewedUsers != null)
                        {
                            if (didNotViewedUsers.Any())
                            {
                                await Clients.Client(reconnectUser.connectionId).stillWaitingFor(didNotViewedUsers);
                                await Clients.Client(reconnectUser.connectionId).finishedViewIdentityAndWaitOnOtherPlayers(true);
                                await Clients.Client(reconnectUser.connectionId).updateWaitingProgess(
                                    await redisCacheService.getWaitingProgessPercentage(groupName, didNotViewedUsers.Count));
                            }
                        }
                    }
                }
                // skip the IdentityViewingState, because user could view their identity later
                else if (currentGameStatus == "DiscussingState")
                {
                    await Clients.Client(reconnectUser.connectionId)
                        .nextStep(new NextStep("discussing", new List<string>() { await redisCacheService.getDiscussingTopic(groupName) }));
                    string? currentDissingUser = await redisCacheService.getWhoIsCurrentlyDiscussing(groupName);
                    if (!string.IsNullOrEmpty(currentDissingUser))
                    {
                        await Clients.Client(reconnectUser.connectionId).currentUserInDiscusstion("", currentDissingUser);
                    }
                }
                // skip the VoteState, because user disconnect already vote himself.
                else if (currentGameStatus == "VoteState")
                {
                    // sometimes onDisconnectGameOnStateReaction does not fire, so we need to fire again.
                    if (!reconnectUser.viewedResult)
                    {
                        await voteHimOrHer(groupName, reconnectUser.name, reconnectUser.name);
                    }
                    else
                    {
                        List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
                        if (didNotViewedUsers != null)
                        {
                            // didNotViewedUsers will not be reset, because currentGameStatus will change 
                            if (didNotViewedUsers.Any())
                            {
                                await Clients.Client(reconnectUser.connectionId).stillWaitingFor(didNotViewedUsers);
                                await Clients.Client(reconnectUser.connectionId).finishVoteWaitForOthersOrVoteResult(true);
                                await Clients.Client(reconnectUser.connectionId).updateWaitingProgess(
                                    await redisCacheService.getWaitingProgessPercentage(groupName, didNotViewedUsers.Count));
                            }
                        }
                    }
                }
                // skip PriestRoundState, because it could cause race condition.
                else if (currentGameStatus == "PriestRoundState")
                {
                    await Clients.Client(reconnectUser.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                }
                else if (currentGameStatus == "JudasMeetWithPriestState")
                {
                    await Clients.Client(reconnectUser.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                }
                else if (currentGameStatus == "NicodemusSavingRoundBeginState")
                {
                    await Clients.Client(reconnectUser.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                }
                else if (currentGameStatus == "JohnFireRoundBeginState")
                {
                    await Clients.Client(reconnectUser.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                }
                else if (currentGameStatus == "JudasCheckRoundState")
                {
                    await Clients.Client(reconnectUser.connectionId).nextStep(new NextStep("SetUserToNightWaiting"));
                }
                else if (currentGameStatus == "NightRoundEndState")
                {
                    // when Judas disconnects from JudasCheckRoundState, the viewedResult will remain false.
                    // so when Judas reconnects, we should not wait for him.
                    // sometimes onDisconnectGameOnStateReaction does not fire, so we need to fire again.
                    if (!reconnectUser.viewedResult)
                    {
                        await finishedToViewTheExileResult(groupName, reconnectUser.name);
                    }
                    else
                    {
                        List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
                        if (didNotViewedUsers != null)
                        {
                            // didNotViewedUsers will not be reset, because currentGameStatus will change 
                            if (didNotViewedUsers.Any())
                            {
                                await Clients.Client(reconnectUser.connectionId).stillWaitingFor(didNotViewedUsers);
                                await Clients.Client(reconnectUser.connectionId).openOrCloseExileResultModal(true);
                                await Clients.Client(reconnectUser.connectionId).updateWaitingProgess(
                                    await redisCacheService.getWaitingProgessPercentage(groupName, didNotViewedUsers.Count));
                            }
                        }
                    }
                }
                // skip spiritualQuestionAnsweredCorrectOrNotState
            }
        }
    }
}
