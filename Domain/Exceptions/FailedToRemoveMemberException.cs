using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Exceptions
{
    [Serializable]
    public class FailedToRemoveMemberException: Exception
    {
        public FailedToRemoveMemberException(string message): base(message) { }
    }
}
