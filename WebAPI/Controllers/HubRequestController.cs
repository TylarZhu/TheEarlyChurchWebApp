using Domain.HubInterface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Domain.DBEntities;
using Domain.Interfaces;
using Domain.APIClass;
using Infrastructure.DBService;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HubRequestController : Controller
    {
        private readonly IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> _hub;
        private readonly IGroupsUsersAndMessagesService groupsUsersAndMessagesService;

        public HubRequestController(IHubContext<PlayerGroupsHubBase, 
            IPlayerGroupsHub> hub,
            IGroupsUsersAndMessagesService groupsUsersAndMessagesService)
        {
            _hub = hub;
            this.groupsUsersAndMessagesService = groupsUsersAndMessagesService;
        }

        /*[HttpPost("messagesToAllUserInGroup/{groupName}")]
        public async Task<IActionResult> sendMessageToAllUsers(string groupName, [FromBody] string message)
        {
            await _hub.Clients.Group(groupName).ReceiveMessage(new Message("system", message));
            return Ok();

        }*/
        [HttpPost("onlineUser")]
        public async Task<IActionResult> postAOnlineUser([FromBody] CreateNewUser newUser)
        {
            GroupsUsersAndMessages gourp = await groupsUsersAndMessagesService.getOneGroup(newUser.groupName);
            OnlineUsers groupLeader = null!;
            if (gourp == null)
            {
                if (int.TryParse(newUser.maxPlayerInGroup, out int maxPlayerInGroup))
                {
                    groupLeader = new OnlineUsers(newUser.name, newUser.connectionId, true);
                    await groupsUsersAndMessagesService.addNewGroupAndFirstUser(
                        new GroupsUsersAndMessages(
                            newUser.groupName,
                            maxPlayerInGroup,
                            groupLeader
                    ));
                }
            }
            else
            {
                await groupsUsersAndMessagesService.addNewUserIntoGroup(
                    newUser.groupName,
                    new OnlineUsers(newUser.name, newUser.connectionId)
                );

                groupLeader = await groupsUsersAndMessagesService.getGroupLeaderFromSpecificGroup( newUser.groupName );
            }
            await groupsUsersAndMessagesService.addNewMessageIntoGroup(newUser.groupName, new Message("system", $"{newUser.name} join {newUser.groupName} group!"));

            await _hub.Clients.Group(newUser.groupName).updateOnlineUserList(
                await groupsUsersAndMessagesService.getUsersFromOneGroup(newUser.groupName));
            await _hub.Clients.Group(newUser.groupName).ReceiveMessages(
                await groupsUsersAndMessagesService.getMessagesFromOneGroup(newUser.groupName));
            
            await _hub.Clients.Group(newUser.groupName).updateGroupLeader(
                new CreateNewUser(
                    groupLeader.connectionId, 
                    groupLeader.name, 
                    newUser.groupName, 
                    newUser.maxPlayerInGroup
            ));
            
            return CreatedAtAction(nameof(postAOnlineUser), new { id = newUser.connectionId }, newUser);
        }
        /*[HttpPost[""]]*/
        [HttpGet("getAllUsersInGroup/{groupName}")]
        public async Task<ActionResult<List<OnlineUsers>>> getAllUsersInGroup(string groupName)
        {
            List<OnlineUsers> usersInGroup = await groupsUsersAndMessagesService.getUsersFromOneGroup(groupName);
            if (usersInGroup == null)
            {
                return BadRequest();
            }

            return Ok(usersInGroup);
        }
        [HttpGet("checkIfUserNameInGroupDuplicate/{groupName}/{userName}")]
        public async Task<ActionResult<bool>> checkIfUserNameInGroupDuplicate(string groupName, string userName) =>
            await groupsUsersAndMessagesService.checkIfUserNameInGroupDuplicate(groupName, userName);    
        [HttpGet("checkIfGroupIsFull/{groupName}")]
        public async Task<ActionResult<bool>> getIfGroupIsFull(string groupName) =>
            await groupsUsersAndMessagesService.checkIfGroupIsFull(groupName);
        [HttpGet("checkIfGroupExists/{groupName}")]
        public async Task<ActionResult<bool>> checkIfGroupExists(string groupName)
        {
            if(await groupsUsersAndMessagesService.getOneGroup(groupName) != null)
            {
                return Ok(true);
            }
            else
            {
                return Ok(false);
            }
        }
        [HttpGet("checkIfUserIsGroupLeader/{groupName}/{userName}")]
        public async Task<ActionResult<bool>> checkIfUserIsGroupLeader(string groupName, string username)
        {
            OnlineUsers user  = await groupsUsersAndMessagesService.getOneUserFromSpecificGroup(groupName, username);
            return Ok(user.groupLeader);
        }
        [HttpDelete("userLeaveTheGame/{groupName}/{userName}")]
        public async Task<ActionResult<OnlineUsers>> userLeaveTheGame(string groupName, string userName)
        {
            OnlineUsers leaveUser = await groupsUsersAndMessagesService.getOneUserFromSpecificGroup(groupName, userName);
            await groupsUsersAndMessagesService.deleteOneUserFromGroup(groupName, userName);
            await _hub.Clients.Group(groupName).updateOnlineUserList(
                await groupsUsersAndMessagesService.getUsersFromOneGroup(groupName));

            await groupsUsersAndMessagesService.addNewMessageIntoGroup(groupName, new Message("system", $"{userName} has leave {groupName} group!"));
            await _hub.Clients.Group(groupName).ReceiveMessages(
                await groupsUsersAndMessagesService.getMessagesFromOneGroup(groupName));

            // if there are still users in group and the leaving user is the group leader,
            // then the next user in group will be the group leader.
            if(!await groupsUsersAndMessagesService.isGroupEmpty(groupName) && leaveUser.groupLeader)
            {
                await groupsUsersAndMessagesService.nextFirstUserAssignAsGroupLeader(groupName);
                OnlineUsers onlineUser = await groupsUsersAndMessagesService.getGroupLeaderFromSpecificGroup(groupName);
                await _hub.Clients.Group(groupName).updateGroupLeader(new CreateNewUser(onlineUser.connectionId, onlineUser.name, groupName, "0"));

            }
            return Ok(leaveUser);
        }
    }
}
