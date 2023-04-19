using Domain.Common;
using System.Collections.Concurrent;

namespace Infrastructure.DistributedCacheService
{
    public static class UpdateHelper
    {
        public static bool TryUpdateCustom<TKey, TValue>(
            this ConcurrentDictionary<TKey, TValue> dict,
            TKey key,
            Func<TValue, TValue> updateFactory)
        {
            while (dict.TryGetValue(key, out TValue? curValue))
            {
                if (dict.TryUpdate(key, updateFactory(curValue!), curValue))
                    return true;
                // if we're looping either the key was removed by another thread,
                // or another thread changed the value, so we start again.
            }
            return false;
        }
    }
}
