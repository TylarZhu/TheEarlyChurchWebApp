using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Exceptions
{
    [Serializable]
    public class CannotAssignMemberToGroupException : Exception
    {
        public CannotAssignMemberToGroupException(string message)
        : base(message) { }
    }
}
