namespace Infrastructure.MongoDBSetUp
{
    public class RankingBoradDBSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
        public string RankingBoradCollectionName { get; set; } = null!;
    }
}
