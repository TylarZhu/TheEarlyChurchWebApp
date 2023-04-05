using Domain.Common;

namespace Domain.Common
{
    public class Players
    {
        public string name { get; private set; }
        public Identities identity { get; private set; }
        public int number { get; private set; }
        public double originalVote { get; private set; } // 最原始的权重
        public double changedVote { get; private set; } // 可以增加可以减少
        public bool johnProtection { get; private set; } = false;
        public bool nicodemusProtection { get; private set; } = false;
        public bool priest { get; private set; } = false;
        public bool rulerOfTheSynagogue { get; private set; } = false;
        public bool check { get; private set; } = false;
        public bool inGame { get; private set; } = true;
        public bool disempowering { get; private set; } = false;




        public Players(Identities identity, string name = "", int number = -1, bool protection = false)
        {
            this.name = name;
            this.identity = identity;
            this.number = number;
            switch (this.identity)
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
            }
            this.changedVote = this.originalVote;
        }
        public void setChangeVote(double vote)
        {
            this.changedVote = vote;
        }
        /// <summary>
        /// Change John protection to false.
        /// </summary>
        public void changeJohnProtection()
        {
            this.johnProtection = false;
        }
        /// <summary>
        /// Change Nicodemus protection to false.
        /// </summary>
        public void changeNicodemusProtection()
        {
            this.nicodemusProtection = false;
        }
        public void setPriest()
        {
            this.priest = true;
        }
        /// <summary>
        /// Change the In Game status of this player.
        /// False means out of game, True means still in the game.
        /// </summary>
        /// <param name="inGameOrNot">  </param>
        public void exileStatus(bool inGameOrNot)
        {
            this.inGame = inGameOrNot;
        }
        public void setRulerOfTheSynagogue()
        {
            this.rulerOfTheSynagogue = true;
        }
        public void setDisempowering()
        {
            this.disempowering = true;
        }
    }
}
