namespace Infrastructure.MongoDBSetUp
{

    // The preceding BookStoreDatabaseSettings class is used to store the appsettings.json file's BookStoreDatabase property values.
    // The JSON and C# property names are named identically (一样) to ease the mapping process.
    public class GameHistoryDBSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
        public string GameHistoryCollectionName { get; set; } = null!;
    }
}
