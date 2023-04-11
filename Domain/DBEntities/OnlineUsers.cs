using Domain.Common;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
namespace Domain.DBEntities
{
    public class OnlineUsers
    {
        public string connectionId { get; set; } = null!;
        public string name { get; set; } = null!;
        public OnlineUsers(string name, string connectionId)
        {
            /*this.groupName = groupName;*/
            this.name = name;
            this.connectionId = connectionId;
        }
    }
}
