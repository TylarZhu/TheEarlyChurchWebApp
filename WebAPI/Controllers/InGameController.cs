using Domain.APIClass;
using Domain.HubInterface;
using Domain.Interfaces;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InGameController: Controller
    {
        private readonly IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> _hub;
        private readonly ICacheService redisCacheService;
        private readonly Explanation explanation;
        public InGameController(IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> hub,
            ICacheService redisCacheService)
        {
            _hub = hub;
            this.redisCacheService = redisCacheService;
            explanation = new Explanation();
        }

        [HttpPost("CreateAGame")]
        public async Task<ActionResult> createAGame(GameOnData data)
        {
            if(double.TryParse(data.christans, out double christans) && double.TryParse(data.judaisms, out double judaisms))
            {
                if(await redisCacheService.createAGameAndAssignIdentities(data.groupName, (int) christans, (int) judaisms))
                {
                    await _hub.Clients.Group(data.groupName).updatePlayersIdentities(
                        await redisCacheService.getAllUsers(data.groupName));
                    return Ok();   
                }
                else
                {
                    return BadRequest();
                }
            }
            return BadRequest();
        }
        [HttpGet("GetIdentitiesExplanation/{groupName}")]
        public async Task<ActionResult> getIdentitiesExplanation(string groupName)
        {
            List<OnlineUsers> onlineUsers = await redisCacheService.getAllUsers(groupName);
            foreach (OnlineUsers onlineUser in onlineUsers)
            {
                List<string>? ex = explanation.getExplanation(onlineUser.identity);
                if (ex == null)
                {
                    return BadRequest();
                }
                await _hub.Clients.Client(onlineUser.connectionId).IdentitiesExplanation(ex);
            }
            return Ok();
        }
    }
}
