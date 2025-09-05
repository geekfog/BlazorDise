namespace BlazorDise.Shared
{
    public static class Constants
    {
        public const string ConfigStorageAccount = "StorageAccountConnection";
        public const string ConfigSignalRAccount = "AzureSignalRConnectionString";
        public const string ConfigTimeZone = "ApplicationTimeZone";

        public const string DefaultTimeZone = "Central Standard Time"; // Default time zone if not configured        

        public const string StorageQueue = "blazordise-queue-items";
        public const string StorageQueuePoison = StorageQueue + "-poison";
        public const string StorageQueueHistory = StorageQueue + "-history";
        public const int MaxQueueItemAttempts = 1000;

        public const string StorageTable = "blazordisestatus";
        public const string StoragePartitionKey = "Status";

        public const int AttemptCountInitial = 1;

        public const string SignalRHubName = "statushub";
        public const string SignalRMethodName = "statusupdate";
        public const string SignalREndpoint = "negotiate";
        public const string SignalRHttpName = "signalrnegotiate";

        public const string SerilogTemplateNoDate = "{Level:u3}: {Message:lj}{NewLine}";
        public const string SerilogTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss} " + SerilogTemplateNoDate;
        public const string SerilogLogFilePath = "Logs/serilog-.txt"; // Path for Serilog log files
        public const int SerilogRollingIntervalDays = 7; // Number of days to keep log files
        public const string SeriogAzureTableName = "serilog"; // Azure Table Storage name for Serilog logs
    }
}