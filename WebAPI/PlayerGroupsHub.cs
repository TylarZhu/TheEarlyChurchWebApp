using Domain.APIClass;
using Domain.DBEntities;

namespace WebAPI
{
    internal class PlayerGroupsHub : PlayerGroupsHubBase
    {

        public override async Task onConntionAndCreateGroup(CreateNewUser createNewUser)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, createNewUser.groupName);
            await Clients.Caller.CreateNewUserJoinNewGroup(Context.ConnectionId, createNewUser.groupName, createNewUser.name, createNewUser.maxPlayerInGroup);
        }
        public override async Task leaveGroup(string gourpName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, gourpName);
        }
        /*public override async Task onDisconnect()
        {
            await Clients.Others.ReceiveMessage(new Message("system", $"{Context.ConnectionId} has disconnected!"));
        }*/
    }
}
