using Domain.APIClass;
using Domain.Common;

namespace Domain.HubInterface
{
    public interface IPlayerGroupsHub
    {
        // type is either message or error
        Task ReceiveMessages(List<Message> messages);
        Task CreateNewUserJoinNewGroup(string connectionId, string groupName, string username, string numberOfPlayers);
        Task updateOnlineUserList(List<OnlineUsers> onlineUsers);
        Task leaveGroupUserConnectionId(string connectionId);
        Task updateGroupLeader(CreateNewUser users);
        Task GameStop();
    }
}
