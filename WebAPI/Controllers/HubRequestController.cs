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

        public HubRequestController(IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> hub, 

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
            if (gourp == null)
            {
                if (int.TryParse(newUser.maxPlayerInGroup, out int maxPlayerInGroup))
                {
                    await groupsUsersAndMessagesService.addNewGroupAndFirstUser(
                        new GroupsUsersAndMessages(
                            newUser.groupName,
                            maxPlayerInGroup,
                            new OnlineUsers(newUser.name, newUser.connectionId)
                    ));
                }
            }
            else
            {
                await groupsUsersAndMessagesService.addNewUserIntoGroup(
                    newUser.groupName,
                    new OnlineUsers(newUser.name, newUser.connectionId
                ));
                
            }
            await groupsUsersAndMessagesService.addNewMessageIntoGroup(newUser.groupName, new Message("system", $"{newUser.name} join {newUser.groupName} group!"));

            await _hub.Clients.Group(newUser.groupName).updateOnlineUserList(
                await groupsUsersAndMessagesService.getUsersFromOneGroup(newUser.groupName));
            await _hub.Clients.Group(newUser.groupName).ReceiveMessages(
                await groupsUsersAndMessagesService.getMessagesFromOneGroup(newUser.groupName));

            return CreatedAtAction(nameof(postAOnlineUser), new { id = newUser.connectionId }, newUser);
        }

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

        [HttpDelete("userLeaveTheGame/{groupName}/{userName}")]
        public async Task<ActionResult> userLeaveTheGame(string groupName, string userName)
        {
            await groupsUsersAndMessagesService.deleteOneUserFromGroup(groupName, userName);
            await _hub.Clients.All.updateOnlineUserList(
                await groupsUsersAndMessagesService.getUsersFromOneGroup(groupName));

            await groupsUsersAndMessagesService.addNewMessageIntoGroup(groupName, new Message("system", $"{userName} has leave {groupName} group!"));
            await _hub.Clients.Group(groupName).ReceiveMessages(
                await groupsUsersAndMessagesService.getMessagesFromOneGroup(groupName));

            await groupsUsersAndMessagesService.isGroupEmpty(groupName);
            return Ok();
        }
    }
}
