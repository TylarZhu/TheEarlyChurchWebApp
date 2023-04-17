using Domain.Common;
using Domain.DBEntities;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace Infrastructure.DistributedCacheService
{
    public class DistributedCacheService: ICacheService
    {
        private readonly IDistributedCache distributedCache;
        private static readonly ConcurrentDictionary<string, bool> groupNames = new ConcurrentDictionary<string, bool>();
        public DistributedCacheService(IDistributedCache distributedCache)
        {
            this.distributedCache = distributedCache;
        }
        public async Task<GamesGroupsUsersMessages?> GetGroupAsync(string groupName, CancellationToken cancellationToken = default)
        {
            string? value = await distributedCache.GetStringAsync(groupName, cancellationToken);
            if(string.IsNullOrEmpty(value)) 
            {
                return null!;
            }
            GamesGroupsUsersMessages? convert = JsonConvert.DeserializeObject<GamesGroupsUsersMessages>(value);
            return convert!;
        }
        public async Task RemoveAsync(string groupName, CancellationToken cancellationToken = default)
        {
            await distributedCache.RemoveAsync(groupName, cancellationToken);
            groupNames.TryRemove(groupName, out var value);
        }
        public async Task RemoveByPrefixAsync(string prefixKey, CancellationToken cancellationToken = default)
        {
            IEnumerable<Task> tasks = groupNames.Keys.Where(x => x.StartsWith(prefixKey)).Select(x => RemoveAsync(x, cancellationToken));
            await Task.WhenAll(tasks);
        }
        public async Task SetNewGroupAsync(string groupName, GamesGroupsUsersMessages value, OnlineUsers groupLeader, CancellationToken cancellationToken = default)
        {
            value.onlineUsers.TryAdd(groupLeader.name, groupLeader);
            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(value), cancellationToken);
            groupNames.TryAdd(groupName, true);
        }
        public async Task AddNewUsersToGroupAsync(string groupName, OnlineUsers user, CancellationToken cancellationToken = default)
        {
            GamesGroupsUsersMessages? newUserInGroup = await GetGroupAsync(groupName);
            if(newUserInGroup != null)
            {
                newUserInGroup.onlineUsers.TryAdd(user.name, user);
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(newUserInGroup), cancellationToken);
            }
        }
        public bool CheckIfGroupExsits(string groupName)
        {
            return groupNames.TryGetValue(groupName, out bool value);
        }
        public async Task<OnlineUsers?> removeUserAndAssignNextUserAsGroupLeader(string groupName, string name)
        {
            GamesGroupsUsersMessages? newUserInGroup = await GetGroupAsync(groupName);
            if(newUserInGroup != null)
            {
                if (newUserInGroup.onlineUsers.TryRemove(name, out OnlineUsers? removedUser))
                {
                    if (newUserInGroup.onlineUsers.Count == 0)
                    {
                        await RemoveAsync(groupName);
                        return removedUser;
                    }
                    else
                    {
                        OnlineUsers firstUser = newUserInGroup.onlineUsers.First().Value;
                        firstUser.groupLeader = true;
                        if (newUserInGroup.onlineUsers.TryGetValue(firstUser.name, out OnlineUsers? originalVal))
                        {
                            newUserInGroup.onlineUsers.TryUpdate(name, firstUser, originalVal);
                           /* UpdateHelper.TryUpdateCustom(newUserInGroup.onlineUsers, name, 
                                x =>
                                {
                                    x.groupLeader = true;
                                    return x;
                                });*/
                            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(newUserInGroup));
                        }
                        return removedUser;
                    }

                }
            }
            
            return null;
        }
        public async Task<OnlineUsers?> assignUserAsGroupLeader(string groupName, string nextGroupLeader, string originalGroupLeader)
        {
            GamesGroupsUsersMessages? newUserInGroup = await GetGroupAsync(groupName);
            if(newUserInGroup != null)
            {
                /*if(newUserInGroup.onlineUsers.TryGetValue(name, out OnlineUsers? originalVal))
                {
                    OnlineUsers? nextGroupLeader = originalVal;
                    nextGroupLeader.groupLeader = true;
                    newUserInGroup.onlineUsers.TryUpdate(name, nextGroupLeader, originalVal);

                    return nextGroupLeader;
                }*/
                UpdateHelper.TryUpdateCustom(newUserInGroup.onlineUsers, originalGroupLeader,
                    x => {
                        x.groupLeader = false;
                        return x;
                    });
                UpdateHelper.TryUpdateCustom(newUserInGroup.onlineUsers, nextGroupLeader,
                    x => {
                        x.groupLeader = true;
                        return x;
                    });
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(newUserInGroup));
                if(newUserInGroup.onlineUsers.TryGetValue(nextGroupLeader, out OnlineUsers? val))
                {
                    return val;
                }
            }
            return null;
        }
        public async Task<List<OnlineUsers>> getAllUsers(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group == null)
            {
                return new List<OnlineUsers> { };
            }
            return group.onlineUsers.Values.ToList();
        }
        public async Task AddNewMessageIntoGroup(string groupName, Message message)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null && group.messages.TryAdd(groupName + group.messages.Count, message))
            {
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<List<Message>> getAllMessagesInGroup(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group == null)
            {
                return new List<Message> { };
            }
            return group.messages.Values.ToList();
        }
        public async Task<OnlineUsers?> getGroupLeaderFromGroup(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group == null)
            {
                return null;
            }
            return group.onlineUsers.FirstOrDefault(x => x.Value.groupLeader).Value;
        }
        public async Task<bool> checkIfUserNameInGroupDuplicate(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group == null)
            {
                return false;
            }
            return group.onlineUsers.Any(x=> x.Value.name == name);
        }
        public async Task<bool> checkIfGroupIsFull(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group == null)
            {
                return false;
            }
            return group.onlineUsers.Count >= group.maxPlayers;
        }
        public async Task<bool> isGroupEmpty(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group == null)
            {
                return true;
            }
            return group.onlineUsers.Count == 0;
        }
        /*Task addNewGroupAndFirstUser(GroupsUsersAndMessages groupsUsersAndMessages);
        Task<GroupsUsersAndMessages> getOneGroup(string groupName);
        Task<List<OnlineUsers>> getUsersFromOneGroup(string groupName);
        Task<List<Message>> getMessagesFromOneGroup(string groupName);
        Task<bool> isGroupEmpty(string groupName);
        Task<bool> checkIfUserNameInGroupDuplicate(string groupName, string name);
        Task<bool> checkIfGroupIsFull(string groupName);
        Task<OnlineUsers> getOneUserFromSpecificGroup(string groupName, string? name = "", string? connectionId = "");
        Task deleteOneUserFromGroup(string groupName, string name);
        Task addNewMessageIntoGroup(string groupName, Message newMessage);
        Task addNewUserIntoGroup(string groupName, OnlineUsers onlineUsers);
        Task<OnlineUsers> getGroupLeaderFromSpecificGroup(string groupName);
        Task nextFirstUserAssignAsGroupLeader(string groupName);*/

        
    }
}
