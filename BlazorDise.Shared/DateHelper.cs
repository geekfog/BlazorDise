namespace BlazorDise.Shared;

public static class DateHelper
{
    private static TimeZoneInfo? _selectedTimeZone;
    private static readonly object _lock = new object();

    public static void InitializeTimeZone(string? timeZoneId)
    {
        lock (_lock)
        {
            if (_selectedTimeZone != null) return; // Already initialized

            var targetTimeZoneId = string.IsNullOrWhiteSpace(timeZoneId) ? Constants.DefaultTimeZone : timeZoneId;

            try
            {
                _selectedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(targetTimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback to default if specified time zone is not found
                _selectedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(Constants.DefaultTimeZone);
            }
            catch (InvalidTimeZoneException)
            {
                // Fallback to default if specified time zone is invalid
                _selectedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(Constants.DefaultTimeZone);
            }
        }
    }

    private static TimeZoneInfo SelectedTimeZone
    {
        get
        {
            if (_selectedTimeZone == null)
            {
                lock (_lock)
                {
                    _selectedTimeZone ??= TimeZoneInfo.FindSystemTimeZoneById(Constants.DefaultTimeZone);
                }
            }
            return _selectedTimeZone;
        }
    }

    public static DateTimeOffset GetCentralTimeNow()
    {
        var convertedTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SelectedTimeZone);
        return new DateTimeOffset(convertedTime, SelectedTimeZone.GetUtcOffset(convertedTime));
    }

    public static string FormatDateTime(DateTimeOffset when)
    {
        var convertedTime = TimeZoneInfo.ConvertTime(when, SelectedTimeZone);
        return convertedTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // Helper method to get the current time zone display name
    public static string GetCurrentTimeZoneDisplayName()
    {
        return SelectedTimeZone.DisplayName;
    }

    // Helper method to get the current time zone ID
    public static string GetCurrentTimeZoneId()
    {
        return SelectedTimeZone.Id;
    }
}