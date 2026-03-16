namespace ProfileBook.API.Patterns.Observer
{
    public interface INotificationObserver
    {
        void Update(string message);
    }
}