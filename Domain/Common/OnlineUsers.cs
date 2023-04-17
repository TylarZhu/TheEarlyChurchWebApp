using Domain.Common;
namespace Domain.Common
{
    public class OnlineUsers
    {
        // group fields
        public string connectionId { get; set; } = null!;
        public string name { get; set; } = null!;
        public bool groupLeader { get; set; } = false;

        //game fields
        public string identity { get; set; } = "";
        public double originalVote { get; set; } // 最原始的权重
        public double changedVote { get; set; } // 可以增加可以减少
        public bool johnProtection { get; set; } = false;
        public bool nicodemusProtection { get; set; } = false;
        public bool priest { get; set; } = false;
        public bool rulerOfTheSynagogue { get; set; } = false;
        public bool check { get; set; } = false;
        public bool inGame { get; set; } = true;
        public bool disempowering { get; set; } = false;


        public OnlineUsers(string name, string connectionId, bool groupLeader = false)
        {
            /*this.groupName = groupName;*/
            this.name = name;
            this.connectionId = connectionId;
            this.groupLeader = groupLeader;
        }
        /*switch (this.identity)
            {
                case Identities.Judas:
                    this.originalVote = 0.5;
                    this.check = true;
                    break;
                case Identities.Scribes:
                    this.originalVote = 1;
                    break;
                case Identities.Pharisee:
                    this.originalVote = 1;
                    break;
                case Identities.Judaism:
                    this.originalVote = 1;
                    break;
                case Identities.Peter:
                    this.originalVote = 1.5;
                    break;
                case Identities.John:
                    this.originalVote = 1.5;
                    this.johnProtection = true;
                    break;
                case Identities.Laity:
                    this.originalVote = 1;
                    break;
                case Identities.Nicodemus:
                    this.originalVote = 0.5;
                    this.nicodemusProtection = true;
                    break;
            }*/
        /*this.changedVote = this.originalVote;*/
    }
}
