namespace Domain.Common
{
    public class OnlineUsers
    {
        // group fields
        public string connectionId { get; set; } = null!;
        public string name { get; set; } = null!;
        public bool groupLeader { get; set; } = false;

        //game fields
        public Identities identity { get; set; }
        public double originalVote { get; set; } // 最原始的权重
        public double changedVote { get; set; } // 可以增加可以减少
        public bool johnProtection { get; set; } = false;
        public bool nicodemusProtection { get; set; } = false;
        public bool priest { get; set; } = false;
        public bool rulerOfTheSynagogue { get; set; } = false;
        public bool check { get; set; } = false;
        public bool inGame { get; set; } = true;
        // for calculating lost vote weight of christians or judaism.
        public bool disempowering { get; set; } = false;


        public OnlineUsers(string name, string connectionId, bool groupLeader = false)
        {
            /*this.groupName = groupName;*/
            this.name = name;
            this.connectionId = connectionId;
            this.groupLeader = groupLeader;
        }
        public void assignOriginalVote()
        {
            switch (identity)
            {
                case Identities.Judas:
                    originalVote = 0.5;
                    check = true;
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
