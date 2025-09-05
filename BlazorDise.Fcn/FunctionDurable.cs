using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace BlazorDise.Fcn;

public static class FunctionDurable
{
    [Function(nameof(FunctionDurable) + "-" + nameof(RunOrchestrator))]
    public static async Task<List<string>> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var logger = context.CreateReplaySafeLogger(nameof(FunctionDurable) + "-" + nameof(RunOrchestrator));

        var input = context.GetInput<string>();
        logger.LogInformation($"Input Received: {input}");

        logger.LogInformation("Saying hello.");
        var outputs = new List<string>
        {
            // Replace name and input with values relevant for your Durable Functions Activity
            await context.CallActivityAsync<string>(nameof(FunctionDurable) + "-" + nameof(SayHello), "Tokyo"),
            await context.CallActivityAsync<string>(nameof(FunctionDurable) + "-" + nameof(SayHello), "Seattle"),
            await context.CallActivityAsync<string>(nameof(FunctionDurable) + "-" + nameof(SayHello), "London")
        };

        // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
        return outputs;
    }

    [Function(nameof(FunctionDurable) + "-" + nameof(SayHello))]
    public static string SayHello([ActivityTrigger] string name, FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(FunctionDurable) + "-" + nameof(SayHello));
        logger.LogInformation("Saying hello to {name}.", name);
        return $"Hello {name}!";
    }

    [Function(nameof(FunctionDurable) + "-" + nameof(HttpStart))]
    public static async Task<HttpResponseData> HttpStart([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req, [DurableClient] DurableTaskClient client, FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(FunctionDurable) + "-" + nameof(HttpStart));

        // Function input comes from the request content.
        var instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(FunctionDurable) + "-" + nameof(RunOrchestrator));

        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}