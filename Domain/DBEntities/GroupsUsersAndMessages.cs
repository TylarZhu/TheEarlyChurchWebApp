using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Domain.Common;
using System.Collections.Concurrent;
using System.Xml.Serialization;

namespace Domain.DBEntities
{
    public class GroupsUsersAndMessages
    {
        // group fields
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? _groupId { get; set; }
        public string groupName { get; set; } = "";
        public int maxPlayers { get; set; } = 0;
        public List<OnlineUsers> onlineUsers { get; set; } = new List<OnlineUsers>();
        public List<Message> messages { get; set; } = new List<Message>();

        // game fields
        public int numOfChristans { get; set; } = 0;
        public int numOfJudaisms { get; set; } = 0;
        public int day { get; set; } = 1;
        public double judaismLostVote { get; set; } = 0;
        public double judaismTotalVote { get; set; } = 0;
        public double totalVotes { get; set; } = 0;


        public GroupsUsersAndMessages(string groupName, int maxPlayers) 
        {
            this.groupName = groupName;
            this.maxPlayers = maxPlayers;
        }
        public List<OnlineUsers> issuedIdentityCards()
        {
            /*ConcurrentDictionary<int, Identities> identities = new ConcurrentDictionary<int, Identities>();*/
            List<Identities> identities = new List<Identities>();
            Random rand = new Random();

            identities.Add(Identities.Judas);
            identities.Add(Identities.Scribes);
            identities.Add(Identities.Pharisee);
            identities.Add(Identities.Peter);
            identities.Add(Identities.John);
            identities.Add(Identities.Nicodemus);

            if (maxPlayers % 2 != 0)
            {
                int addChoice = rand.Next(0, 2);
                if (addChoice == 1)
                {
                    identities.Add(Identities.Judaism);
                }
                else
                {
                    identities.Add(Identities.Laity);
                }
            }

            for (int i = 0; i < maxPlayers / 2 - 3; i++)
            {
                identities.Add(Identities.Judaism);
            }
            for (int i = 0; i < maxPlayers / 2 - 3; i++)
            {
                identities.Add(Identities.Laity);
            }



            for (int i = identities.Count - 1; i > 0; i--)
            {
                int rad = rand.Next(0, i);
                Identities temp = identities[i];
                identities[i] = identities[rad];
                identities[rad] = temp;
            }

           
            for(int i = 0; i < onlineUsers.Count; i ++)
            {
                onlineUsers[i].identity = identities[i];
            }

            return onlineUsers;
        }
    }
}
