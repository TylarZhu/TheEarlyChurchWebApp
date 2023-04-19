using Domain.Common;
using System.Collections.Concurrent;

namespace Domain.DBEntities
{
    public class GamesGroupsUsersMessages
    {
        public string groupName { get; set; } = "";
        public int maxPlayers { get; set; } = 0;

        // name -> player info
        public ConcurrentDictionary<string, OnlineUsers> onlineUsers { get; set; } = new ConcurrentDictionary<string, OnlineUsers>();
        // messages -> unuse info
        public ConcurrentDictionary<string, Message> messages { get; set; } = new ConcurrentDictionary<string, Message>();
        public ConcurrentBag<int> numberofWaitingUser = new ConcurrentBag<int>();

        // game fields
        public int numOfChristans { get; set; } = 0;
        public int numOfJudaisms { get; set; } = 0;
        public int day { get; set; } = 1;
        public double judaismLostVote { get; set; } = 0;
        public double judaismTotalVote { get; set; } = 0;
        public double totalVotes { get; set; } = 0;

        public GamesGroupsUsersMessages(string groupName, int maxPlayers)
        {
            this.groupName = groupName;
            this.maxPlayers = maxPlayers;
            numberofWaitingUser.Add(maxPlayers);
        }

        public List<Identities> issuedIdentityCards()
        {
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

            return identities;
            
        }
    }
}
