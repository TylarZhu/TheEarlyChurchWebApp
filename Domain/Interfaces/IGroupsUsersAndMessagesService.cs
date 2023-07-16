using Domain.DBEntities;
using Domain.Common;

namespace Domain.Interfaces
{
    public interface IGroupsUsersAndMessagesService
    {
        // group operations
        Task addNewGroupAndFirstUser(GroupsUsersAndMessages groupsUsersAndMessages);
        Task<GroupsUsersAndMessages> getOneGroup(string groupName);
        Task<List<Users>> getUsersFromOneGroup(string groupName);
       /* Task<List<Message>> getMessagesFromOneGroup(string groupName);*/
        Task<bool> isGroupEmpty(string groupName);
        Task<bool> checkIfUserNameInGroupDuplicate(string groupName, string name);
        Task<bool> checkIfGroupIsFull(string groupName);
        Task<Users> getOneUserFromSpecificGroup(string groupName, string? name = "", string? connectionId = "");
        Task deleteOneUserFromGroup(string groupName, string name);
/*        Task addNewMessageIntoGroup(string groupName, Message newMessage);*/
        Task addNewUserIntoGroup(string groupName, Users onlineUsers);
        Task<Users> getGroupLeaderFromSpecificGroup(string groupName);
        Task nextFirstUserAssignAsGroupLeader(string groupName);

        // game operations
        Task createAGame(string groupName, int christans, int judaisms);
        Task assginIdentities(string groupName);
    }
}
