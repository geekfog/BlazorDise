using Azure;
using BlazorDise.Shared;

namespace BlazorDise.Fcn;

public class StatusTableEntity : StatusDto, Azure.Data.Tables.ITableEntity
{
    public string PartitionKey { get; set; } = Constants.StoragePartitionKey;
    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    public DateTimeOffset FirstTimeWorkStarted { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastTimeWorkStarted { get; set; } = DateTimeOffset.UtcNow;
    public string? Message { get; set; }
    
    public bool CancelOperation { get; set; } = false;
}