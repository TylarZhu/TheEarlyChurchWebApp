using Domain.DBEntities;
using Domain.Interfaces;
using MongoDB.Driver;
using Microsoft.Extensions.Options;
using Infrastructure.MongoDBSetUp;

namespace Infrastructure.DBService
{
    public class RankingBoardService : IRankingBoardService
    {
        private readonly IMongoCollection<RankingBoard> rankingBoardService;
        public RankingBoardService(IOptions<RankingBoradDBSettings> rankingBoradDBSettings)
        {
            var mongoClient = new MongoClient(rankingBoradDBSettings.Value.ConnectionString);
            var mongoDb = mongoClient.GetDatabase(rankingBoradDBSettings.Value.DatabaseName);
            rankingBoardService = mongoDb.GetCollection<RankingBoard>(rankingBoradDBSettings.Value.RankingBoradCollectionName);
        }
        public async Task createRankingBoard(RankingBoard newRanking) =>
            await rankingBoardService.InsertOneAsync(newRanking);

        public async Task deleteRankingBoard(string id) =>
            await rankingBoardService.DeleteOneAsync(x => x.Id == id);

        public async Task<RankingBoard> getRankingBoard(string id) =>
            await rankingBoardService.Find(x => x.Id == id).FirstOrDefaultAsync();

        public async Task<List<RankingBoard>> getRankingBoards() =>
            await rankingBoardService.Find(_ => true).ToListAsync();

        public async Task updateRankingBoard(string id, RankingBoard newRanking) =>
            await rankingBoardService.ReplaceOneAsync(x => x.Id == id, newRanking);
    }
}
