namespace ProfileBook.API.Patterns.Factory
{
    public class NotificationFactory
    {
        public static string CreateNotification(string type)
        {
            return type switch
            {
                "message" => "New message received",
                "post" => "New post created",
                _ => "Notification"
            };
        }
    }
}