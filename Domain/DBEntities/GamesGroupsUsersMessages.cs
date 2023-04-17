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
        }
    }
}
