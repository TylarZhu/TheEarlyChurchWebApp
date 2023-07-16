using Domain.APIClass;
using Domain.Common;
using Domain.DBEntities;
using Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver.Core.Connections;
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
                await Clients.Group(groupName).updateOnlineUserList(
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
        // This method will call when user close tab or refresh the page.
        // We assume that only one user will remain online 
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Users? leaveUser = await redisCacheService.removeUserFromGroupByConnectionId(Context.ConnectionId);
            string? removeUserGroupName = await redisCacheService.removeConnectionIdFromGroup(Context.ConnectionId);
            // if leaveUser is null and removeUserGroupName is null, meaning user quit application correctly.
            if (leaveUser != null && removeUserGroupName != null)
            {
                if(await redisCacheService.getGameInProgess(removeUserGroupName))
                {
                    await redisCacheService.addOfflineUser(removeUserGroupName, leaveUser);
                    List<Users>? offlineUsers = await redisCacheService.getListOfOfflineUsers(removeUserGroupName);
                    await announceOffLinePlayer(offlineUsers, removeUserGroupName);

                    string currentGameStatus = await redisCacheService.getCurrentGameStatus(removeUserGroupName);
                    if (leaveUser.inGame)
                    {
                        await gameOnStateReaction(currentGameStatus, removeUserGroupName, leaveUser);
                    }
                }
                else
                {
                    await Clients.Group(removeUserGroupName).updateOnlineUserList(
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
        private async Task gameOnStateReaction(string currentGameStatus, string removeUserGroupName, Users leaveUser)
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
                await redisCacheService.decreaseWaitingUsers(removeUserGroupName);
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
                await redisCacheService.decreaseWaitingUsers(removeUserGroupName);
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
                await redisCacheService.decreaseWaitingUsers(removeUserGroupName);
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
                await redisCacheService.decreaseWaitingUsers(removeUserGroupName);
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
                await redisCacheService.decreaseWaitingUsers(removeUserGroupName);
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
                await redisCacheService.decreaseWaitingUsers(removeUserGroupName);
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
                await redisCacheService.decreaseWaitingUsers(removeUserGroupName);
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
    }
}
