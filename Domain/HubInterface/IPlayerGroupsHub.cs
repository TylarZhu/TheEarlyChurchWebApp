﻿using Domain.APIClass;
using Domain.Common;
using Domain.DBEntities;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace Domain.HubInterface
{
    public interface IPlayerGroupsHub
    {
        // type is either message or error
        /*Task ReceiveMessages(List<Message> messages);*/
        Task CreateNewUserJoinNewGroup(string connectionId, string groupName, string username, string numberOfPlayers);
        Task updateUserList(List<Users> onlineUsers);
        Task leaveGroupUserConnectionId(string connectionId);
        Task updateGroupLeader(CreateNewUser users);
        Task identityModalOpen(bool openIdentitiesExplanationModal);
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
        Task currentUserInDiscusstion(string state, string inDiscusstionUserName = "");
        Task nextStep(NextStep nextStep);
        Task finishVoteWaitForOthersOrVoteResult(bool waitingState, string result = "");
        Task PriestROTSNicoMeet(string ROTSName, string priestName, string NicodemusName);
        Task PriestRound();
        Task RulerOfTheSynagogueMeeting();
        Task PriestMeetingOnReconnect();
        Task NicoMeeting();
        Task priestExileRoundFinish();
        Task JudasCheckResult(bool status);
        Task updateExiledUsers(List<Users> userNames);
        Task announceExile(string name);
        Task changeDay(int day);
        Task getAQuestion(Questions questions);
        Task inAnswerQuestionName(string name = "");
        Task JudasGivePriestHint();
        Task AssignJudasHimselfAndPriestName(string priestName, string judasName);
        Task PriestReceiveHint(string JudasName, string hint);
        Task announceLastExiledPlayerInfo(bool status, string name);
        Task announceWinner(int winnerParty);
        Task announceGameHistory(ConcurrentDictionary<string, List<string>>? history);
        Task updateGameMessageHistory(ConcurrentDictionary<string, List<string>>? gameMessageHistory);
        Task announceOffLinePlayer(List<string> playerNameList);

        // user refresh page or close tab
        Task IdentityViewingStateFinish(string removeUserGroupName, string leaveUserName);
        Task DiscussingStateFinish(string removeUserGroupName);
        Task VoteStateFinish(string removeUserGroupName, string leaveUserName);
        Task PriestRoundStateFinish(string groupName);
        Task JudasMeetWithPriestStateFinish(string groupName);
        Task NicodemusSavingRoundBeginStateFinish(string groupName);
        Task JohnFireRoundBeginStateFinish(string groupName);
        Task JudasCheckRoundStateFinish(string groupName);
        Task finishedToViewTheExileResultStateFinish(string groupName, string leaveUserName);
        Task spiritualQuestionAnsweredCorrectOrNotStateFinish(string groupName, string leaveUserName);
        Task stillWaitingFor(List<Users> didNotViewedUsers);
        Task openOrCloseExileResultModal(bool status);
        Task repostOnlineUser(string newConnectionId, string groupName, string name, string maxPlayer);
        Task redirectToHomePage();
        Task updateWaitingProgess(double currentNumOfViewingUser);
        Task closeWaitOthersToVoteModel();
    }
}
