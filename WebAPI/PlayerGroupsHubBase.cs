using Domain.APIClass;
using Domain.HubInterface;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI
{
    public abstract class PlayerGroupsHubBase : Hub<IPlayerGroupsHub>
    {
        public abstract Task onConntionAndCreateGroup(CreateNewUser createNewUser);
        public abstract Task leaveGroup();
        public abstract Task onDisconnect();
    }
}
