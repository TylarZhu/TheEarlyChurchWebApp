using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Common
{
    public class Member
    {
        public string connectionId { get; private set; } = null!;
        public string name { get; private set; } = null!;
        public string groupName { get; private set; } = null!;
        public string identifyId { get; private set; } = null!;
        public bool groupLeader { get; private set; } = false;

        public Member(string connectionId, string name, string groupName = "") 
        {
            this.connectionId = connectionId;
            this.name = name;
            this.groupName = groupName;
        }

        public Member setAsGroupLeader()
        {
            groupLeader = true;
            return this;
        }

        public Member setGroupName(string groupName)
        {
            this.groupName = groupName;
            return this;
        }
    }
}
