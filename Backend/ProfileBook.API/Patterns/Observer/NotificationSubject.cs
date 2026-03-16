namespace ProfileBook.API.Patterns.Observer
{
    public class NotificationSubject
    {
        private readonly List<INotificationObserver> observers = new();

        public void Attach(INotificationObserver observer)
        {
            observers.Add(observer);
        }

        public void Notify(string message)
        {
            foreach (var observer in observers)
            {
                observer.Update(message);
            }
        }
    }
}