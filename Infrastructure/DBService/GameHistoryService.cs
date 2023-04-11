using Domain.Interfaces;
using MongoDB.Driver;
using Infrastructure.MongoDBSetUp;
using Microsoft.Extensions.Options;
using Domain.DBEntities;

namespace Infrastructure.DBService
{
    public class GameHistoryService : IGameHistoryService
    {
        private readonly IMongoCollection<GameHistory> gameHistoryService;

        public GameHistoryService(IOptions<GameHistoryDBSettings> playerStoreDatabaseSettings)
        {
            var mongoClient = new MongoClient(playerStoreDatabaseSettings.Value.ConnectionString);
            var mongoDb = mongoClient.GetDatabase(playerStoreDatabaseSettings.Value.DatabaseName);
            gameHistoryService = mongoDb.GetCollection<GameHistory>(playerStoreDatabaseSettings.Value.GameHistoryCollectionName);
        }

        public async Task createGameHistory(GameHistory gameHistory) => 
            await gameHistoryService.InsertOneAsync(gameHistory);
  
        public async Task deleteGameHistory(string id) =>
            await gameHistoryService.DeleteOneAsync(x => x.gameId == id);

        public async Task<List<GameHistory>> getGameHistories() =>
            await gameHistoryService.Find(_ => true).ToListAsync();

        public async Task<GameHistory> getGameHistory(string id) =>
            await gameHistoryService.Find(x => x.gameId == id).FirstOrDefaultAsync();

        public async Task updateGameHistory(string id, GameHistory gameHistory) =>
            await gameHistoryService.ReplaceOneAsync(x => x.gameId == id, gameHistory);
    }
}
