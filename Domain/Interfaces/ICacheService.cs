using Domain.Common;
using Domain.DBEntities;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Numerics;

namespace Domain.Interfaces
{
    public interface ICacheService
    {
        // Group and Users (get)
        Task<GamesGroupsUsersMessages?> GetGroupAsync(string groupName, CancellationToken cancellationToken = default);
        Task<List<OnlineUsers>> getAllUsers(string groupName);
        Task<OnlineUsers?> getFirstUser(string groupName);
        Task<Dictionary<int, List<string>>?> getAllMessagesInGroup(string groupName);
        Task<OnlineUsers?> getGroupLeaderFromGroup(string groupName);
        Task<OnlineUsers?> getOneUserFromGroup(string groupName, string name);
        Task<string> getMaxPlayersInGroup(string groupName);
        Task<OnlineUsers?> getSpecificUserFromGroupByIdentity(string groupName, Identities identity);
        Task<OnlineUsers?> getPriest(string groupName);


        // Group and Users (set)
        Task SetNewGroupAsync(string groupName, GamesGroupsUsersMessages value, OnlineUsers groupLeader, CancellationToken cancellationToken = default);
        Task<OnlineUsers?> assignUserAsGroupLeader(string groupName, string nextGroupLeader, string originalGroupLeader);
        // Group and Users (remove and set)
        Task<OnlineUsers?> removeUserAndAssignNextUserAsGroupLeader(string groupName, string name);


        // Group and Users (remove)
        Task RemoveGroupAsync(string groupName, CancellationToken cancellationToken = default);


        // Group and Users (add)
        Task AddNewUsersToGroupAsync(string groupName, OnlineUsers user, CancellationToken cancellationToken = default);
        void AddNewMessageIntoGroup(GamesGroupsUsersMessages group, string message);


        // Group and Users (check)
        Task<bool> checkIfUserNameInGroupDuplicate(string groupName, string name);
        Task<bool> checkIfGroupIsFull(string groupName);
        Task<bool> isGroupEmpty(string groupName);


        // Vote List
        Task setNewVoteListAsync(string groupName, ConcurrentDictionary<string, double> newVoteList);
        Task<ConcurrentDictionary<string, double>?> getVoteList(string groupName);
        Task removeVoteListAsync(string groupName);



        // Game
        Task<bool> createAGameAndAssignIdentities(string groupName, int christans, int judaisms);
        Task<bool> waitOnOtherPlayersActionInGroup(string groupName);
        Task<string?> whoIsDiscussingNext(string groupName, string name);
        Task<(bool, int)> votePerson(string groupName, string votePerson, string fromWho);
        Task<(bool, int)> whoWins(string groupName);
        Task<List<OnlineUsers>?> assignPriestAndRulerOfTheSynagogue(string groupName);
        Task<bool> setExile(string groupName, bool exileState, string exileName = "");
        Task<int> increaseDay(string groupName);
        Task changeVote(string groupName, string name = "", Identities? identities = null, double changedVote = 0.0, string option = "");
        Task NicodemusSetProtection(string groupName, bool protectionStatus);
        Task<List<string>?> GetJohnCannotFireList(string groupName);
        Task<bool> checkJohnFireAllOrNot(string groupName);
        Task AddToJohnFireList(string groupName, string fireName);
        Task<bool> JudasCheck(string groupName, string checkName);
        Task<OnlineUsers?> checkIfAnyoneOutOfGame(string groupName);
        Task PeterIncreaseVoteWeightByOneOrNot(string groupName);
        Task<List<string>> collectAllExiledUserName(string groupName);
    }
}
