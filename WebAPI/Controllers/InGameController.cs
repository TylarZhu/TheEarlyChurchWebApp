using Domain.APIClass;
using Domain.HubInterface;
using Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InGameController
    {
        private readonly IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> _hub;
        private readonly IGroupsUsersAndMessagesService groupsUsersAndMessagesService;
        public InGameController(IHubContext<PlayerGroupsHubBase, IPlayerGroupsHub> hub,
            IGroupsUsersAndMessagesService groupsUsersAndMessagesService)
        {
            _hub = hub;
            this.groupsUsersAndMessagesService = groupsUsersAndMessagesService;
        }

        [HttpPost("CreateAGame")]
        public async Task createAGame(GameOnData data)
        {

            if(int.TryParse(data.christans, out int christans) && int.TryParse(data.judaisms, out int judaisms))
            {
                await groupsUsersAndMessagesService.createAGame(data.groupName, christans, judaisms);
                await groupsUsersAndMessagesService.assginIdentities(data.groupName);
            }

        }
    }
}
