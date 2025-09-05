namespace BlazorDise.Shared;

public class StatusDto
{
    public string RowKey { get; set; } = string.Empty; // Azure Storage Queue Message ID
    public string? Data { get; set; }
    public string Status { get; set; } = string.Empty;
    public int AttemptCount { get; set; } = Constants.AttemptCountInitial; // Number of Function QueueTrigger attempts
    public int InitialWorkEffort { get; set; }
    public int CompletedWorkEffort { get; set; } // Number of work units completed so far (could be multiple within a Function QueueTrigger execution)
    public int RemainingWorkEffort() => Math.Max(InitialWorkEffort - CompletedWorkEffort, 0);
    public bool HasRemainingWork() => RemainingWorkEffort() > 0;
    public int CompletedPercent() => InitialWorkEffort <= 0 ? 0 : (int)Math.Round((double)CompletedWorkEffort / InitialWorkEffort * 100);
}