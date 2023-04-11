namespace Domain.APIClass
{
    public class CreateNewUser
    {
        public string connectionId { get; set; } = null!;
        public string name { get; set; } = null!;
        public string groupName { get; set; } = null!;
        public string maxPlayerInGroup { get; set; } = null!;
    }
}
