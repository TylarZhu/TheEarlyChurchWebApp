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
        Task<string?> getConnectionIdByName(string groupName, string name);
        Task<ConcurrentDictionary<string, List<string>>?> getGameHistory(string groupName);
        Task<ConcurrentDictionary<string, List<string>>?> getGameMessageHistory(string groupName);
        Task<List<string>?> getWhoAnswerSpritualQuestionsCorrectly(string groupName);


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
        Task setGameMessageHistory(string groupName, string message = "", List<string>? messages = null);
        Task setWhoAnswerSpritualQuestionsCorrectly(string groupName, string name);
        Task resetWhoAnswerSpritualQuestionsCorrectly(string groupName);

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
        /*Task<bool> decreaseWaitingUsers(string groupName);
        Task<bool> addWaitingUser(string groupName);*/
        /*Task resetWaitingPlayerToPrepareToViewGameHistory(string groupName);*/
        Task<Users?> whoIsDiscussingNext(string groupName);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="votePerson"></param>
        /// <param name="fromWho"></param>
        /// <param name="everyoneFinishVoting">True, meaning there are other users did not finish voting. 
        /// False, all user finish voting and return the result</param>
        /// <returns></returns>
        Task<int> votePerson(string groupName, string votePerson, string fromWho, bool everyoneFinishVoting);
        Task<int> whoWins(string groupName);
        // isNico is for game history
        Task<bool> setExile(string groupName, bool exileState, string exileName = "");
        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupName"></param>
        /// <param name="justGetDay">if justGetDay = true, then day will not increase.</param>
        /// <returns></returns>
        Task<int> increaseAndGetDay(string groupName, bool justGetDay = false);
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
        Task<Users?> chooseARandomPlayerToExile(string groupName);
        Task setDiscussingTopic(string groupName, string topic);
        Task<string> getDiscussingTopic(string groupName);
        Task setVoteResult(string groupName, string result);
        Task<string> getVoteResult(string groupName);
        /*Task setWhoWins(string groupName, int whoWins);
        Task<int> getWhoWins(string groupName);*/
        Task beforeGameCleanUp(string groupName);
        Task<double> getWaitingProgessPercentage(string groupName, int currentNumOfViewingUser);
        
        Task cleanUp(string groupName);


        // player refresh page or close tab
        // --- WARNING --- //
        // These three methods are operations on ConnectionIdAndGroupName DB only!
        // We assume that there is only one user remain connected per group.
        Task<ConcurrentDictionary<string, List<string>>?> getConnectionIdAndGroupName();
        Task<string?> removeConnectionIdFromGroup(string connectionId, string groupName = "");
        Task removeGroupInConnectionIdAndGroupName(string groupName);
        Task<string?> getGroupNameByConnectionId(string connectionId);
        Task<bool> addConnectionIdToGroup(string connectionId, string groupName);
        // --- WARNING --- //

        Task changeCurrentGameStatus(string groupName, string gameStatus);
        Task<string> getCurrentGameStatus(string groupName);
        Task setViewedResultToTrue(string groupName, string name);
        Task resetAllViewedResultState(string groupName);
        Task<List<Users>?> doesAllPlayerViewedResult(string groupName);
        Task<List<Users>?> getListOfOfflineUsers(string groupName);
        Task<Users?> getWhoIsCurrentlyDiscussing(string groupName);
        /// <summary>
        /// call this method when user close the tab or refresh the page
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        Task<Users?> removeUserFromGroupByConnectionIdAndSetOfflineTrue(string connectionId);
        Task setOfflineFalse(string groupName, string name);
        Task setNewConnectionId(string groupName, string name, string newConnectionId);
        Task setFirstTimeConnectToFalse(string groupName, string name);
        Task<bool> getFirstTimeConnect(string groupName, string name);
    }
}
