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
        Task<List<Users>> getAllUsers(string groupName);
        Task<Users?> getFirstOnlineUser(string groupName);
        /*Task<Dictionary<string, List<string>>?> getAllMessagesInGroup(string groupName);*/
        Task<Users?> getGroupLeaderFromGroup(string groupName);
        Task<Users?> getOneUserFromGroup(string groupName, string name);
        Task<string> getMaxPlayersInGroup(string groupName);
        Task<Users?> getSpecificUserFromGroupByIdentity(string groupName, Identities identity);
        

        // Group and Users (set)
        Task SetNewGroupAsync(string groupName, GamesGroupsUsersMessages value, Users groupLeader, CancellationToken cancellationToken = default);
        Task<Users?> assignUserAsGroupLeader(string groupName, string nextGroupLeader, string originalGroupLeader);
        // Group and Users (remove and set)
        Task<Users?> removeUserFromGroup(string groupName, string name);


        // Group and Users (remove)
        Task RemoveGroupAsync(string groupName, CancellationToken cancellationToken = default);


        // Group and Users (add)
        Task AddNewUsersToGroupAsync(string groupName, Users user, CancellationToken cancellationToken = default);
        void AddNewMessageIntoGroup(GamesGroupsUsersMessages group, string message);
        Task AddNewMessageIntoGroupAndSave(string groupName, string message);


        // Group and Users (check)
        Task<bool> checkIfUserNameInGroupDuplicate(string groupName, string name);
        Task<bool> checkIfGroupIsFull(string groupName);
        Task<bool> isGroupEmpty(string groupName);


        // Vote List
        Task updateListAsync(string groupName, ConcurrentDictionary<string, double> newVoteList);
        Task<ConcurrentDictionary<string, double>?> getVoteList(string groupName);
        Task removeVoteListAsync(string groupName);



        // Game
        Task turnOnGameInProgress(string groupName);
        Task<bool> getGameInProgess(string groupName);
        Task<bool> createAGameAndAssignIdentities(string groupName);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupName"></param>
        /// <returns>
        ///     return true, if there are still other players did not take action.
        ///     return false, if all players have taked actions.
        /// </returns>
        Task<bool> decreaseWaitingUsers(string groupName);
        Task<bool> addWaitingUser(string groupName);
        /*Task resetWaitingPlayerToPrepareToViewGameHistory(string groupName);*/
        Task<Users?> whoIsDiscussingNext(string groupName);
        Task<int> votePerson(string groupName, string votePerson, string fromWho, bool everyoneFinishVoting);
        Task<int> whoWins(string groupName);
        // isNico is for game history
        Task<bool> setExile(string groupName, bool exileState, string exileName = "");
        Task<int> increaseDay(string groupName);
        /// <summary>
        /// Set changed vote for current user. The user should have either name or identity.
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="name"> if missing, then identities must have a value. </param>
        /// <param name="identities">if missing, then name must have a value. </param>
        /// <param name="changedVote">for add and directly set option.</param>
        /// <param name="option">setZero, half, add, others options will be directly set </param>
        /// <returns></returns>
        Task changeVote(string groupName, string name = "", Identities? identities = null, double changedVote = 0.0, string option = "");
        Task NicodemusSetProtection(string groupName, bool protectionStatus);
        Task<List<string>?> GetJohnCannotFireList(string groupName);
        Task<bool> checkJohnFireAllOrNot(string groupName);
        Task AddToJohnFireList(string groupName, string fireName);
        Task<bool> JudasCheck(string groupName, string checkName);
        Task<Users?> checkAndSetIfAnyoneOutOfGame(string groupName);
        Task PeterIncreaseVoteWeightByOneOrNot(string groupName);
        Task<List<Users>> collectAllExiledAndOfflineUserName(string groupName);
        Task<List<Users>> collectAllExileUserName(string groupName);
        Task<int> getDay(string groupName);
        Task<Users?> getLastNightExiledPlayer(string groupName);
        Task<ConcurrentDictionary<string, List<string>>?> getGameHistory(string groupName);
        Task<Users?> chooseARandomPlayerToExile(string groupName);
        Task cleanUp(string groupName);


        // player refresh page or close tab
        // --- WARNING --- //
        // These three methods are operations on ConnectionIdAndGroupName DB only!
        // We assume that there is only one user remain connected per group.
        Task<ConcurrentDictionary<string, List<string>>?> getConnectionIdAndGroupName();
        Task<string?> removeConnectionIdFromGroup(string connectionId);
        Task removeGroupInConnectionIdAndGroupName(string groupName);
        Task<string?> getGroupNameByConnectionId(string connectionId);
        Task<bool> addConnectionIdToGroup(string connectionId, string groupName);
        // --- WARNING --- //

        Task changeCurrentGameStatus(string groupName, string gameStatus);
        Task<string> getCurrentGameStatus(string groupName);
        Task<bool> getViewedIdentity(string groupName, string name);
        Task playerViewedIdentity(string groupName, string name);
        Task<bool> getViewedExileResult(string groupName, string name);
        Task playerViewedExileResult(string groupName, string name);
        Task resetAllViewedExileResultState(string groupName);
        Task<bool> addOfflineUser(string groupName, Users user);
        Task<Users?> removeOfflineUser(string groupName, string userName);
        Task<List<Users>?> getListOfOfflineUsers(string groupName);
        Task<Users?> getWhoIsCurrentlyDiscussing(string groupName);
        /// <summary>
        /// call this method when user close the tab or refresh the page
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        Task<Users?> removeUserFromGroupByConnectionId(string connectionId);
    }
}
