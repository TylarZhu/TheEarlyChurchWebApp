using Domain.Common;
namespace Domain.DBEntities
{
    public class OnlineUsers
    {
        public string connectionId { get; set; } = null!;
        public string name { get; set; } = null!;
        public Players player { get; set; } = null!;
        public OnlineUsers(string name, string connectionId)
        {
            /*this.groupName = groupName;*/
            this.name = name;
            this.connectionId = connectionId;
        }
        public void assginPlayer(Players players)
        {
            this.player = players;
        }
    }
}
