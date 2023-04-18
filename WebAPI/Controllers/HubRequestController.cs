using Domain.HubInterface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Domain.DBEntities;
using Domain.Interfaces;
using Domain.APIClass;
using Domain.Common;
using Infrastructure.DBService;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HubRequestController : Controller
    {
        private readonly IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> _hub;
        private readonly ICacheService redisCacheService;

        public HubRequestController(IHubContext<PlayerGroupsHubBase, 
            IPlayerGroupsHub> hub,
            ICacheService redisCacheService)
        {
            _hub = hub;
            this.redisCacheService = redisCacheService;
        }

        [HttpPost("onlineUser")]
        public async Task<ActionResult<OnlineUsers>> postAOnlineUser([FromBody] CreateNewUser newUser)
        {

            if(!redisCacheService.CheckIfGroupExsits(newUser.groupName) && int.TryParse(newUser.maxPlayerInGroup, out int maxPlayerInGroup))
            {

                await redisCacheService.SetNewGroupAsync(newUser.groupName,
                    new GamesGroupsUsersMessages(newUser.groupName, maxPlayerInGroup),
                    new OnlineUsers(newUser.name, newUser.connectionId, true));
            }
            else
            {
                await redisCacheService.AddNewUsersToGroupAsync(newUser.groupName, 
                    new OnlineUsers(newUser.name, newUser.connectionId));
            }
            await redisCacheService.AddNewMessageIntoGroup(newUser.groupName, new Message("system", $"{newUser.name} join {newUser.groupName} group!"));

            await _hub.Clients.Group(newUser.groupName).updateOnlineUserList(
               await redisCacheService.getAllUsers(newUser.groupName));

            await _hub.Clients.Group(newUser.groupName).ReceiveMessages(
                await redisCacheService.getAllMessagesInGroup(newUser.groupName));

            OnlineUsers? groupLeader = await redisCacheService.getGroupLeaderFromGroup(newUser.groupName);

            if (groupLeader != null)
            {
                await _hub.Clients.Group(newUser.groupName).updateGroupLeader(
                 new CreateNewUser(
                     groupLeader.connectionId,
                     groupLeader.name,
                     newUser.groupName,
                     newUser.maxPlayerInGroup));
                GamesGroupsUsersMessages? group = await redisCacheService.GetGroupAsync(newUser.groupName);
                if(group != null)
                {
                    await _hub.Clients.Group(newUser.groupName).getMaxPlayersInGroup(group.maxPlayers.ToString());
                }
            }
            return CreatedAtAction(nameof(postAOnlineUser), new { id = newUser.connectionId }, newUser);
        }
        [HttpGet("getAllUsersInGroup/{groupName}")]
        public async Task<ActionResult<List<OnlineUsers>>> getAllUsersInGroup(string groupName)
        {
            List<OnlineUsers> usersInGroup = await redisCacheService.getAllUsers(groupName);
            if (usersInGroup == null)
            {
                return BadRequest();
            }

            return Ok(usersInGroup);
        }
        [HttpGet("checkIfUserNameInGroupDuplicate/{groupName}/{userName}")]
        public async Task<ActionResult<bool>> checkIfUserNameInGroupDuplicate(string groupName, string userName) =>
            await redisCacheService.checkIfUserNameInGroupDuplicate(groupName, userName);    
        [HttpGet("checkIfGroupIsFull/{groupName}")]
        public async Task<ActionResult<bool>> getIfGroupIsFull(string groupName) =>
            await redisCacheService.checkIfGroupIsFull(groupName);
        [HttpGet("GetMaxPlayersInGroup/{groupName}")]
        public async Task<ActionResult> getMaxPlayersInGroup(string groupName)
        {
            string max = await redisCacheService.getMaxPlayersInGroup(groupName);
            if(string.IsNullOrEmpty(max))
            {
                return BadRequest();
            }
            /*await _hub.Clients.Client().getMaxPlayersInGroup(max);*/
            return Ok();
        }
            
        [HttpGet("checkIfGroupExists/{groupName}")]
        public async Task<ActionResult<bool>> checkIfGroupExists(string groupName)
        {
            if(await redisCacheService.GetGroupAsync(groupName) != null)
            {
                return Ok(true);
            }
            else
            {
                return Ok(false);
            }
        }
        [HttpPost("assignNextGroupLeader/{groupName}/{nextGroupLeader}/{originalGroupLeader}")]
        public async Task<ActionResult> assignUserAsGroupLeader(string groupName, string nextGroupLeader, string originalGroupLeader)
        {
            OnlineUsers? groupLeader = await redisCacheService.assignUserAsGroupLeader(groupName, nextGroupLeader, originalGroupLeader);
            if(groupLeader != null)
            {
                await _hub.Clients.Group(groupName).updateGroupLeader(new CreateNewUser(groupLeader!.connectionId, groupLeader.name, groupName, "0"));
                return Ok();
            }
            return BadRequest();
        }
        [HttpDelete("userLeaveTheGame/{groupName}/{userName}/{gameOn}")]
        public async Task<ActionResult<OnlineUsers>> userLeaveTheGame(string groupName, string userName, bool gameOn = false)
        {
            OnlineUsers? leaveUser = await redisCacheService.removeUserAndAssignNextUserAsGroupLeader(groupName, userName);
            if (leaveUser != null)
            {
                await _hub.Clients.Group(groupName).updateOnlineUserList(
                await redisCacheService.getAllUsers(groupName));

                await redisCacheService.AddNewMessageIntoGroup(groupName, new Message("system", $"{userName} has leave {groupName} group!"));
                await _hub.Clients.Group(groupName).ReceiveMessages(
                    await redisCacheService.getAllMessagesInGroup(groupName));

                if (!await redisCacheService.isGroupEmpty(groupName) && leaveUser.groupLeader)
                {
                    OnlineUsers? onlineUser = await redisCacheService.getGroupLeaderFromGroup(groupName);
                    await _hub.Clients.Group(groupName).updateGroupLeader(new CreateNewUser(onlineUser!.connectionId, onlineUser.name, groupName, "0"));

                }
                if (gameOn)
                {
                    await _hub.Clients.Group(groupName).GameStop();
                }
                return Ok(leaveUser);
            }
            else
            {
                return NotFound(false);
            }
        }
    }
}
