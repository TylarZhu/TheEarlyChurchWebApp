using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.DBEntities
{
    public class RankingBoard
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string playerName { get; set; } = null!;
        // this player wins played as which identity?
        public string winAs { get; set; } = null!;
    }
}
