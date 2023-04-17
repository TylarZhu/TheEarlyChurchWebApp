using Domain.Common;
using System.Collections.Concurrent;

namespace Infrastructure.DistributedCacheService
{
    public static class UpdateHelper
    {
        public static bool TryUpdateCustom(
            this ConcurrentDictionary<string, OnlineUsers> dict,
            string key,
            Func<OnlineUsers, OnlineUsers> updateFactory)
        {
            while (dict.TryGetValue(key, out OnlineUsers? curValue))
            {
                if (dict.TryUpdate(key, updateFactory(curValue), curValue))
                    return true;
                // if we're looping either the key was removed by another thread,
                // or another thread changed the value, so we start again.
            }
            return false;
        }
    }
}
