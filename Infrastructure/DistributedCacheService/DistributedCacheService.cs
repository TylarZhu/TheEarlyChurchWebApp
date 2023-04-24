using Domain.Common;
using Domain.DBEntities;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Bson.Serialization.IdGenerators;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Threading;

namespace Infrastructure.DistributedCacheService
{
    public class DistributedCacheService: ICacheService
    {
        private readonly IDistributedCache distributedCache;
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
            return convert;
        }
        public async Task RemoveGroupAsync(string groupName, CancellationToken cancellationToken = default)
        {
            await distributedCache.RemoveAsync(groupName, cancellationToken);
        }
        public async Task SetNewGroupAsync(string groupName, GamesGroupsUsersMessages value, OnlineUsers groupLeader, CancellationToken cancellationToken = default)
        {
            value.onlineUsers.TryAdd(groupLeader.name, groupLeader);
            string saveData = JsonConvert.SerializeObject(value);
            await distributedCache.SetStringAsync(groupName, saveData, cancellationToken);
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
        public async Task<OnlineUsers?> removeUserAndAssignNextUserAsGroupLeader(string groupName, string name)
        {
            GamesGroupsUsersMessages? newUserInGroup = await GetGroupAsync(groupName);
            if(newUserInGroup != null)
            {
                if (newUserInGroup.onlineUsers.TryRemove(name, out OnlineUsers? removedUser))
                {
                    if (newUserInGroup.onlineUsers.Count == 0)
                    {
                        await RemoveGroupAsync(groupName);
                        await removeVoteListAsync(groupName);
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
        public async Task<OnlineUsers?> getFirstUser(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group == null)
            {
                return null;
            }
            return group.onlineUsers.Values.FirstOrDefault();
        }
        public async Task AddNewMessageIntoGroup(string groupName, Message message)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                group.messages.Add(message);
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
            return group.messages;
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


        // Vote List
        public async Task setNewVoteListAsync(string groupName, ConcurrentDictionary<string, double> newVoteList)
        {
            string saveData = JsonConvert.SerializeObject(newVoteList);
            await distributedCache.SetStringAsync(groupName + "VoteList", saveData);
        }
        public async Task<ConcurrentDictionary<string, double>?> getVoteList(string groupName)
        {
            string? value = await distributedCache.GetStringAsync(groupName + "VoteList");
            if (string.IsNullOrEmpty(value))
            {
                return null!;
            }
            ConcurrentDictionary<string, double>? convert = JsonConvert.DeserializeObject<ConcurrentDictionary<string, double>>(value);
            return convert;
        }
        public async Task removeVoteListAsync(string groupName)
        {
            await distributedCache.RemoveAsync(groupName + "VoteList");
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
                        group.numberofWaitingUser.Add(group.maxPlayers);
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="votePerson"></param>
        /// <param name="fromWho"></param>
        /// <returns> 
        ///     return true, meanning all user finish voting.
        ///     return false, meaning stil user did not finish voting.
        ///     return 0, meaning not all user finish voting or an error occur.
        ///     return 1, meaning a christian is vote out.
        ///     return 2, meaning a judasim is vote out.
        ///     return 3, meaning there is a tie, no one is vote out.
        /// </returns>
        public async Task<(bool, int)> votePerson(string groupName, string votePerson, string fromWho)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            ConcurrentDictionary<string, double>? voteList = await getVoteList(groupName);
            if (group != null && voteList != null)
            {
                updateVote(group, votePerson, fromWho, voteList);
                if (group.numberofWaitingUser.TryPeek(out int currentWaitingUser))
                {
                    // everyone finished voting 
                    if (currentWaitingUser <= 1)
                    {
                        (string, double) voteHighestPerson = ("", 0.0);
                        List<string> equalVotePersonName = new List<string>();
                        int counter = 1;
                        int returnType = 0;
                        List<string> keys = voteList.Keys.ToList();


                        foreach (string key in voteList.Keys)
                        {
                            if (voteList.TryGetValue(key, out double vote))
                            {
                                if (vote > voteHighestPerson.Item2)
                                {
                                    voteHighestPerson.Item1 = key;
                                    voteHighestPerson.Item2 = vote;
                                    counter = 1;
                                    equalVotePersonName.Clear();
                                    equalVotePersonName.Add(key);
                                }
                                else if (vote == voteHighestPerson.Item2)
                                {
                                    counter++;
                                    equalVotePersonName.Add(key);
                                }
                            }
                        }
                        // someone has equal vote, then no one gets vote out.
                        if (counter > 1)
                        {
                            string equalVoteMessage = "";
                            foreach (string name in equalVotePersonName)
                            {
                                equalVoteMessage = equalVoteMessage + name + " ";
                            }
                            equalVoteMessage += "have same amount of vote!";
                            group.messages.Add(new Message("game", equalVoteMessage));
                            returnType = 3;
                        }
                        // someone lost vote weight
                        else
                        {
                            // update lost vote
                            if (group.onlineUsers.TryGetValue(voteHighestPerson.Item1, out OnlineUsers? users) && !users.disempowering)
                            {
                                if (users.identity == Identities.Laity ||
                                    users.identity == Identities.Nicodemus ||
                                    users.identity == Identities.John ||
                                    users.identity == Identities.Peter)
                                {
                                    group.christianLostVote += users.originalVote;
                                    returnType = 1;
                                }
                                else
                                {
                                    group.judaismLostVote += users.originalVote;
                                    returnType = 2;
                                }
                                group.messages.Add(new Message("game", $"{users.name} has been vote out on Day {group.day}!"));
                            }
                            UpdateHelper.TryUpdateCustom(group.onlineUsers, voteHighestPerson.Item1, 
                                x => {
                                    x.changedVote = 0;
                                    x.disempowering = true;
                                    return x;
                                });
                        }
                        group.numberofWaitingUser.Clear();
                        voteList.Clear();
                        group.numberofWaitingUser.Add(group.maxPlayers);
                        await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                        await setNewVoteListAsync(groupName, voteList);
                        return (true, returnType);
                    }
                    else
                    {
                        group.numberofWaitingUser.TryTake(out int _);
                        group.numberofWaitingUser.Add(--currentWaitingUser);
                        await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                        await setNewVoteListAsync(groupName, voteList);
                    }
                }
            }
            return (false, 0);
        }
        private void updateVote(GamesGroupsUsersMessages group, string votePerson, string fromWho,
            ConcurrentDictionary<string, double> voteList)
        {
            if (group.onlineUsers.TryGetValue(fromWho, out OnlineUsers? fromWhoUser) && fromWhoUser.inGame)
            {
                if (voteList.ContainsKey(votePerson))
                {
                    UpdateHelper.TryUpdateCustom(voteList, votePerson, x =>
                    {
                        x += fromWhoUser.changedVote;
                        return x;
                    });
                }
                else
                {
                    voteList.TryAdd(votePerson, fromWhoUser.changedVote);
                }
            }
        }
    }
}
