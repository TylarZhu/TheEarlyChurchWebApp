using Domain.DBEntities;
using Domain.Common;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Domain.Interfaces
{
    public interface IGroupsUsersAndMessagesService
    {
        // group operations
        Task addNewGroupAndFirstUser(GroupsUsersAndMessages groupsUsersAndMessages);
        Task<GroupsUsersAndMessages> getOneGroup(string groupName);
        Task<List<OnlineUsers>> getUsersFromOneGroup(string groupName);
        Task<List<Message>> getMessagesFromOneGroup(string groupName);
        Task<bool> isGroupEmpty(string groupName);
        Task<bool> checkIfUserNameInGroupDuplicate(string groupName, string name);
        Task<bool> checkIfGroupIsFull(string groupName);
        Task<OnlineUsers> getOneUserFromSpecificGroup(string groupName, string? name = "", string? connectionId = "");
        Task deleteOneUserFromGroup(string groupName, string name);
        Task addNewMessageIntoGroup(string groupName, Message newMessage);
        Task addNewUserIntoGroup(string groupName, OnlineUsers onlineUsers);
        Task<OnlineUsers> getGroupLeaderFromSpecificGroup(string groupName);
        Task nextFirstUserAssignAsGroupLeader(string groupName);

        // game operations
        Task createAGame(string groupName, int christans, int judaisms);
        Task assginIdentities(string groupName);
    }
}
