using Domain.Common;
using Domain.DBEntities;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Bson.Serialization.IdGenerators;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace Infrastructure.DistributedCacheService
{
    public class DistributedCacheService: ICacheService
    {
        private readonly IDistributedCache distributedCache;
        private readonly ConcurrentDictionary<string, bool> groupNames = new ConcurrentDictionary<string, bool>();
        public DistributedCacheService(IDistributedCache distributedCache)
        {
            this.distributedCache = distributedCache;
        }

        // Group and User
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
            string saveData = JsonConvert.SerializeObject(value);
            await distributedCache.SetStringAsync(groupName, saveData, cancellationToken);
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
                        UpdateHelper.TryUpdateCustom(newUserInGroup.onlineUsers, firstUser.name,
                            x =>
                            {
                                x.groupLeader = true;
                                return x;
                            });
                        await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(newUserInGroup));
                        /*firstUser.groupLeader = true;
                        if (newUserInGroup.onlineUsers.TryGetValue(firstUser.name, out OnlineUsers? originalVal))
                        {
                            newUserInGroup.onlineUsers.TryUpdate(name, firstUser, originalVal);

                            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(newUserInGroup));
                        }*/
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
        public async Task<OnlineUsers?> getOneUserFromGroup(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group == null)
            {
                return null;
            }
            if(group.onlineUsers.TryGetValue(name, out OnlineUsers? users)){
                return users;
            }
            return null;
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
        public async Task<string> getMaxPlayersInGroup(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group == null)
            {
                return "";
            }
            return group.maxPlayers.ToString();
        }


        // Game
        public async Task<bool> createAGameAndAssignIdentities(string groupName, int christans, int judaisms)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group == null)
            {
                return false;
            }
            group.numOfChristans = christans;
            group.numOfJudaisms = judaisms;
            List<Identities>? identityCards = group.issuedIdentityCards();
            if (identityCards != null) 
            {
                int i = 0;
                foreach (string key in group.onlineUsers.Keys)
                {
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, key,
                        x => {
                            x.identity = identityCards[i];
                            x.assignOriginalVote();
                            
                            group.totalVotes += x.originalVote;
                            if (x.identity == Identities.Judaism ||
                                x.identity == Identities.Judas ||
                                x.identity == Identities.Pharisee ||
                                x.identity == Identities.Scribes)
                            {
                                group.judaismTotalVote += x.originalVote;
                            }
                            return x;
                        });
                    i++;
                }

                
            }
            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            return true;
        }
        public async Task<bool> waitOnOtherPlayersActionInGroup(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                if(group.numberofWaitingUser.TryPeek(out int currentWaitingUser))
                {
                    if(currentWaitingUser <= 1)
                    {
                        group.numberofWaitingUser.Clear();

                        /*group.numberofWaitingUser.Add(group.maxPlayers);*/
                        group.numberofWaitingUser.Add(3);

                        await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                        return false;
                    }
                    else
                    {
                        group.numberofWaitingUser.TryTake(out int _);
                        group.numberofWaitingUser.Add(-- currentWaitingUser);
                        await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    }
                }
            }
            return true;
        }

        public async Task<string?> whoIsDiscussingNext(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                List<string> keys = group.onlineUsers.Keys.ToList();
                // meaning the disscussion just started, then return the person who is in the first in array will disscuss.
                if (name == "none")
                {
                    if(group.onlineUsers.TryGetValue(keys[0], out OnlineUsers? user))
                    {
                        return user.connectionId;
                    }
                }
                // choose the next person to disscuss
                for (int i = 0; i < keys.Count; i ++)
                {
                    if (keys[i] == name && i + 1 < keys.Count)
                    {
                        if(group.onlineUsers.TryGetValue(keys[i + 1], out OnlineUsers? user))
                        {
                            return user.connectionId;
                        }
                    }
                    // if all users finished talking, then return null
                }
            }
            return null;
        }
    }
}
