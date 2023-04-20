using Domain.APIClass;
using Domain.HubInterface;
using Domain.Interfaces;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Infrastructure.DistributedCacheService;
using Domain.DBEntities;

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
        [HttpPost("IdentityViewingState/{groupName}/{playerName}")]
        public async Task<ActionResult> IdentityViewingState(string groupName, string playerName)
        {
            bool wait = await redisCacheService.waitOnOtherPlayersActionInGroup(groupName);
            // true, then finish view identity and wait for others.
            // False, then all user finished viewing ready to go to next step.
            if (wait)
            {
                OnlineUsers? user = await redisCacheService.getOneUserFromGroup(groupName, playerName);
                if(user != null)
                {
                    await _hub.Clients.Client(user.connectionId).finishedViewIdentityAndWaitOnOtherPlayers(true);
                }
                else
                {
                    return BadRequest();
                }
            }
            else
            {
                await _hub.Clients.Group(groupName).finishedViewIdentityAndWaitOnOtherPlayers(false);
                await _hub.Clients.Group(groupName).nextStep(new NextStep("discussing", new List<string>()));
            }
            return Ok();
        }

        [HttpGet("WhoIsDiscussing/{groupName}/{name}")]
        public async Task whoIsDiscussing(string groupName, string name)
        {
            OnlineUsers? currentUser = await redisCacheService.getOneUserFromGroup(groupName, name);
            // put the one who finished disscuss to wait
            if(currentUser != null)
            {
                string? connectionId = await redisCacheService.whoIsDiscussingNext(groupName, name);
                // if there is still user who did not discuss
                if (connectionId != null)
                {
                    await _hub.Clients.Client(currentUser.connectionId).currentUserInDiscusstion("FinishDisscussionWaitOthers");
                    await _hub.Clients.Client(connectionId).currentUserInDiscusstion("InDisscussion");
                }
                // all user finish discussing
                else
                {
                    await _hub.Clients.Group(groupName).currentUserInDiscusstion("AllUserFinishDisscussion");
                    await _hub.Clients.Group(groupName).nextStep(new NextStep("vote", new List<string>()));
                }
            }
            // name = "none", then put all people on wait just first user talking
            else
            {
                string? connectionId = await redisCacheService.whoIsDiscussingNext(groupName, name);
                
                if (connectionId != null)
                {
                    await _hub.Clients.GroupExcept(groupName, connectionId).currentUserInDiscusstion("FinishDisscussionWaitOthers");
                    await _hub.Clients.Client(connectionId).currentUserInDiscusstion("InDisscussion");
                }
            }
        }
    }
}
