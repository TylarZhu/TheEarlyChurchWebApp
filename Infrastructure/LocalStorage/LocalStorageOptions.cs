using Domain.Common;
using System.Collections.Concurrent;
namespace Infrastructure.LocalStorage
{
    public static class LocalStorageOptions
    {
        // connection Id -> (Member, Player)
        private static ConcurrentDictionary<string, TupleMemberPlayer> connectionIdMemberPlayerMap = new ConcurrentDictionary<string, TupleMemberPlayer>();
        
        /*private ConcurrentDictionary<string, SynchronizedCollection<string>> groupNameConnectionIdsMap = new ConcurrentDictionary<string, SynchronizedCollection<string>>();*/
        public static bool addOnlineMembers(Member member) => 
            connectionIdMemberPlayerMap.TryAdd(member.connectionId, new TupleMemberPlayer(member, null!));

        public static bool assginPlayerToOnlineMember(string connectionId, Players newPlayer)
        {
            /*connectionIdMemberPlayerMap.AddOrUpdate(connectionId,
                _ => new TupleMemberPlayer(null!, null!),
                (connectionId, originalMemberPlayer) =>
                {
                    originalMemberPlayer.player = newPlayer;
                    return originalMemberPlayer;
                });
            */
            if(connectionIdMemberPlayerMap.TryGetValue(connectionId, out TupleMemberPlayer originalValue))
            {
                return connectionIdMemberPlayerMap.TryUpdate(connectionId, new TupleMemberPlayer(originalValue.member, newPlayer), originalValue);
            }
            return false;
        }

        /*public bool assignGroupName(string connectionId, string groupName)
        {
            if (connectionIdMemberPlayerMap.TryGetValue(connectionId, out TupleMemberPlayer originalValue))
            {   
                return connectionIdMemberPlayerMap.TryUpdate(connectionId, 
                    new TupleMemberPlayer(
                        new Member(originalValue.member.connectionId, originalValue.member.name).setGroupName(groupName),
                        originalValue.player),
                    originalValue);
            }
            return false;
        }*/

        public static Member getMember(string connectionId)
        {
            if(connectionIdMemberPlayerMap.TryGetValue(connectionId, out TupleMemberPlayer originalValue))
            {
                return originalValue.member;
            }
            return null!;
        }

        public static bool leaveGroup(string connectionId)
        {
            if (connectionIdMemberPlayerMap.TryGetValue(connectionId, out TupleMemberPlayer originalValue))
            {
                return connectionIdMemberPlayerMap.TryUpdate(connectionId,
                    new TupleMemberPlayer(
                        new Member(originalValue.member.connectionId, originalValue.member.name).setGroupName(""),
                        originalValue.player),
                    originalValue);
            }
            return false;
        }

        public static Players getPlayer(string connectionId)
        {
            if (connectionIdMemberPlayerMap.TryGetValue(connectionId, out TupleMemberPlayer originalValue))
            {
                return originalValue.player;
            }
            return null!;
        }

        public static bool disconnectOnlineMembers(string connectionId) =>
            connectionIdMemberPlayerMap.TryRemove(connectionId, out var player);

        /*public bool addMemberIntoGroup(string groupName, string connectionId)
        {
            if (groupNameConnectionIdsMap.TryGetValue(groupName, out SynchronizedCollection<string> originalValue))
            {
                SynchronizedCollection<string> newList = originalValue;
                newList.Add(connectionId);
                return groupNameConnectionIdsMap.TryUpdate(groupName, newList, originalValue);
            }
            return false;
        }
            groupNameConnectionIdsMap.AddOrUpdate(groupName, 
                (groupName) => new List<string> { connectionId }, 
                (groupName, orginalList) =>
                {
                    orginalList.Add(connectionId);
                    return orginalList;
                });

        public List<string> removeMemberFromGroup(string groupName, string connectionId) =>
            groupNameConnectionIdsMap.AddOrUpdate(groupName,
                (groupName) => null!,
                (groupName, orginalList) =>
                {
                    if (orginalList.Remove(connectionId))
                    {
                        return orginalList;
                    }
                    else
                    {
                        return null!;
                    }
                });*/
    }
}
