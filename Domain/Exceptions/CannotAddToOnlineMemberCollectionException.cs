using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Exceptions
{
    [Serializable]
    public class CannotAddToOnlineMemberCollectionException: Exception
    {
        public CannotAddToOnlineMemberCollectionException() { }
        public CannotAddToOnlineMemberCollectionException(string message)
        : base(message) { }
    }
}
