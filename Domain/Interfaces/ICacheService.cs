using Domain.Common;
using Domain.DBEntities;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace Domain.Interfaces
{
    public interface ICacheService
    {
        // Group and Users (get)
        Task<GamesGroupsUsersMessages?> GetGroupAsync(string groupName, CancellationToken cancellationToken = default);
        Task<List<OnlineUsers>> getAllUsers(string groupName);
        Task<List<Message>> getAllMessagesInGroup(string groupName);
        Task<OnlineUsers?> getGroupLeaderFromGroup(string groupName);
        Task<OnlineUsers?> getOneUserFromGroup(string groupName, string name);
        Task<string> getMaxPlayersInGroup(string groupName);


        // Group and Users (set)
        Task SetNewGroupAsync(string groupName, GamesGroupsUsersMessages value, OnlineUsers groupLeader, CancellationToken cancellationToken = default);
        Task<OnlineUsers?> assignUserAsGroupLeader(string groupName, string nextGroupLeader, string originalGroupLeader);
        // Group and Users (remove and set)
        Task<OnlineUsers?> removeUserAndAssignNextUserAsGroupLeader(string groupName, string name);


        // Group and Users (remove)
        Task RemoveAsync(string groupName, CancellationToken cancellationToken = default);
        Task RemoveByPrefixAsync(string prefixKey, CancellationToken cancellationToken = default);


        // Group and Users (add)
        Task AddNewUsersToGroupAsync(string groupName, OnlineUsers user, CancellationToken cancellationToken = default);
        Task AddNewMessageIntoGroup(string groupName, Message message);


        // Group and Users (check)
        bool CheckIfGroupExsits(string groupName);
        Task<bool> checkIfUserNameInGroupDuplicate(string groupName, string name);
        Task<bool> checkIfGroupIsFull(string groupName);
        Task<bool> isGroupEmpty(string groupName);
        
        

        // Game
        Task<bool> createAGameAndAssignIdentities(string groupName, int christans, int judaisms);
        Task<bool> waitOnOtherPlayersActionInGroup(string groupName);
    }
}
