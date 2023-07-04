using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.DBEntities
{
    public class Questions
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        // Q, meaning question discription
        // A .. D, choices
        // An, meaning answer
        public Dictionary<string, string> Question { get; set; } = null!;

        public Questions(Dictionary<string, string> Question) 
        {
            this.Question = Question;
        }

    }
}
