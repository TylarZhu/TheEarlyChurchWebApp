namespace Domain.Common
{
    public class TupleMemberPlayer
    {
        public Member member { get; set; }
        public Players player { get; set; }
        public TupleMemberPlayer(Member member, Players player)
        {
            this.member = member;
            this.player = player;
        }
    }
}
