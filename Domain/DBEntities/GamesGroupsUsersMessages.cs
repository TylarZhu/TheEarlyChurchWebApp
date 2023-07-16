using Domain.Common;
using System.Collections.Concurrent;

namespace Domain.DBEntities
{
    public class GamesGroupsUsersMessages
    {
        public string groupName { get; set; } = "";
        public int maxPlayers { get; set; } = 0;

        // name -> player info
        public ConcurrentDictionary<string, Users> onlineUsers { get; set; } = new ConcurrentDictionary<string, Users>();
        // name -> offline player info
        public ConcurrentDictionary<string, Users> offlineUsers { get; set; } = new ConcurrentDictionary<string, Users>();
        public ConcurrentDictionary<string, List<string>> history { get; set; } = new ConcurrentDictionary<string, List<string>>();
        public ConcurrentDictionary<string, int> numberofWaitingUser = new ConcurrentDictionary<string, int>();

        // this is for John's ability, which he cannot fire duplicate person.
        // be aware that this array does not contain John himself.
        public List<string> JohnFireList = new List<string>();
        public Users? WhoIsCurrentlyDiscussing = null;

        // game fields
        public bool gameInProgess = false;
        // this field will only have value when gameInProgess = true
        // keep record of what current status is.
        public string currentGameStatus = "";
        public int day { get; set; } = 1;
        public double judaismLostVote { get; set; } = 0.0;
        public double judaismTotalVote { get; set; } = 0.0;
        public double christianLostVote { get; set; } = 0.0;
        public double totalVotes { get; set; } = 0.0;
        public bool resetWaitingUserForGameHistory { get; set; } = false;
        public Users? lastNightExiledPlayer { get; set; } = null;

        public GamesGroupsUsersMessages(string groupName, int maxPlayers)
        {
            this.groupName = groupName;
            this.maxPlayers = maxPlayers;
            /*this.maxPlayers = 3;*/
            numberofWaitingUser.TryAdd("WaitingUsers", maxPlayers);
        }
        public List<Identities>? issuedIdentityCards()
        {
            List<Identities> identities = new List<Identities>();
            Random rand = new Random();

            identities.Add(Identities.Judas);
            identities.Add(Identities.Preist);
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
        /// <summary>
        /// Detect who wins in the current 
        /// </summary>
        /// <param name="totalVotes"></param>
        /// <returns>
        ///     0, No one wins.
        ///     1, christian wins.
        ///     2, judaism wins.
        /// </returns>
        public int winCondition()
        { 
            if (judaismLostVote / judaismTotalVote > 0.5)
            {
                return 1;
            }
            else if (christianLostVote / totalVotes > 0.35)
            {
                return 2;
            }
            else
            {
                return 0;
            }
        }
        public void cleanUp()
        {
            history.Clear();
            numberofWaitingUser.Clear();
            numberofWaitingUser.TryAdd("WaitingUsers", maxPlayers);
            JohnFireList.Clear();
            day = 1;
            judaismLostVote = 0.0;
            judaismTotalVote = 0.0;
            christianLostVote = 0.0;
            totalVotes = 0.0;
            lastNightExiledPlayer = null;
            resetWaitingUserForGameHistory = false;
            gameInProgess = false;
            currentGameStatus = "";
            WhoIsCurrentlyDiscussing = null;
        }
    }
}
