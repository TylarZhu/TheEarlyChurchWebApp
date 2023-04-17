using Domain.Common;
using Domain.DBEntities;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace Domain.Interfaces
{
    public interface ICacheService
    {
        Task<GamesGroupsUsersMessages?> GetGroupAsync(string groupName, CancellationToken cancellationToken = default);
        Task SetNewGroupAsync(string groupName, GamesGroupsUsersMessages value, OnlineUsers groupLeader, CancellationToken cancellationToken = default);
        Task AddNewUsersToGroupAsync(string groupName, OnlineUsers user, CancellationToken cancellationToken = default);
        Task RemoveAsync(string groupName, CancellationToken cancellationToken = default);
        Task RemoveByPrefixAsync(string prefixKey, CancellationToken cancellationToken = default);
        Task<OnlineUsers?> removeUserAndAssignNextUserAsGroupLeader(string groupName, string name);
        Task<List<OnlineUsers>> getAllUsers(string groupName);
        Task AddNewMessageIntoGroup(string groupName, Message message);
        Task<List<Message>> getAllMessagesInGroup(string groupName);
        bool CheckIfGroupExsits(string groupName);
        Task<OnlineUsers?> getGroupLeaderFromGroup(string groupName);
        Task<bool> checkIfUserNameInGroupDuplicate(string groupName, string name);
        Task<bool> checkIfGroupIsFull(string groupName);
        Task<bool> isGroupEmpty(string groupName);
        Task<OnlineUsers?> assignUserAsGroupLeader(string groupName, string nextGroupLeader, string originalGroupLeader);
    }
}
