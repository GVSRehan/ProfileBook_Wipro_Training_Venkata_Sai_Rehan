public interface IPostState
{
    string GetStatus();
}

public class PendingState : IPostState
{
    public string GetStatus() => "Pending";
}

public class ApprovedState : IPostState
{
    public string GetStatus() => "Approved";
}

public class RejectedState : IPostState
{
    public string GetStatus() => "Rejected";
}