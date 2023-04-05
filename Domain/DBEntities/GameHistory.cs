using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


namespace Domain.DBEntities
{
    public class GameHistory
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? gameId { get; set; }
        public string GameTime { get; set; } = null!;
        // Day number -> What happened.
        public Dictionary<string, string[]> playBack { get; set; } = null!;
    }
}
