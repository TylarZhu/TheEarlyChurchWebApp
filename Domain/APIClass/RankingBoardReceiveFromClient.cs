using Domain.DBEntities;
namespace Domain.APIClass
{
    public class RankingBoardReceiveFromClient
    {
        public RankingBoard[] winners { get; set; } = null!;
    }
}
