﻿using Domain.DBEntities;
using Domain.Interfaces;
using Infrastructure.MongoDBSetUp;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Domain.Common;

namespace Infrastructure.DBService
{
    public class GroupsUsersAndMessagesService: IGroupsUsersAndMessagesService
    {
        private readonly IMongoCollection<GroupsUsersAndMessages> groupsUsersAndMessagesService;

        // group operations
        public GroupsUsersAndMessagesService(IOptions<GroupsUsersAndMessagesSettings> groupsUsersAndMessagesSettings)
        {
            var mongoClient = new MongoClient(groupsUsersAndMessagesSettings.Value.ConnectionString);
            var mongoDb = mongoClient.GetDatabase(groupsUsersAndMessagesSettings.Value.DatabaseName);
            groupsUsersAndMessagesService = mongoDb.GetCollection<GroupsUsersAndMessages>(groupsUsersAndMessagesSettings.Value.GroupsUsersAndMessagesCollectionName);
        }
        public async Task addNewGroupAndFirstUser(GroupsUsersAndMessages groupsUsersAndMessages) =>
            await groupsUsersAndMessagesService.InsertOneAsync(groupsUsersAndMessages);
        public async Task<GroupsUsersAndMessages> getOneGroup(string groupName) =>
            await groupsUsersAndMessagesService.Find(x => x.groupName == groupName).FirstOrDefaultAsync();
        public async Task<List<Users>> getUsersFromOneGroup(string groupName) =>
             await groupsUsersAndMessagesService.Find(x => x.groupName == groupName).Project(x => x.onlineUsers).FirstOrDefaultAsync();
        /*public async Task<List<Message>> getMessagesFromOneGroup(string groupName) =>
            await groupsUsersAndMessagesService.Find(x => x.groupName == groupName).Project(x => x.messages).FirstOrDefaultAsync();*/
        public async Task<bool> isGroupEmpty(string groupName)
        {
            GroupsUsersAndMessages group = await getOneGroup(groupName);
            if(group.onlineUsers.Count == 0)
            {
                await groupsUsersAndMessagesService.DeleteOneAsync(x => x.groupName == groupName);
                return true;
            }
            return false;
        }
        public async Task<Users> getOneUserFromSpecificGroup(string groupName, string? name = "", string? connectionId = "")
        {
            BsonDocument results;

            if (string.IsNullOrEmpty(name))
            {
                results = await groupsUsersAndMessagesService.Aggregate()
                    .Match(new BsonDocument { { "groupName", groupName } })
                    .Unwind(x => x.onlineUsers)
                    .Match(new BsonDocument { { "onlineUsers.connectionId", connectionId } })
                    .Project(new BsonDocument { { "onlineUsers", 1 } })
                    .FirstOrDefaultAsync();
            }
            else
            {
                results = await groupsUsersAndMessagesService.Aggregate()
                    .Match(new BsonDocument { { "groupName", groupName } })
                    .Unwind(x => x.onlineUsers)
                    .Match(new BsonDocument { { "onlineUsers.name", name } })
                    .Project(new BsonDocument { { "onlineUsers", 1 } })
                    .FirstOrDefaultAsync();
            }
            return BsonSerializer.Deserialize<Users>(results[1].ToJson());
        }
        public async Task<Users> getGroupLeaderFromSpecificGroup(string groupName)
        {
            var results = await groupsUsersAndMessagesService.Aggregate()
                .Match(new BsonDocument { { "groupName", groupName } })
                .Unwind(x => x.onlineUsers)
                .Match(new BsonDocument { { "onlineUsers.groupLeader", true } })
                .Project(new BsonDocument { { "onlineUsers", 1 } })
                .FirstOrDefaultAsync();
            return BsonSerializer.Deserialize<Users>(results[1].ToJson());
        }
        public async Task<bool> checkIfUserNameInGroupDuplicate(string groupName, string name)
        {
            var filter = Builders<GroupsUsersAndMessages>.Filter.And(
                Builders<GroupsUsersAndMessages>.Filter.Eq(x => x.groupName, groupName),
                Builders<GroupsUsersAndMessages>.Filter.ElemMatch(
                    x => x.onlineUsers,
                    Builders<Users>.Filter.Eq(x => x.name, name)
                ));
            if (await groupsUsersAndMessagesService.Find(filter).FirstOrDefaultAsync() == null)
            {
                return false;
            }
            return true;
        }
        public async Task deleteOneUserFromGroup(string groupName, string name)
        {
            var filter = Builders<GroupsUsersAndMessages>.Filter.And(
                Builders<GroupsUsersAndMessages>.Filter.Eq(x => x.groupName, groupName),
                Builders<GroupsUsersAndMessages>.Filter.ElemMatch(
                    x => x.onlineUsers,
                    Builders<Users>.Filter.Eq(x => x.name, name)
                ));
            var update = Builders<GroupsUsersAndMessages>.Update.PullFilter(
                x => x.onlineUsers, 
                Builders<Users>.Filter.Eq(x => x.name, name));
            await groupsUsersAndMessagesService.FindOneAndUpdateAsync(filter, update);
        }
/*        public async Task addNewMessageIntoGroup(string groupName, Message newMessage) =>
            await groupsUsersAndMessagesService.FindOneAndUpdateAsync(
                Builders<GroupsUsersAndMessages>.Filter.Eq(x => x.groupName, groupName),
                Builders<GroupsUsersAndMessages>.Update.Push(x => x.messages, newMessage)
            );*/
        public async Task addNewUserIntoGroup(string groupName, Users onlineUsers) =>
            await groupsUsersAndMessagesService.FindOneAndUpdateAsync(
                Builders<GroupsUsersAndMessages>.Filter.Eq(x => x.groupName, groupName),
                Builders<GroupsUsersAndMessages>.Update.Push(x => x.onlineUsers, onlineUsers)
            );
        public async Task<bool> checkIfGroupIsFull(string groupName)
        {
            GroupsUsersAndMessages group = await getOneGroup(groupName);
            return group.onlineUsers.Count >= group.maxPlayers;
        }
        public async Task nextFirstUserAssignAsGroupLeader(string groupName)
        {
            var filter = Builders<GroupsUsersAndMessages>.Filter.And(
                Builders<GroupsUsersAndMessages>.Filter.Eq(x => x.groupName, groupName),
                Builders<GroupsUsersAndMessages>.Filter.ElemMatch(
                    x => x.onlineUsers,
                    Builders<Users>.Filter.Eq(x => x.groupLeader, false)
                ));
            var update = Builders<GroupsUsersAndMessages>.Update.Set("onlineUsers.$.groupLeader", true);
            await groupsUsersAndMessagesService.UpdateOneAsync(filter, update);
        }

        // game operations
        public async Task createAGame(string groupName, int christans, int judaisms) =>
            await groupsUsersAndMessagesService.UpdateOneAsync(x => x.groupName == groupName,
                Builders<GroupsUsersAndMessages>.Update.Set(x => x.numOfChristans, christans)
                    .Set(x => x.numOfJudaisms, judaisms));
        public async Task assginIdentities(string groupName)
        {
            GroupsUsersAndMessages group = await groupsUsersAndMessagesService.Find(x => x.groupName == groupName).FirstOrDefaultAsync();
            List<Users> users = group.issuedIdentityCards();
            var update = Builders<GroupsUsersAndMessages>.Update.Set("onlineUsers", users);
            await groupsUsersAndMessagesService.UpdateOneAsync(x => x.groupName == groupName, update);
        }
    }
}