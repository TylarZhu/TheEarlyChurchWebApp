using Domain.APIClass;
using Domain.HubInterface;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI
{
    public abstract class PlayerGroupsHubBase : Hub<IPlayerGroupsHub>
    {
        public abstract Task onConntionAndCreateGroup(CreateNewUser createNewUser);
        public abstract Task leaveGroup(string gourpName, string name);
        public abstract Task reconnectionToGame(string groupName, string name);
        /*public abstract Task onDisconnect();*/
    }
}
