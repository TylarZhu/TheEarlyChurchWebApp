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
            return group.onlineUsers.Values.FirstOrDefault();
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
        public async Task<OnlineUsers?> getSpecificIdentityFromGroup(string groupName, Identities identity)
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
        public async Task<OnlineUsers?> getPriest(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                return group.onlineUsers.Values.FirstOrDefault(x => x.priest);
            }
            return null;
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
                            x.assignOriginalVoteAndAbility();
                            
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
                            AddNewMessageIntoGroup(group, equalVoteMessage);
                            returnType = 3;
                        }
                        // someone lost vote weight
                        else if (group.onlineUsers.TryGetValue(voteHighestPerson.Item1, out OnlineUsers? users) && !users.disempowering)
                        {
                            // update lost vote
                            returnType = addLostVote(group, users);

                            AddNewMessageIntoGroup(group, $"{users.name} has been vote out on Day {group.day}!");
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
        public async Task<(bool, int)> whoWins(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                return group.winCondition();
            }
            return (false, -1);
        }
        public async Task<List<OnlineUsers>?> assignPriestAndRulerOfTheSynagogue(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                Random rand = new Random();
                OnlineUsers? priest, rulerOfTheSynagogue;
                // assign priest
                if (rand.Next(0, 2) == 1)
                {
                    priest = group.onlineUsers.Values.FirstOrDefault(x => x.identity == Identities.Scribes);
                    rulerOfTheSynagogue = group.onlineUsers.Values.FirstOrDefault(x => x.identity == Identities.Pharisee);
                }
                else
                {
                    priest = group.onlineUsers.Values.FirstOrDefault(x => x.identity == Identities.Pharisee);
                    rulerOfTheSynagogue = group.onlineUsers.Values.FirstOrDefault(x => x.identity == Identities.Scribes);
                }
                // avoid duplicate assign.
                // if the user already assigned as priest or rulerOfTheSynagogue before,
                // then avoid to be assigned again.
                if (priest != null && rulerOfTheSynagogue != null && 
                    !(priest.priest || priest.rulerOfTheSynagogue))
                {
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, priest.name,
                        x =>
                        {
                            x.priest = true;
                            return x;
                        });
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, rulerOfTheSynagogue.name,
                        x =>
                        {
                            x.rulerOfTheSynagogue = true;
                            return x;
                        });
                    AddNewMessageIntoGroup(group, $"{priest.name} is the Priest in this game! ");
                    AddNewMessageIntoGroup(group, $"{rulerOfTheSynagogue.name} is the Ruler Of The Synagogue in this game!");
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    return new List<OnlineUsers> { priest, rulerOfTheSynagogue };
                }
                else
                {
                    return new List<OnlineUsers>();
                }
            }
            return null;
        }
        public async Task<bool> setExile(string groupName, bool exileStat, string exileName = "")
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                if(string.IsNullOrEmpty(exileName))
                {
                    OnlineUsers? user = group.onlineUsers.Values.FirstOrDefault(x => x.aboutToExile);
                    if (user != null)
                    {
                        UpdateHelper.TryUpdateCustom(group.onlineUsers, user.name,
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
                else
                {
                    UpdateHelper.TryUpdateCustom(group.onlineUsers, exileName,
                        x =>
                        {
                            x.aboutToExile = exileStat;
                            return x;
                        });
                }
                await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                return true;
            }
            return false;
        }
        public async Task increaseDay(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if (group != null)
            {
                group.day++;
            }
            await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
        }
        /// <summary>
        /// Set changed vote for current user. The user should have either name or identity.
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="name"></param>
        /// <param name="identities"></param>
        /// <param name="changedVote"></param>
        /// <param name="option"></param>
        /// <returns></returns>
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
                    user = await getSpecificIdentityFromGroup(groupName, identities!.Value);
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
            OnlineUsers? user = await getSpecificIdentityFromGroup(groupName, Identities.Nicodemus);
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
        public async Task<OnlineUsers?> checkIfAnyoneOutOfGame(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                OnlineUsers? user = group.onlineUsers.Values.FirstOrDefault(x => x.aboutToExile);
                if(user != null)
                {
                    if(user.identity == Identities.Peter)
                    {
                        OnlineUsers? John = await getSpecificIdentityFromGroup(groupName, Identities.John);
                        if(John != null && John.johnProtection)
                        {
                            UpdateHelper.TryUpdateCustom(group.onlineUsers, John.name,
                               x => {
                                   x.johnProtection = false;
                                   return x;
                               });
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
                    AddNewMessageIntoGroup(group, $"{user.name} has been exiled! His/her identity is {user.identity}!");
                    await distributedCache.SetStringAsync(groupName, JsonConvert.SerializeObject(group));
                    return user;
                }
            }
            return null;
        }
        public async Task PeterIncreaseVoteWeightByOneOrNot(string groupName)
        {
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null && group.day == 3)
            {
                OnlineUsers? Peter = await getSpecificIdentityFromGroup(groupName, Identities.Peter);
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
        public async Task<List<string>> collectAllExiledUserName(string groupName)
        {
            List<string> usersList = new List<string>();
            GamesGroupsUsersMessages? group = await GetGroupAsync(groupName);
            if(group != null)
            {
                foreach(KeyValuePair<string, OnlineUsers> user in group.onlineUsers)
                {
                    if(!user.Value.inGame)
                    {
                        usersList.Add(user.Key);
                    }
                }
            }
            return usersList;
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
                group.christianLostVote += users.originalVote;
                returnType = 1;
            }
            else
            {
                group.judaismLostVote += users.originalVote;
                returnType = 2;
            }

            return returnType;
        }
    }
}
