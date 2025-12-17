namespace BlazorDise.Shared;

/// <summary>
/// Exception thrown to trigger Azure Functions queue retry mechanism.
/// This exception is used for testing and simulating retry scenarios.
/// </summary>
public class FunctionRetryException : Exception
{
    public FunctionRetryException() : base()
    {
    }

    public FunctionRetryException(string message) : base(message)
    {
    }

    public FunctionRetryException(string message, Exception innerException) : base(message, innerException)
    {
    }
}