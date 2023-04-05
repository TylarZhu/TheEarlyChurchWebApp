using Domain.APIClass;
using Domain.Exceptions;
using Domain.HubInterface;
using Infrastructure.LocalStorage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("userJoinHub/[controller]")]
    public class UserJoinHubController: Controller
    {
        private readonly IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> _hub;

        public UserJoinHubController(IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> hub)
        {
            _hub = hub;
        }

        [HttpPost("messagesToAllUserInGroup/{groupName}")]
        public async Task<IActionResult> sendMessageToAllUsers(string groupName, [FromBody] string message)
        {
            await _hub.Clients.Group(groupName).ReceiveMessage(message, "message");
            return Ok();

        }
        /*[HttpPost("createNewUserAndGroup")]
        public async Task<IActionResult> createNewUserAndGroup(CreateNewUser createNewUser)
        {
            _hub.
            
        }*/
    }
}
