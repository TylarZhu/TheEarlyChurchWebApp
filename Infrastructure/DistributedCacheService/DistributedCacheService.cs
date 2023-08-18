using Domain.Common;
using Domain.DBEntities;
using Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using System.Xml.Linq;

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
                return null;
            }
            GamesGroupsUsersMessages? convert = JsonConvert.DeserializeObject<GamesGroupsUsersMessages>(value);
            return convert;
        }
        public async Task RemoveGroupAsync(string groupName, CancellationToken cancellationToken = default)
        {
            await distributedCache.RemoveAsync(groupName, cancellationToken);
        }
        public async Task SetNewGroupAsync(string groupName, GamesGroupsUsersMessages value, Users groupLeader, CancellationToken cancellationToken = default)
        {
            value.onlineUsers.TryAdd(groupLeader.name, groupLeader);
            string saveData = JsonConvert.SerializeObject(value);
            await distributedCache.SetStringAsync(groupName, saveData, cancellationToken);
        }
        public async Task AddNewUsersToGroupAsync(string groupName, Users user, CancellationToken cancellationToken = default)
        {
            GamesGroupsUsersMessages? newUserInGroup = await GetGroupAsync(groupName);
            if(newUserInGroup != null)
            {
                newUserInGroup.onlineUsers.TryAdd(user.name, user);
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(newUserInGroup), cancellationToken);
            }
        }
        public async Task<Users?> removeUserFromGroup(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                if (group.onlineUsers.TryRemove(name, out Users? removedUser))
                {
                    if (group.onlineUsers.Count == 0)
                    {
                        await RemoveGroupAsync(groupName);
                        await removeVoteListAsync(groupName);
                        return removedUser;
                    }
                    else
                    {
                        // if the removed user is group leader, then assign a new group leader.
                        if (removedUser.groupLeader)
                        {
                            Users? firstUser = group.onlineUsers.Values.FirstOrDefault();
                            if (firstUser != null)
                            {
                                UpdateHelper.TryUpdateCustom(group.onlineUsers, firstUser.name,
                                    x =>
                                    {
                                        x.groupLeader = true;
                                        return x;
                                    });
                            }
                        }
                        await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                        return removedUser;
                    }
                }
            }
            return null;
        }
        public async Task<bool> getGameInProgess(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.gameInProgess;
            }
            return false;
        }
        public async Task<Users?> assignUserAsGroupLeader(string groupName, string nextGroupLeader, string originalGroupLeader)
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
                if(newUserInGroup.onlineUsers.TryGetValue(nextGroupLeader, out Users? val))
                {
                    return val;
                }
            }
            return null;
        }
        public async Task<List<Users>> getAllUsers(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group == null)
            {
                return new List<Users> { };
            }
            return group.onlineUsers.Values.ToList();
        }
        public async Task<Users?> getFirstOnlineUser(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group == null)
            {
                return null;
            }
            return group.onlineUsers.Values.FirstOrDefault(x => !x.offLine);
        }
        public void AddNewMessageIntoGroup(GamesGroupsUsersMessages group, string message)
        {
            if(group.history.TryGetValue(group.day.ToString(), out List<string>? value) && value != null)
            {
                UpdateHelper.TryUpdateCustom(group.history, group.day.ToString(), 
                    x =>
                    {
                        x.Add(message);
                        return x;
                    });
            }
            else
            {
                group.history.TryAdd(group.day.ToString(), new List<string>() { message });
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
        public async Task setGameMessageHistory(string groupName, string message = "", List<string>? messages = null)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                string addMessage = string.IsNullOrEmpty(message) ? "" : message;
                if(messages != null)
                {
                    addMessage += "These players answered Spritual Questions correctly! (VP + 0.25): \n";
                    foreach (string smallMessage in messages)
                    {
                        addMessage += smallMessage + "\n";
                    }
                }
                if (group.GameMessageHistory.TryGetValue(group.day.ToString(), out List<string>? value) && value != null)
                {
                    UpdateHelper.TryUpdateCustom(group.GameMessageHistory, group.day.ToString(),
                        x =>
                        {
                            x.Add(addMessage);
                            return x;
                        });
                }
                else
                {
                    group.GameMessageHistory.TryAdd(group.day.ToString(), new List<string>() { addMessage });
                }
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task setWhoAnswerSpritualQuestionsCorrectly(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                group.whoAnswerSpritualQuestionsCorrectly.Add(name);
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task resetWhoAnswerSpritualQuestionsCorrectly(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                group.whoAnswerSpritualQuestionsCorrectly.Clear();
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        /*        public async Task<Dictionary<string, List<string>>?> getAllMessagesInGroup(string groupName)
                {
                    GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
                    if (group == null)
                    {
                        return null;
                    }
                    return group.message;
                }*/
        public async Task<Users?> getGroupLeaderFromGroup(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group == null)
            {
                return null;
            }
            return group.onlineUsers.FirstOrDefault(x => x.Value.groupLeader).Value;
        }
        public async Task<Users?> getOneUserFromGroup(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group == null)
            {
                return null;
            }
            if(group.onlineUsers.TryGetValue(name, out Users? users)){
                return users;
            }
            return null;
        }
        public async Task<Users?> getSpecificUserFromGroupByIdentity(string groupName, Identities identity)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                ICollection<Users> users = group.onlineUsers.Values;
                var a = from user in users
                        where user.identity == identity
                        select user;
                return a.FirstOrDefault();
            }
            return null;
        }
        public async Task<string?> getConnectionIdByName(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                if(group.onlineUsers.TryGetValue(name, out Users? selectUser) && selectUser != null)
                {
                    return selectUser.connectionId;
                }
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
                return null;
            }
            ConcurrentDictionary<string, double>? convert = JsonConvert.DeserializeObject<ConcurrentDictionary<string, double>>(value);
            return convert;
        }
        public async Task removeVoteListAsync(string groupName)
        {
            await distributedCache.RemoveAsync(groupName + "VoteList");
        }


        // Game
        public async Task turnOnGameInProgress(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                group.gameInProgess = true;
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
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
        /*public async Task<bool> decreaseWaitingUsers(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                if(group.numberofWaitingUser.TryGetValue("WaitingUsers", out int currentWaitingUser))
                {
                    if(currentWaitingUser <= 1)
                    {
                        List<Users> NotInGameUser = await collectAllExiledAndOfflineUserName(groupName);
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
        }*/
        /*public async Task<bool> addWaitingUser(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                if (group.numberofWaitingUser.TryGetValue("WaitingUsers", out int currentWaitingUser))
                {
                   
                    UpdateHelper.TryUpdateCustom(group.numberofWaitingUser, "WaitingUsers",
                        x => {
                            x++;
                            return x;
                        });
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    return true;
                }
            }
            return false;
        }*/
        public async Task<Users?> whoIsDiscussingNext(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                Users? nextUser = group.onlineUsers.Values.FirstOrDefault(x => !x.offLine && x.inGame && !x.disscussed);
                if(nextUser != null)
                {
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, nextUser.name,
                        x => {
                            x.disscussed = true;
                            return x;
                        });
                    group.WhoIsCurrentlyDiscussing = nextUser.name;
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    return nextUser;
                }
                // everyone finished discuss, reset disscussed status.
                else
                {
                    foreach(KeyValuePair<string, Users> user in group.onlineUsers)
                    {
                        if(user.Value.inGame && !user.Value.offLine)
                        {
                            UpdateHelper.TryUpdateCustom(group.onlineUsers, user.Value.name,
                                x => {
                                    x.disscussed = false;
                                    return x;
                                });
                        }
                    }
                    group.WhoIsCurrentlyDiscussing = null;
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
        ///     return -1, meaning an error occur.
        ///     return 0, meaning not all user finish voting.
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
                    else if (group.onlineUsers.TryGetValue(voteHighestPerson.Item1, out Users? users) && users != null)
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
                return 0;
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
                    Users? user = group.onlineUsers.Values.FirstOrDefault(x => x.aboutToExile);
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
                    Users? user = await getOneUserFromGroup(groupName, exileName);
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
        public async Task<int> increaseAndGetDay(string groupName, bool justGetDay = false)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                if (!justGetDay)
                {
                    group.day++;
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                }
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
                Users? user;
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
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task NicodemusSetProtection(string groupName, bool protectionStatus)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            Users? user = await getSpecificUserFromGroupByIdentity(groupName, Identities.Nicodemus);
            if (user != null && group != null)
            {
                UpdateHelper.TryUpdateCustom(group.onlineUsers, user.name,
                    x => {
                        x.nicodemusProtection = protectionStatus;
                        return x;
                    });
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
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
                if (group.onlineUsers.TryGetValue(checkName, out Users? checkUser))
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
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
            return flag;
        }
        public async Task<Users?> checkAndSetIfAnyoneOutOfGame(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                Users? user = group.onlineUsers.Values.FirstOrDefault(x => x.aboutToExile);
                if(user != null)
                {
                    if(user.identity == Identities.Peter)
                    {
                        Users? John = await getSpecificUserFromGroupByIdentity(groupName, Identities.John);
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
                            /*group.lastNightExiledPlayer = null;*/
                            group.lastNightExiledPlayer = ("", Identities.NullState);
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
                    group.lastNightExiledPlayer = (user.name, user.identity);
                    AddNewMessageIntoGroup(group, $"{user.name} has been exiled! His/her identity is {user.identity}!");
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    return user;
                }
                group.lastNightExiledPlayer = ("", Identities.NullState);
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
                Users? Peter = await getSpecificUserFromGroupByIdentity(groupName, Identities.Peter);
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
        public async Task<List<Users>> collectAllExiledAndOfflineUserName(string groupName)
        {
            List<Users> usersList = new List<Users>();
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                foreach(KeyValuePair<string, Users> user in group.onlineUsers)
                {
                    if(!user.Value.inGame || user.Value.offLine)
                    {
                        usersList.Add(user.Value);
                    }
                }
            }
            return usersList;
        }
        public async Task<List<Users>> collectAllExileUserName(string groupName)
        {
            List<Users> usersList = new List<Users>();
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                foreach (KeyValuePair<string, Users> user in group.onlineUsers)
                {
                    if (!user.Value.inGame)
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
        public async Task<(string, Identities)?> getLastNightExiledPlayer(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.lastNightExiledPlayer;
            }
            return null;
        }
        public async Task<ConcurrentDictionary<string, List<string>>?> getGameHistory(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.history;
            }
            return null;
        }
        public async Task<ConcurrentDictionary<string, List<string>>?> getGameMessageHistory(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.GameMessageHistory;
            }
            return null;

        }
        public async Task<List<string>?> getWhoAnswerSpritualQuestionsCorrectly(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.whoAnswerSpritualQuestionsCorrectly;
            }
            return null;
        }
        public async Task setDiscussingTopic(string groupName, string topic)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                group.discussingTopic = topic;
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<string> getDiscussingTopic(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.discussingTopic;
            }
            return "";
        }
        public async Task setVoteResult(string groupName, string result)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                group.voteResult = result;
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<string> getVoteResult(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.voteResult;
            }
            return "";
        }
        /*public async Task setWhoWins(string groupName, int whoWins)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                group.whoWins = whoWins;
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<int> getWhoWins(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.whoWins;
            }
            return -1;
        }*/
        public async Task beforeGameCleanUp(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                group.beforeGameCleanUp();
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<double> getWaitingProgessPercentage(string groupName, int currentNumOfViewingUser)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return (double) (group.maxPlayers - currentNumOfViewingUser) / group.maxPlayers * 100;
            }
            return -1.0;
        }
        public async Task cleanUp(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                foreach(KeyValuePair<string, Users> user in group.onlineUsers)
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
                            x.offLine = false;
                            x.viewedResult = false;
                            return x;
                        });
                }
                group.cleanUp();
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }

        // disconnection methods
        public async Task<ConcurrentDictionary<string, List<string>>?> getConnectionIdAndGroupName()
        {
            string? value = await distributedCache.GetStringAsync("ConnectionIdAndGroupName");
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            ConcurrentDictionary<string, List<string>>? convert =
                    JsonConvert.DeserializeObject<ConcurrentDictionary<string, List<string>>>(value);
            return convert;
        }
        public async Task<string?> removeConnectionIdFromGroup(string connectionId, string groupName = "")
        {
            ConcurrentDictionary<string, List<string>>? ConnectionIdAndGroupName = await getConnectionIdAndGroupName();
            if (ConnectionIdAndGroupName != null)
            {
                if(string.IsNullOrEmpty(groupName))
                {
                    foreach (KeyValuePair<string, List<string>> valuePair in ConnectionIdAndGroupName)
                    {
                        if (!string.IsNullOrEmpty(valuePair.Value.FirstOrDefault(x => x == connectionId)))
                        {
                            UpdateHelper.TryUpdateCustom(ConnectionIdAndGroupName, valuePair.Key,
                                x =>
                                {
                                    x.Remove(connectionId);
                                    return x;
                                });
                            if (valuePair.Value.Count == 0)
                            {
                                ConnectionIdAndGroupName.TryRemove(valuePair.Key, out _);
                            }
                            await distributedCache.SetStringAsync("ConnectionIdAndGroupName", JsonConvert.SerializeObject(ConnectionIdAndGroupName));
                            return valuePair.Key;
                            
                        }
                    }
                }
                else
                {
                    UpdateHelper.TryUpdateCustom(ConnectionIdAndGroupName, groupName,
                        x =>
                        {
                            x.Remove(connectionId);
                            return x;
                        });
                    if (ConnectionIdAndGroupName.TryGetValue(groupName, out List<string>? connectionIds) && connectionIds.Count == 0)
                    {
                        ConnectionIdAndGroupName.TryRemove(groupName, out _);
                    }
                    await distributedCache.SetStringAsync("ConnectionIdAndGroupName", JsonConvert.SerializeObject(ConnectionIdAndGroupName));
                    return groupName;
                }
            }
            return null;
        }
        public async Task removeGroupInConnectionIdAndGroupName(string groupName)
        {
            ConcurrentDictionary<string, List<string>>? ConnectionIdAndGroupName = await getConnectionIdAndGroupName();
            if (ConnectionIdAndGroupName != null)
            {
                ConnectionIdAndGroupName.TryRemove(groupName, out _);
                await distributedCache.SetStringAsync("ConnectionIdAndGroupName", JsonConvert.SerializeObject(ConnectionIdAndGroupName));
            }
        }
        public async Task<string?> getGroupNameByConnectionId(string connectionId)
        {
            ConcurrentDictionary<string, List<string>>? ConnectionIdAndGroupName = await getConnectionIdAndGroupName();
            
            if (ConnectionIdAndGroupName != null)
            {
                foreach (KeyValuePair<string, List<string>> valuePair in ConnectionIdAndGroupName)
                {
                    if (!string.IsNullOrEmpty(valuePair.Value.FirstOrDefault(x => x == connectionId)))
                    {
                        return valuePair.Key;
                    }
                }
            }
            
            return null;
        }
        public async Task<bool> addConnectionIdToGroup(string connectionId, string groupName)
        {
            ConcurrentDictionary<string, List<string>>? ConnectionIdAndGroupName = await getConnectionIdAndGroupName();
            if (ConnectionIdAndGroupName != null)
            {
                // if group exists, then add to the list. If not, create new group.
                if (ConnectionIdAndGroupName.TryGetValue(groupName, out List<string>? someValue) && someValue != null)
                {
                    UpdateHelper.TryUpdateCustom(ConnectionIdAndGroupName, groupName,
                        x =>
                        {
                            x.Add(connectionId);
                            return x;
                        });

                    await distributedCache.SetStringAsync("ConnectionIdAndGroupName", JsonConvert.SerializeObject(ConnectionIdAndGroupName));
                    return true;
                }
            }
            else
            {
                ConnectionIdAndGroupName = new ConcurrentDictionary<string, List<string>>();
            }
            if (ConnectionIdAndGroupName.TryAdd(groupName, new List<string>() { connectionId }))
            {
                await distributedCache.SetStringAsync("ConnectionIdAndGroupName", JsonConvert.SerializeObject(ConnectionIdAndGroupName));
                return true;
            }
            return false;
        }
        public async Task<Users?> chooseARandomPlayerToExile(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                List<Users> onlineUsers = new List<Users> { };
                foreach(Users user in group.onlineUsers.Values)
                {
                    if(user.inGame && !user.offLine)
                    {
                        onlineUsers.Add(user);
                    }
                }
                Random rand = new Random();
                return onlineUsers[rand.Next(onlineUsers.Count)];
            }
            return null;
        }
        public async Task<bool> changeCurrentGameStatus(string groupName, string gameStatus)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
               /* if(!group.currentGameStatus.TryGetValue("currentGameStatus", out string? _))
                {
                    group.currentGameStatus.TryAdd("currentGameStatus", gameStatus);
                }
                else
                {
                    group.currentGameStatus.TryRemove("currentGameStatus", out string? _);
                    group.currentGameStatus.TryAdd("currentGameStatus", gameStatus);
                }*/
                group.currentGameStatus = gameStatus;
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                return true;
            }
            return false;
        }
        public async Task<string> getCurrentGameStatus(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                /*if(group.currentGameStatus.TryGetValue("currentGameStatus", out string? value) && !string.IsNullOrEmpty(value))
                {
                    return value;
                }*/
                return group.currentGameStatus;
            }
            return "";
        }

        public async Task setViewedResultToTrue(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                UpdateHelper.TryUpdateCustom(group.onlineUsers, name,
                    x =>
                    {
                        x.viewedResult = true;
                        return x;
                    });
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task resetAllViewedResultState(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                foreach(KeyValuePair<string, Users> user in group.onlineUsers)
                {
                    // if player is not offline, then set viewedResult to false.
                    // For player is offline, keep the viewedResult to true.
                    // Because when player is back online, he already passed the current game state
                    /*if (!user.Value.offLine) 
                    {
                        UpdateHelper.TryUpdateCustom(group.onlineUsers, user.Value.name,
                           x =>
                           {
                               x.viewedResult = false;
                               return x;
                           });
                    }*/
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, user.Value.name,
                        x =>
                        {
                            x.viewedResult = false;
                            return x;
                        });
                }
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<List<Users>?> doesAllPlayerViewedResult(string groupName)
        {
            /*List<Users> usersList = new List<Users>();*/
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                // if the player is online, ingame and does not view the result, then we need to wait on this player.
                return group.onlineUsers.Values.Where(x => !x.offLine && x.inGame && !x.viewedResult).ToList();
                /*                foreach (KeyValuePair<string, Users> user in group.onlineUsers)
                                {
                                    if(!user.Value.offLine && user.Value.inGame && !user.Value.viewedResult)
                                    {
                                        usersList.Add(user.Value);
                                    }
                                }*/
                /*return usersList;*/
            }
            return null;
        }

        public async Task<List<Users>?> getListOfOfflineUsers(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.onlineUsers.Values.Where(x => x.offLine).ToList();
            }
            return null;
        }
        public async Task<Users?> removeUserFromGroupByConnectionIdAndSetOfflineTrue(string connectionId)
        {
            string? groupName = await getGroupNameByConnectionId(connectionId);
            if (!string.IsNullOrEmpty(groupName))
            {
                GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
                if (group != null)
                {
                    Users? user = group.onlineUsers.Values.FirstOrDefault(x => x.connectionId == connectionId);
                    if (user != null)
                    {
                        if (group.gameInProgess)
                        {
                            UpdateHelper.TryUpdateCustom(group.onlineUsers, user.name,
                                x =>
                                {
                                    x.offLine = true;
                                    return x;
                                });
                            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                            return user;
                        }
                        else
                        {
                            return await removeUserFromGroup(groupName, user.name);
                        }
                    }
                }
            }
            return null;
        }
        public async Task setOfflineFalse(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                UpdateHelper.TryUpdateCustom(group.onlineUsers, name, 
                    x =>
                    {
                        x.offLine = false;
                        return x;
                    });
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task setNewConnectionId(string groupName, string name, string newConnectionId)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                UpdateHelper.TryUpdateCustom(group.onlineUsers, name,
                    x =>
                    {
                        x.connectionId = newConnectionId;
                        return x;
                    });
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<string?> getWhoIsCurrentlyDiscussing(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                return group.WhoIsCurrentlyDiscussing;
            }
            return null;
        }
        public async Task setFirstTimeConnectToFalse(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                UpdateHelper.TryUpdateCustom(group.onlineUsers, name,
                    x =>
                    {
                        x.isFirstTimeConnect = false;
                        return x;
                    });
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
            }
        }
        public async Task<bool> getFirstTimeConnect(string groupName, string name)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                if(group.onlineUsers.TryGetValue(name, out Users? user) && user != null)
                {
                    return user.isFirstTimeConnect;
                }
            }
            return true;
        }

        //private methods
        private void updateVote(GamesGroupsUsersMessages group, string votePerson, string fromWho,
            ConcurrentDictionary<string, double> voteList)
        {
            if (group.onlineUsers.TryGetValue(fromWho, out Users? fromWhoUser) && fromWhoUser.inGame)
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
        private int addLostVote(GamesGroupsUsersMessages group, Users users)
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