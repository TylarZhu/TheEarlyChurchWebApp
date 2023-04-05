using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.HubInterface
{
    public interface IPlayerGroupsHub
    {
        // type is either message or error
        Task ReceiveMessage(string message, string type);
    }
}
