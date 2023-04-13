namespace Domain.APIClass
{
    public class CreateNewUser
    {
        public string connectionId { get; set; } = null!;
        public string name { get; set; } = null!;
        public string groupName { get; set; } = null!;
        public string maxPlayerInGroup { get; set; } = null!;
        public CreateNewUser(string connectionId, string name, string groupName, string maxPlayerInGroup)
        {
            this.connectionId = connectionId;
            this.name = name;
            this.groupName = groupName;
            this.maxPlayerInGroup = maxPlayerInGroup;
        }
    }
}
