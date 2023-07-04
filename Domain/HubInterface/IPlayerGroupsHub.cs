using Domain.APIClass;
using Domain.Common;

namespace Domain.HubInterface
{
    public interface IPlayerGroupsHub
    {
        // type is either message or error
        Task ReceiveMessages(List<Message> messages);
        Task CreateNewUserJoinNewGroup(string connectionId, string groupName, string username, string numberOfPlayers);
        Task updateOnlineUserList(List<OnlineUsers> onlineUsers);
        Task leaveGroupUserConnectionId(string connectionId);
        Task updateGroupLeader(CreateNewUser users);
        Task GameStop();
        Task updatePlayersIdentities(string identity);
        Task getMaxPlayersInGroup(string max);
        Task IdentitiesExplanation(List<string> ex);

        /// <summary>
        /// User who invoke this method finishs the current action and wait for next step.
        /// </summary>
        /// <param name="waitingState"> if true, user will be in the loading screen. If False, out of loading screen.</param>
        /// <param name="nextStep"> specificly discribe that which screen shows to the user.</param>
        /// <param name="commands"> send payloads to UI to display on screen.</param>
        /// <returns></returns>
        Task finishedViewIdentityAndWaitOnOtherPlayers(bool waitingState);
        Task currentUserInDiscusstion(string waitingState);
        Task nextStep(NextStep nextStep);
        Task finishVoteWaitForOthersOrVoteResult(bool waitingState, string result);
        Task PriestROTSNicoMeet(string ROTSName, string priestName, string NicodemusName);
        Task PriestRound();
        Task RulerOfTheSynagogueMeeting();
        Task NicoMeeting();
        Task priestExileRoundFinish();
        Task JudasCheckResult(bool status);
        Task updateExiledUsers(List<string> userNames);
        Task announceExile(string name);
        Task changeDay(int day);
    }
}
