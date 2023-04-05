namespace Domain.Common
{
    public class GameCommonData
    {
        public int totalPlayers { get; private set; } = 0;
        public int day { get; private set; } = 1;
        public double judaismLostVote { get; private set; } = 0;
        public double judaismTotalVote { get; private set; } = 0;
        public Dictionary<string, Players> johnFireList { get; private set; } = new Dictionary<string, Players>();
    }
}
