using Domain.Common;
using Domain.DBEntities;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Bson.Serialization.IdGenerators;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Security.Principal;
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
                        
                        OnlineUsers? firstUser = newUserInGroup.onlineUsers.Values.FirstOrDefault();
                        if (firstUser != null)
                        {
                            UpdateHelper.TryUpdateCustom(newUserInGroup.onlineUsers, firstUser.name,
                                x =>
                                {
                                    x.groupLeader = true;
                                    return x;
                                });
                            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(newUserInGroup));
                            return removedUser;
                        }
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
            return group.onlineUsers.Values.FirstOrDefault(x => x.inGame);
        }
        public void AddNewMessageIntoGroup(GamesGroupsUsersMessages group, string message)
        {
            if(group.history.message.ContainsKey(group.day))
            {
                group.history.message[group.day].Add(message);
            }
            else
            {
                group.history.message[group.day] = new List<string> { message };
            }
        }
        public async Task AddNewMessageIntoGroupAndSave(string groupName, string message)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                AddNewMessageIntoGroup(group, message);
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<Dictionary<int, List<string>>?> getAllMessagesInGroup(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group == null)
            {
                return null;
            }
            return group.history.message;
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
        public async Task<OnlineUsers?> getSpecificUserFromGroupByIdentity(string groupName, Identities identity)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                ICollection<OnlineUsers> users = group.onlineUsers.Values;
                var a = from user in users
                        where user.identity == identity
                        select user;
                return a.FirstOrDefault();
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
        public async Task updateListAsync(string groupName, ConcurrentDictionary<string, double> newVoteList)
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
        public async Task<bool> createAGameAndAssignIdentities(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group == null)
            {
                return false;
            }
            List<Identities>? identityCards = group.issuedIdentityCards();
            if (identityCards != null) 
            {
                int i = 0;
                foreach (string key in group.onlineUsers.Keys)
                {
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, key,
                        x => {
                            x.identity = identityCards[i];
                            x.assignOriginalVoteAndAbility();
                            
                            group.totalVotes += x.originalVote;
                            if (x.identity == Identities.Judaism ||
                                x.identity == Identities.Judas ||
                                x.identity == Identities.Pharisee ||
                                x.identity == Identities.Preist)
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
                if(group.numberofWaitingUser.TryGetValue("WaitingUsers", out int currentWaitingUser))
                {
                    if(currentWaitingUser <= 1)
                    {
                        List<OnlineUsers> NotInGameUser = await collectAllExiledUserName(groupName);
                        UpdateHelper.TryUpdateCustom(group.numberofWaitingUser, "WaitingUsers",
                            x => {
                                x = group.maxPlayers - NotInGameUser.Count;
                                return x;
                           });
                        await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                        return false;
                    }
                    else
                    {
                        UpdateHelper.TryUpdateCustom(group.numberofWaitingUser, "WaitingUsers",
                        x => {
                            x--;
                            return x;
                        });
                        await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    }
                }
            }
            return true;
        }
        public async Task<OnlineUsers?> whoIsDiscussingNext(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                OnlineUsers? nextUser = group.onlineUsers.Values.FirstOrDefault(x => x.inGame && !x.disscussed);
                if(nextUser != null)
                {
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, nextUser.name,
                        x => {
                            x.disscussed = true;
                            return x;
                        });
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    return nextUser;
                }
                // everyone finished discuss, reset disscussed status.
                else
                {
                    foreach(KeyValuePair<string, OnlineUsers> user in group.onlineUsers)
                    {
                        UpdateHelper.TryUpdateCustom(group.onlineUsers, user.Value.name,
                        x => {
                            x.disscussed = false;
                            return x;
                        });
                    }
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    return null;
                }
            }
            // if all users finished talking, then return null
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
        public async Task<int> votePerson(string groupName, string votePerson, string fromWho, bool everyoneFinishVoting)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            ConcurrentDictionary<string, double>? voteList = await getVoteList(groupName);
            if (group != null && voteList != null)
            {
                updateVote(group, votePerson, fromWho, voteList);
                if (everyoneFinishVoting)
                {
                    await updateListAsync(groupName, voteList);
                }
                else
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
                        AddNewMessageIntoGroup(group, equalVoteMessage);
                        returnType = 3;
                    }
                    // someone lost vote weight
                    else if (group.onlineUsers.TryGetValue(voteHighestPerson.Item1, out OnlineUsers? users) && users != null)
                    {
                        // update lost vote
                        returnType = addLostVote(group, users);

                        AddNewMessageIntoGroup(group, $"{users.name} lost all his/her vote weight and his identity is {users.identity}!");
                        UpdateHelper.TryUpdateCustom(group.onlineUsers, voteHighestPerson.Item1,
                        x =>
                        {
                            x.changedVote = 0;
                            x.disempowering = true;
                            return x;
                        });

                    }
                    voteList.Clear();
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    await updateListAsync(groupName, voteList);
                    return returnType;
                }
            }
            return -1;
        }
        public async Task<int> whoWins(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                return group.winCondition();
            }
            return -1;
        }
        /// <summary>
        /// set a player's exile status
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="exileStat"></param>
        /// <param name="exileName">if it is empty string, meaning Nicodemus is using his ability to save whoever's aboutToExile is true.</param>
        /// <returns> return false, if priest try to exile Nicodemus or cannot get group from Redis.</returns>
        public async Task<bool> setExile(string groupName, bool exileStat, string exileName = "")
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                // only Nico choice to save so exileName will be empty
                if(string.IsNullOrEmpty(exileName))
                {
                    OnlineUsers? user = group.onlineUsers.Values.FirstOrDefault(x => x.aboutToExile);
                    if (user != null)
                    {
                        // it is the opposite
                        if (exileStat)
                        {
                            AddNewMessageIntoGroup(group, $"Nicodemus choice not to save {user.name}!");
                        }
                        else
                        {
                            AddNewMessageIntoGroup(group, $"Nicodemus choice to save {user.name}!");
                        }
                        UpdateHelper.TryUpdateCustom(group.onlineUsers, user.name,
                            x =>
                            {
                                x.aboutToExile = exileStat;
                                return x;
                            });
                    }
                }
                else
                {
                    OnlineUsers? user = await getOneUserFromGroup(groupName, exileName);
                    if (user != null)
                    {
                        // Nicodemus cannot be exiled.
                        if(user.identity != Identities.Nicodemus)
                        {
                            AddNewMessageIntoGroup(group, $"Priest try to exile {user.name} and his/her identity is {user.identity}!");
                            UpdateHelper.TryUpdateCustom(group.onlineUsers, exileName,
                                x =>
                                {
                                    x.aboutToExile = exileStat;
                                    return x;
                                });
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                return true;
            }
            return false;
        }
        public async Task<int> increaseDay(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                group.day++;
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                return group.day;
            }
            return -1;
        }
        
        public async Task changeVote(string groupName, string name = "", Identities? identities = null, double changedVote = 0.0, string option = "")
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                string changeVoteName = "";
                OnlineUsers? user;
                if (!string.IsNullOrEmpty(name))
                {
                    changeVoteName = name;
                }
                else
                {
                    user = await getSpecificUserFromGroupByIdentity(groupName, identities!.Value);
                    if(user != null)
                    {
                        changeVoteName = user.name;
                    }
                }
                if (changeVoteName != null)
                {
                    if (option == "setZero")
                    {
                        UpdateHelper.TryUpdateCustom(group.onlineUsers, changeVoteName,
                            x => {
                                x.changedVote = 0;
                                return x;
                            });
                    }
                    else if (option == "half")
                    {
                        UpdateHelper.TryUpdateCustom(group.onlineUsers, changeVoteName,
                            x => {
                                x.changedVote /= 2;
                                return x;
                            });
                    }
                    else if(option == "add")
                    {
                        UpdateHelper.TryUpdateCustom(group.onlineUsers, changeVoteName,
                            x => {
                                x.changedVote += changedVote;
                                return x;
                            });
                    } 
                    else 
                    {
                   
                        UpdateHelper.TryUpdateCustom(group.onlineUsers, changeVoteName,
                            x => {
                                x.changedVote = changedVote;
                                return x;
                            });
                    }
                }
            }
            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
        }
        public async Task NicodemusSetProtection(string groupName, bool protectionStatus)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            OnlineUsers? user = await getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);
            if (user != null && group != null)
            {
                UpdateHelper.TryUpdateCustom(group.onlineUsers, user.name,
                    x => {
                        x.nicodemusProtection = protectionStatus;
                        return x;
                    });
            }
            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
        }
        public async Task<List<string>?> GetJohnCannotFireList(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                return group.JohnFireList;
            }
            return null;
        }
        public async Task<bool> checkJohnFireAllOrNot(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                // plus one because we need to cound John himself
                return group.JohnFireList.Count + 1 >= group.onlineUsers.Count;
            }
            return false;
        }
        public async Task AddToJohnFireList(string groupName, string fireName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                group.JohnFireList.Add(fireName);
                AddNewMessageIntoGroup(group, $"John fire {fireName}!");
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<bool> JudasCheck(string groupName, string checkName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            bool flag = false;
            if (group != null)
            {
                AddNewMessageIntoGroup(group, $"Judas check {checkName}!");
                if (group.onlineUsers.TryGetValue(checkName, out OnlineUsers? checkUser))
                {
                    if(checkUser != null)
                    {
                        if (checkUser.identity == Identities.John ||
                            checkUser.identity == Identities.Peter ||
                            checkUser.identity == Identities.Laity ||
                            checkUser.identity == Identities.Nicodemus)
                        {
                            flag = true;
                        }
                    }
                }
            }
            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            return flag;
        }
        public async Task<OnlineUsers?> checkAndSetIfAnyoneOutOfGame(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                OnlineUsers? user = group.onlineUsers.Values.FirstOrDefault(x => x.aboutToExile);
                if(user != null)
                {
                    if(user.identity == Identities.Peter)
                    {
                        OnlineUsers? John = await getSpecificUserFromGroupByIdentity(groupName, Identities.John);
                        if(John != null && John.inGame && John.johnProtection)
                        {
                            UpdateHelper.TryUpdateCustom(group.onlineUsers, John.name,
                                x => {
                                    x.johnProtection = false;
                                    return x;
                                });
                            UpdateHelper.TryUpdateCustom(group.onlineUsers, user.name,
                                x => {
                                    x.aboutToExile = false;
                                    return x;
                                });
                            group.lastNightExiledPlayer = null;
                            AddNewMessageIntoGroup(group, "John protect Peter! Peter did not exiled.");
                            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                            return null;
                        }
                    }
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, user.name,
                        x => {
                            x.aboutToExile = false;
                            x.inGame = false;
                            return x;
                        });
                    addLostVote(group, user);
                    group.lastNightExiledPlayer = user;
                    UpdateHelper.TryUpdateCustom(group.numberofWaitingUser, "WaitingUsers",
                        x => {
                            x--;
                            return x;
                        });
                    AddNewMessageIntoGroup(group, $"{user.name} has been exiled! His/her identity is {user.identity}!");
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    return user;
                }
                group.lastNightExiledPlayer = null;
                AddNewMessageIntoGroup(group, $"No one has been exiled!");
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
            return null;
        }
        public async Task PeterIncreaseVoteWeightByOneOrNot(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null && group.day == 3)
            {
                OnlineUsers? Peter = await getSpecificUserFromGroupByIdentity(groupName, Identities.Peter);
                if(Peter != null && Peter.inGame)
                {
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, Peter.name,
                        x =>
                        {
                            x.changedVote += 1;
                            return x;
                        });
                    AddNewMessageIntoGroup(group, "It is day 3, Peter's vote weight increase by 1!");
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                }
            }
        }
        public async Task<List<OnlineUsers>> collectAllExiledUserName(string groupName)
        {
            List<OnlineUsers> usersList = new List<OnlineUsers>();
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                foreach(KeyValuePair<string, OnlineUsers> user in group.onlineUsers)
                {
                    if(!user.Value.inGame)
                    {
                        usersList.Add(user.Value);
                    }
                }
            }
            return usersList;
        }
        public async Task<int> getDay(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.day;
            }
            return -1;
        }
        public async Task<OnlineUsers?> getLastNightExiledPlayer(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.lastNightExiledPlayer;
            }
            return null;
        }
        public async Task<Dictionary<int, List<string>>?> getGameHistory(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.history.message;
            }
            return null;
        }
        public async Task cleanUp(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                foreach(KeyValuePair<string, OnlineUsers> user in group.onlineUsers)
                {
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, user.Value.name, 
                        x => {
                            x.identity = Identities.NullState;
                            x.originalVote = 0.0;
                            x.changedVote = 0.0;
                            x.johnProtection = false;
                            x.nicodemusProtection = false;
                            x.judasCheck = false;
                            x.inGame = true;
                            x.disscussed = false;
                            x.disempowering = false;
                            x.aboutToExile = false;
                            return x;
                        });
                }
                group.cleanUp();
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }

        //private methods
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
        private int addLostVote(GamesGroupsUsersMessages group, OnlineUsers users)
        {
            int returnType;
            // update lost vote
            if (users.identity == Identities.Laity ||
                users.identity == Identities.Nicodemus ||
                users.identity == Identities.John ||
                users.identity == Identities.Peter)
            {
                if(!users.disempowering)
                {
                    group.christianLostVote += users.originalVote;
                }
                returnType = 1;
            }
            else
            {
                if(!users.disempowering)
                {
                    group.judaismLostVote += users.originalVote;
                }
                returnType = 2;
            }

            return returnType;
        }
    }
}
