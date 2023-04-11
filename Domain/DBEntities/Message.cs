namespace Domain.DBEntities
{
    public class Message
    {
        public string type { get; set; }
        public string message { get; set; }
        public Message(string type, string message)
        {
            this.type = type;
            this.message = message;
        }
    }
}
