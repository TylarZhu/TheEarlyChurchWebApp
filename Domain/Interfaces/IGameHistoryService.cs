using Domain.DBEntities;

namespace Domain.Interfaces
{
    public interface IGameHistoryService
    {
        Task<List<GameHistory>> getGameHistories();
        Task<GameHistory> getGameHistory(string id);
        Task createGameHistory(GameHistory gameHistory);
        Task updateGameHistory(string id, GameHistory gameHistory);
        Task deleteGameHistory(string id);
    }
}
