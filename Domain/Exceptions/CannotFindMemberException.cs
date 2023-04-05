using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Exceptions
{
    [Serializable]
    public class CannotFindMemberException: Exception
    {
        public CannotFindMemberException(string message): base(message) { }
    }
}
