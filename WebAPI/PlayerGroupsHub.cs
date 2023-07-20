using Domain.APIClass;
using Domain.Common;
using Domain.DBEntities;
using Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver.Core.Connections;
using Redis.OM.Modeling;
using System.Runtime.InteropServices;
using WebAPI.Controllers;

namespace WebAPI
{
    internal class PlayerGroupsHub : PlayerGroupsHubBase
    {
        private readonly ICacheService redisCacheService;
        public PlayerGroupsHub(ICacheService redisCacheService)
        {
            this.redisCacheService = redisCacheService;
        }
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
                if(!await redisCacheService.isGroupEmpty(groupName))
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
        public override async Task reconnectionToGame(string groupName, string name)
        {
            Users? user = await redisCacheService.getOneUserFromGroup(groupName, name);
            // check if user first time to connect to server.
            if (user != null && user.connectionId != Context.ConnectionId)
            {
                string newConnectionId = Context.ConnectionId;
                await redisCacheService.addConnectionIdToGroup(groupName, newConnectionId);
                await Groups.AddToGroupAsync(newConnectionId, groupName);
                // update group infomation
                await Clients.Client(newConnectionId).getMaxPlayersInGroup(await redisCacheService.getMaxPlayersInGroup(groupName));
                await Clients.Client(newConnectionId).updateUserList(await redisCacheService.getAllUsers(groupName));
                Users? groupLeader = await redisCacheService.getGroupLeaderFromGroup(groupName);
                if (groupLeader != null)
                {
                    await Clients.Client(newConnectionId).updateGroupLeader(new CreateNewUser(groupLeader.connectionId, groupLeader.name, groupName, "0"));
                }
                // if user != null, meaning the group is in game because OnDisconnectedAsync did not delete user.
                // if user == null, meaning the group is not in game.
                if (user != null && user.connectionId != newConnectionId)
                {
                    // basic operations
                    await redisCacheService.setOfflineFalse(groupName, name);
                    await redisCacheService.setNewConnectionId(groupName, name, newConnectionId);

                    // if group is in game, then update game infomation.
                    if (await redisCacheService.getGameInProgess(groupName))
                    {
                        Explanation explanation = new Explanation();
                        List<string>? ex = explanation.getExplanation(user.identity);
                        if (ex != null)
                        {
                            await Clients.Client(newConnectionId).updatePlayersIdentities(user.identity.ToString());
                            await Clients.Client(newConnectionId).IdentitiesExplanation(ex, false);
                        }
                        await Clients.Client(newConnectionId).updateExiledUsers(await redisCacheService.collectAllExileUserName(groupName));
                        await announceOffLinePlayer(await redisCacheService.getListOfOfflineUsers(groupName), groupName);
                        await Clients.Client(newConnectionId).changeDay(await redisCacheService.increaseAndGetDay(groupName, true));
                        // if user is in game, then send infomation according to identity and day.
                        if (user.inGame)
                        {
                            await onReconnectionGameOnStateReaction(await redisCacheService.getCurrentGameStatus(groupName), groupName, 
                                await redisCacheService.getOneUserFromGroup(groupName, name) ?? null);
                        }
                    }
                }
            }
        }
        // This method will call when user close tab or refresh the page.
        // We assume that only one user will remain online 
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Users? leaveUser = await redisCacheService.removeUserFromGroupByConnectionIdAndSetOfflineTrue(Context.ConnectionId);
            string? removeUserGroupName = await redisCacheService.removeConnectionIdFromGroup(Context.ConnectionId);
            // if leaveUser is null and removeUserGroupName is null, meaning user quit application correctly.
            if (leaveUser != null && removeUserGroupName != null)
            {
                if(await redisCacheService.getGameInProgess(removeUserGroupName))
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

        // helper methods
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
                Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                if (nextPlayer != null)
                {
                    await Clients.Client(nextPlayer.connectionId).IdentityViewingStateFinish(removeUserGroupName, leaveUser.name);
                }
            }
            else if (currentGameStatus == "DiscussingState")
            {
                Users? currentDissingUser = await redisCacheService.getWhoIsCurrentlyDiscussing(removeUserGroupName);
                // if the current discussing user leaves, then skip put next user in discusstion.
                if (currentDissingUser != null && currentDissingUser.name == leaveUser.name)
                {
                    Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).DiscussingStateFinish(removeUserGroupName);
                    }
                }
            }
            else if (currentGameStatus == "VoteState")
            {
                Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                if (nextPlayer != null)
                {
                    await Clients.Client(nextPlayer.connectionId).VoteStateFinish(removeUserGroupName, leaveUser.name);
                }
            }
            else if (currentGameStatus == "PriestRoundState")
            {
                if (leaveUser.identity == Identities.Preist)
                {
                    Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).PriestRoundStateFinish(removeUserGroupName);
                    }
                }
            }
            else if (currentGameStatus == "JudasMeetWithPriestState")
            {
                if (leaveUser.identity == Identities.Judas)
                {
                    Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).JudasMeetWithPriestStateFinish(removeUserGroupName);
                    }
                }
            }
            else if (currentGameStatus == "NicodemusSavingRoundBeginState")
            {
                if (leaveUser.identity == Identities.Nicodemus)
                {
                    Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).NicodemusSavingRoundBeginStateFinish(removeUserGroupName);
                    }
                }
            }
            else if (currentGameStatus == "JohnFireRoundBeginState")
            {
                if (leaveUser.identity == Identities.John)
                {
                    Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).JohnFireRoundBeginStateFinish(removeUserGroupName);
                    }
                }
            }
            else if (currentGameStatus == "JudasCheckRoundState")
            {
                if (leaveUser.identity == Identities.John)
                {
                    Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).JudasCheckRoundStateFinish(removeUserGroupName);
                    }
                }
            }
            else if(currentGameStatus == "finishedToViewTheExileResultState")
            {
                Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                if (nextPlayer != null)
                {
                    await Clients.Client(nextPlayer.connectionId).finishedToViewTheExileResultStateFinish(removeUserGroupName, leaveUser.name);
                }
            }
            else if(currentGameStatus == "spiritualQuestionAnsweredCorrectOrNotState")
            {
                Users? currentDissingUser = await redisCacheService.getWhoIsCurrentlyDiscussing(removeUserGroupName);
                // if the current discussing user leaves, then skip put next user in discusstion.
                if (currentDissingUser != null && currentDissingUser.name == leaveUser.name)
                {
                    Users? nextPlayer = await redisCacheService.getFirstOnlineUser(removeUserGroupName);
                    if (nextPlayer != null)
                    {
                        await Clients.Client(nextPlayer.connectionId).spiritualQuestionAnsweredCorrectOrNotStateFinish(removeUserGroupName, leaveUser.name);
                    }
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
            if(reconnectUser != null)
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
                    }
                }
                // ROTSInfoRound
                if (reconnectUser.identity == Identities.Pharisee && !reconnectUser.disempowering)
                {
                    Users? lastExiledPlayer = await redisCacheService.getLastNightExiledPlayer(groupName);
                    if (lastExiledPlayer != null &&
                        (lastExiledPlayer.identity == Identities.John ||
                        lastExiledPlayer.identity == Identities.Peter ||
                        lastExiledPlayer.identity == Identities.Laity))
                    {
                        await Clients.Client(reconnectUser.connectionId).announceLastExiledPlayerInfo(true, lastExiledPlayer.name);
                    }
                }
                if (currentGameStatus == "IdentityViewingState")
                {
                    List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
                    if (didNotViewedUsers != null)
                    {
                        if (didNotViewedUsers.Any())
                        {
                            await Clients.Client(groupName).stillWaitingFor(didNotViewedUsers);
                            await Clients.Client(reconnectUser.connectionId).finishedViewIdentityAndWaitOnOtherPlayers(true);
                        }
                    }
                }
                // skip the IdentityViewingState, because user could view their identity later
                else if (currentGameStatus == "DiscussingState")
                {
                    await Clients.Client(reconnectUser.connectionId)
                        .nextStep(new NextStep("discussing", new List<string>() { await redisCacheService.getDiscussingTopic(groupName) }));
                    Users? currentDissingUser = await redisCacheService.getWhoIsCurrentlyDiscussing(groupName);
                    if (currentDissingUser != null)
                    {
                        await Clients.Client(reconnectUser.connectionId).currentUserInDiscusstion("", currentDissingUser.name);
                    }
                }
                // skip the VoteState, because user disconnect already vote himself.
                if(currentGameStatus == "VoteState")
                {
                    List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
                    if (didNotViewedUsers != null)
                    {
                        // didNotViewedUsers will not be reset, because currentGameStatus will change 
                        if (didNotViewedUsers.Any())
                        {
                            await Clients.Client(reconnectUser.connectionId).stillWaitingFor(didNotViewedUsers);
                        }  
                    }
                }
                if(currentGameStatus == "WinnerAfterVoteState" || currentGameStatus == "ROTSInfoRoundState")
                {
                    await Clients.Client(groupName).finishVoteWaitForOthersOrVoteResult(false, await redisCacheService.getVoteResult(groupName));
                    if(currentGameStatus == "WinnerAfterVoteState")
                    {
                        await Clients.Client(groupName).announceWinner(await redisCacheService.getWhoWins(groupName));
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
                else if (currentGameStatus == "finishedToViewTheExileResultState")
                {
                    List<Users>? didNotViewedUsers = await redisCacheService.doesAllPlayerViewedResult(groupName);
                    if (didNotViewedUsers != null)
                    {
                        // didNotViewedUsers will not be reset, because currentGameStatus will change 
                        if (didNotViewedUsers.Any())
                        {
                            await Clients.Client(reconnectUser.connectionId).stillWaitingFor(didNotViewedUsers);
                            await Clients.Client(reconnectUser.connectionId).openOrCloseExileResultModal(true);
                        }
                    }
                }
                else if(currentGameStatus == "WinnerState")
                {
                    await Clients.Client(groupName).announceWinner(await redisCacheService.getWhoWins(groupName));
                }
                // skip spiritualQuestionAnsweredCorrectOrNotState
            }
        }
    }
}
