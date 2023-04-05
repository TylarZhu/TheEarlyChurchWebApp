using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.APIClass
{
    public class CreateNewUser
    {
        public string username { get; set; } = null!;
        public string groupName { get; set; } = null!;
        public string numberOfPlayers { get; set; } = null!;
    }
}
