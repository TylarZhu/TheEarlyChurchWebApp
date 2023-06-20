namespace Domain.Common
{
    public class OnlineUsers
    {
        // ------------ //
        // group fields //
        // ------------ //
        public string connectionId { get; set; } = null!;
        public string name { get; set; } = null!;
        public bool groupLeader { get; set; } = false;

        // ------------ //
        // game fields  //
        // ------------ //
        public Identities identity { get; set; }

        // originial vote is used to calculate lost vote for winning condition.
        public double originalVote { get; set; }

        // vote can be changed due to user's ability or day meetings
        public double changedVote { get; set; }
        public bool johnProtection { get; set; } = false;
        public bool nicodemusProtection { get; set; } = false;
        public bool priest { get; set; } = false;
        public bool rulerOfTheSynagogue { get; set; } = false;

        public bool judasCheck { get; set; } = false;
        public bool inGame { get; set; } = true;

        // for calculating lost vote weight of christians or judaism. (avoid duplicate lost vote)
        public bool disempowering { get; set; } = false;

        // set true, if the priest is going to exile this user. Remember to set to false, once Nicodemus round passed. 
        public bool aboutToExile { get; set; } = false;


        public OnlineUsers(string name, string connectionId, bool groupLeader = false)
        {
            /*this.groupName = groupName;*/
            this.name = name;
            this.connectionId = connectionId;
            this.groupLeader = groupLeader;
        }
        public void assignOriginalVoteAndAbility()
        {
            switch (identity)
            {
                case Identities.Judas:
                    originalVote = 0.5;
                    judasCheck = true;
                    break;
                case Identities.Scribes:
                    originalVote = 1;
                    break;
                case Identities.Pharisee:
                    originalVote = 1;
                    break;
                case Identities.Judaism:
                    originalVote = 1;
                    break;
                case Identities.Peter:
                    originalVote = 1.5;
                    break;
                case Identities.John:
                    originalVote = 1.5;
                    johnProtection = true;
                    break;
                case Identities.Laity:
                    originalVote = 1;
                    break;
                case Identities.Nicodemus:
                    originalVote = 0.5;
                    nicodemusProtection = true;
                    break;
            }
            changedVote = originalVote;
        }
    }
}
