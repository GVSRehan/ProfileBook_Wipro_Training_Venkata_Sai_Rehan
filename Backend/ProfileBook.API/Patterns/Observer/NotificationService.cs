namespace ProfileBook.API.Patterns.Observer
{
    public class NotificationService : INotificationObserver
    {
        public void Update(string message)
        {
            Console.WriteLine($"Notification: {message}");
        }
    }
}