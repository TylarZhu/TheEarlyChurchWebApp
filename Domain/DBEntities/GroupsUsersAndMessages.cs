using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.DBEntities
{
    public class GroupsUsersAndMessages
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? _groupId { get; set; }
        public string groupName { get; set; } = "";
        public int maxPlayers { get; set; } = 0;
        public List<OnlineUsers> onlineUsers { get; set; } = new List<OnlineUsers>();
        public List<Message> messages { get; set; } = new List<Message>();
        public GroupsUsersAndMessages(string groupName, int maxPlayers, OnlineUsers onlineUsers) 
        {
            this.groupName = groupName;
            this.maxPlayers = maxPlayers;
            this.onlineUsers.Add(onlineUsers);
        }
    }
}
