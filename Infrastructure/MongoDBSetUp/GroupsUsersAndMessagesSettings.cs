using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.MongoDBSetUp
{
    public class GroupsUsersAndMessagesSettings: BaseSettings
    {
        public string GroupsUsersAndMessagesCollectionName { get; set; } = null!;
    }
}
