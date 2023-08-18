using Domain.APIClass;
using Domain.HubInterface;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI.HubMethods
{
    public abstract class PlayerGroupsHubBase : Hub<IPlayerGroupsHub>
    {
        public abstract Task onConntionAndCreateGroup(CreateNewUser createNewUser);
        public abstract Task leaveGroup(string gourpName, string name);
        public abstract Task reconnectionToGame(string groupName, string name);

        public abstract Task<int> IdentityViewingState(string groupName, string playerName);
        public abstract Task<int> whoIsDiscussing(string groupName);
        public abstract Task<int> voteHimOrHer(string groupName, string votePerson, string fromWho);
        public abstract Task<int> ROTSInfoRound(string groupName);
        public abstract Task<int> JudasMeetWithPriest(string groupName, string JudasHint = "");
        public abstract Task<int> exileHimOrHer(string groupName, string exileName);
        public abstract Task<int> NicodemusAction(string groupName, bool saveOrNot);
        public abstract Task<int> JohnFireRoundBegin(string groupName, string fireName = "", bool didUserChoose = false);
        public abstract Task<int> JudasCheckRound(string groupName, string checkName = "");
        public abstract Task<int> NightRoundEnd(string groupName);
        public abstract Task<int> finishedToViewTheExileResult(string groupName, string playerName);
        public abstract Task<int> spiritualQuestionAnsweredCorrectOrNot(string groupName, string name = "", bool playerChoiceCorrectOrNot = false);
        public abstract Task PriestRound(string groupName, bool outOfTime);
    }
}
