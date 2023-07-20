using Domain.HubInterface;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Domain.DBEntities;
using Domain.Interfaces;
using Domain.APIClass;
using Domain.Common;
using System.Collections.Concurrent;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HubRequestController : Controller
    {
        private readonly IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> _hub;
        private readonly ICacheService redisCacheService;
        public HubRequestController(IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> hub,
            ICacheService redisCacheService)
        {
            _hub = hub;
            this.redisCacheService = redisCacheService;
        }

        [HttpPost("onlineUser")]
        public async Task<ActionResult<Users>> postAOnlineUser([FromBody] CreateNewUser newUser)
        {
            // if there is no group, then create a new group and add the user as group leader.
            if (await redisCacheService.GetGroupAsync(newUser.groupName) == null && int.TryParse(newUser.maxPlayerInGroup, out int maxPlayerInGroup))
            {
                await redisCacheService.SetNewGroupAsync(newUser.groupName,
                    new GamesGroupsUsersMessages(newUser.groupName, maxPlayerInGroup),
                    new Users(newUser.name, newUser.connectionId, true));
                await redisCacheService.updateListAsync(newUser.groupName, new ConcurrentDictionary<string, double>());
            }
            // if there is group, then just add to the group.
            else
            {
                await redisCacheService.AddNewUsersToGroupAsync(newUser.groupName,
                    new Users(newUser.name, newUser.connectionId));
            }

            // update all other users in group that a new users has just joined.
            await _hub.Clients.Group(newUser.groupName).updateUserList(
               await redisCacheService.getAllUsers(newUser.groupName));

            Users? groupLeader = await redisCacheService.getGroupLeaderFromGroup(newUser.groupName);
            if (groupLeader != null)
            {
                await _hub.Clients.Group(newUser.groupName).updateGroupLeader(
                 new CreateNewUser(
                     groupLeader.connectionId,
                     groupLeader.name,
                     newUser.groupName,
                     newUser.maxPlayerInGroup));
                GamesGroupsUsersMessages? group = await redisCacheService.GetGroupAsync(newUser.groupName);
                if (group != null)
                {
                    // display the maximum player on every users' UI
                    await _hub.Clients.Client(newUser.connectionId).getMaxPlayersInGroup(group.maxPlayers.ToString());
                }
            }
            if(await redisCacheService.addConnectionIdToGroup(newUser.connectionId, newUser.groupName))
            {
                return CreatedAtAction(nameof(postAOnlineUser), new { id = newUser.connectionId }, newUser);
            }
            else
            {
                return BadRequest(nameof(postAOnlineUser));
            }
        }
        [HttpGet("getAllUsersInGroup/{groupName}")]
        public async Task<ActionResult<List<Users>>> getAllUsersInGroup(string groupName)
        {
            List<Users> usersInGroup = await redisCacheService.getAllUsers(groupName);
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
            if (string.IsNullOrEmpty(max))
            {
                return BadRequest();
            }
            /*await _hub.Clients.Client().getMaxPlayersInGroup(max);*/
            return Ok();
        }
        [HttpGet("checkIfGroupExists/{groupName}")]
        public async Task<ActionResult<bool>> checkIfGroupExists(string groupName)
        {
            if (await redisCacheService.GetGroupAsync(groupName) != null)
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
            Users? groupLeader = await redisCacheService.assignUserAsGroupLeader(groupName, nextGroupLeader, originalGroupLeader);
            if (groupLeader != null)
            {
                await _hub.Clients.Group(groupName).updateGroupLeader(new CreateNewUser(groupLeader!.connectionId, groupLeader.name, groupName, "0"));
                return Ok();
            }
            return BadRequest();
        }
        [HttpDelete("userLeaveTheGame/{groupName}/{userName}/{gameOn}")]
        public async Task<ActionResult<Users>> userLeaveTheGame(string groupName, string userName, bool gameOn = false)
        {
            Users? leaveUser = await redisCacheService.removeUserFromGroup(groupName, userName);
            if (leaveUser != null)
            {
                await _hub.Clients.Group(groupName).updateUserList(
                    await redisCacheService.getAllUsers(groupName));

                // if the group is not empty and the current leaving user is the group leader,
                // then update the next group leader to all users.
                if (!await redisCacheService.isGroupEmpty(groupName) && leaveUser.groupLeader)
                {
                    Users? onlineUser = await redisCacheService.getGroupLeaderFromGroup(groupName);
                    await _hub.Clients.Group(groupName).updateGroupLeader(new CreateNewUser(onlineUser!.connectionId, onlineUser.name, groupName, "0"));
                }
                if (gameOn)
                {
                    // TODO: GameStop();
                    await _hub.Clients.Group(groupName).GameStop();
                }
                return Ok(leaveUser);
            }
            else
            {
                return NotFound(false);
            }
        }
        /*[HttpDelete("userLeaveTheGameByConnectionId/{connectionId}")]
        public async Task<ActionResult> userLeaveTheGameByConnectionId(string connectionId)
        {
            Users? leaveUser = await redisCacheService.removeUserFromGroupByConnectionId(connectionId);
            string? removeUserGroupName = await redisCacheService.removeConnectionIdFromGroup(connectionId);
            if (leaveUser != null && removeUserGroupName != null)
            {
                await _hub.Clients.Group(removeUserGroupName).updateOnlineUserList(
                    await redisCacheService.getAllUsers(removeUserGroupName));
                if (!await redisCacheService.isGroupEmpty(removeUserGroupName) && leaveUser.groupLeader)
                {
                    Users? onlineUser = await redisCacheService.getGroupLeaderFromGroup(removeUserGroupName);
                    if(onlineUser != null)
                    {
                        await _hub.Clients.Group(removeUserGroupName)
                            .updateGroupLeader(new CreateNewUser(onlineUser.connectionId, onlineUser.name, removeUserGroupName, "0"));
                        return Ok();
                    }
                }
            }
            return BadRequest();
        }*/
    }
}
