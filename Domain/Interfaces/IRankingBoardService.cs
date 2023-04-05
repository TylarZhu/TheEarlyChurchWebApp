using Domain.DBEntities;

namespace Domain.Interfaces
{
    public interface IRankingBoardService
    {
        Task<List<RankingBoard>> getRankingBoards();
        Task<RankingBoard> getRankingBoard(string id);
        Task createRankingBoard(RankingBoard newRanking);
        Task updateRankingBoard(string id, RankingBoard newRanking);
        Task deleteRankingBoard(string id);
    }
}
