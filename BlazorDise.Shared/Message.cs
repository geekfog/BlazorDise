using System.Text.Json.Serialization;

namespace BlazorDise.Shared;

public class Message
{
    private int _waitPeriod;

    [JsonPropertyName("when")]
    public DateTime When { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("data")]
    public string? Data { get; set; }

    [JsonPropertyName("waitPeriod")]
    public int WaitPeriod { get => _waitPeriod; set => _waitPeriod = (value < 0 ? 0 : value); }

    [JsonPropertyName("raiseException")]
    public bool RaiseException { get; set; } = false;

    [JsonPropertyName("raiseDurableFunction")]
    public bool RaiseDurableFunction { get; set; } = false;
}