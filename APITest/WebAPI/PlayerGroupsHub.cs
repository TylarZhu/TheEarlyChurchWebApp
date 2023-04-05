using Domain.Common;
using Domain.Exceptions;
using Domain.APIClass;
using Infrastructure.LocalStorage;

namespace WebAPI
{
    internal class PlayerGroupsHub : PlayerGroupsHubBase
    {
        
        public override async Task onConntionAndCreateGroup(CreateNewUser createNewUser)
        {
            if (LocalStorageOptions.addOnlineMembers(new Member(Context.ConnectionId, createNewUser.username, createNewUser.groupName)))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, createNewUser.groupName);
                await Clients.Caller.ReceiveMessage(
                    $"{createNewUser.username} has been added to online members and created a group called {createNewUser.groupName}!", "message");
              
            }
            else
            {
                await Clients.Caller.ReceiveMessage($"{Context.ConnectionId} add to online members failed!", "error");
                throw new CannotAddToOnlineMemberCollectionException($"{Context.ConnectionId} add to online members failed!");
            }
        }
        /*public async Task joinGroup(string groupName)
        {
            
            if(localStorage.assignGroupName(Context.ConnectionId, groupName))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                await Clients.Group(groupName).SendAsync("Send", $"{localStorage.getMember(Context.ConnectionId).name} has joined the group {groupName}.");
            }
            else
            {
                await Clients.Caller.SendAsync("Send", $"{Context.ConnectionId} have failed to join group!");
                throw new CannotAssignMemberToGroupException($"{Context.ConnectionId} have failed to join group!");
            }
        }*/
        public override async Task leaveGroup()
        {
            Member member = LocalStorageOptions.getMember(Context.ConnectionId);
            if (member != null)
            {
                if (LocalStorageOptions.leaveGroup(Context.ConnectionId))
                {
                    await Clients.Group(member.groupName).ReceiveMessage($"{member.name} have leave the group!", "message");
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, member.groupName);
                }
                else
                {
                    await Clients.Group(member.groupName).ReceiveMessage($"{member.name} have failed to leave the group!!", "error");
                    throw new MemberFailedToLeaveGroupException($"{member.name} have failed to leave the group!");
                }
            }
            else
            {
                throw new CannotFindMemberException($"{Context.ConnectionId} cannot be found!");
            }
        }
        public override async Task onDisconnect()
        {
            if (LocalStorageOptions.disconnectOnlineMembers(Context.ConnectionId))
            {
                await Clients.Others.ReceiveMessage($"{Context.ConnectionId} has disconnected!", "message");
            }
            else
            {
                await Clients.Others.ReceiveMessage($"{Context.ConnectionId} failed to be diconnected!", "error");
                throw new FailedToRemoveMemberException($"{Context.ConnectionId} failed to be diconnected!");
            }
        }
    }
}
