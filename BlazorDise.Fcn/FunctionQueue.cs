using System.Numerics;
using Azure.Data.Tables;
using Azure.Storage.Queues.Models;
using BlazorDise.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.SignalR.Management;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using static BlazorDise.Shared.DateHelper;

namespace BlazorDise.Fcn;

public class FunctionQueue
{
    private readonly ILogger<FunctionQueue> _logger;
    private readonly IConfiguration _configuration;
    private readonly ServiceManager _signalRServiceManager;
    private IServiceHubContext? _hubContext;

    public FunctionQueue(ILogger<FunctionQueue> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Initialize time zone from configuration
        var timeZoneId = configuration[Constants.ConfigTimeZone];
        DateHelper.InitializeTimeZone(timeZoneId);
        _logger.LogInformation($"Time zone initialized to: {DateHelper.GetCurrentTimeZoneDisplayName()} ({DateHelper.GetCurrentTimeZoneId()})");

        // Initialize SignalR Service Manager
        var signalRConnectionString = configuration[Constants.ConfigSignalRAccount];
        _signalRServiceManager = new ServiceManagerBuilder()
            .WithOptions(option => { option.ConnectionString = signalRConnectionString; })
            .BuildServiceManager();
    }

    [Function(nameof(FunctionQueue) + "-" +nameof(RunTrigger))]
    public async Task RunTrigger([QueueTrigger(Constants.StorageQueue, Connection = Constants.ConfigStorageAccount)] QueueMessage message, 
        [DurableClient] DurableTaskClient durableClient,
        FunctionContext context)
    {
        // If no exception is thrown, the message will be deleted from the queue automatically and deemed as completed.

        var invocationId = context.InvocationId;
        var rowKey = message.MessageId;
        _logger.LogInformation($">----> mId: {rowKey} | InvocationId: {invocationId} | Message received");

        // Azure Storage Table for Status tracking
        var storageConnectionString = _configuration[Constants.ConfigStorageAccount];
        var tableClient = new TableClient(storageConnectionString, Constants.StorageTable);
        await tableClient.CreateIfNotExistsAsync();

        // Process the Message received in the Azure Storage Queue
        var msg = JsonSerializer.Deserialize<Message>(message.MessageText);
        if (msg!=null)
        {
            var attemptCount = Constants.AttemptCountInitial;

            var statusTableEntity = await GetStatusIfExistsAsync(tableClient, Constants.StoragePartitionKey, rowKey);
            if (statusTableEntity == null)
            {
                statusTableEntity = new StatusTableEntity { RowKey = rowKey, InitialWorkEffort = msg.WaitPeriod, Message = message.MessageText, Data = msg.Data, Status = "Starting" };
                statusTableEntity = await AddStatusAsync(tableClient, Constants.StoragePartitionKey, rowKey, statusTableEntity);
            }
            else if (await IsCancelled(tableClient, statusTableEntity, msg.Data, invocationId))
            {
                return;
            }
            else
            {
                statusTableEntity.Status = "Resuming";
                statusTableEntity.AttemptCount++;
                statusTableEntity.LastTimeWorkStarted = GetCentralTimeNow(); //DateTimeOffset.UtcNow;
                attemptCount = statusTableEntity.AttemptCount;
                statusTableEntity = await AddOrUpdateStatusAsync(tableClient, Constants.StoragePartitionKey, rowKey, statusTableEntity);
            }

            if (msg.RaiseDurableFunction)
            {
                _logger.LogInformation($"[DF] >--S--> mId: {rowKey} | {msg.Data} | InvocationId: {invocationId}");
                var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(nameof(FunctionDurable) + "-" + nameof(FunctionDurable.RunOrchestrator), msg.Data);
                _logger.LogInformation($"[DF] <--F--< mId: {rowKey} | {msg.Data} | Dfid: {instanceId} | InvocationId: {invocationId}");
                statusTableEntity.Status = "[DF] Completed";
                await AddOrUpdateStatusAsync(tableClient, Constants.StoragePartitionKey, rowKey, statusTableEntity);
                return; 
            }

            if (msg.RaiseException)
            {
                _logger.LogInformation($"[EX] >--S--> mId: {rowKey} | {msg.Data} | #{attemptCount} | InvocationId: {invocationId}");
                await AddOrUpdateStatusAsync(tableClient, Constants.StoragePartitionKey, rowKey, statusTableEntity);
                throw new Exception($"[EX] Simulated exception for message: mId: {rowKey} | {msg.Data} | InvocationId: {invocationId}");
            }

            // Simulate work
            while (statusTableEntity.HasRemainingWork())
            {
                _logger.LogInformation($"[SW] >--S--> mId: {rowKey} | {msg.Data} | #{attemptCount} | Completed {statusTableEntity.CompletedWorkEffort} | Scheduled {msg.WaitPeriod} | InvocationId: {invocationId}");
                //await Task.Delay(TimeSpan.FromSeconds(statusTableEntity.RemainingWorkEffort));
                _ = DoWorkSimulation(statusTableEntity.CompletedWorkEffort);

                // Get latest status entity to ensure it reflects the latest state to check for cancellation
                statusTableEntity = await GetStatusIfExistsAsync(tableClient, Constants.StoragePartitionKey, rowKey);
                if (statusTableEntity == null)
                {
                    _logger.LogError($"INTERAL ERROR ISSUE: Status entity not found for RowKey (Message ID): {rowKey}");
                    return;
                }

                var isCancelledFlagged = await IsCancelled(tableClient, statusTableEntity, msg.Data, invocationId);
                statusTableEntity.CompletedWorkEffort++;
                statusTableEntity.CancelOperation = isCancelledFlagged;
                await AddOrUpdateStatusAsync(tableClient, Constants.StoragePartitionKey, rowKey, statusTableEntity);
                if (isCancelledFlagged)
                    return;
            }

            _logger.LogInformation($"[SW] <--F--< mId: {rowKey} | {msg.Data} | #{attemptCount} | Scheduled {msg.WaitPeriod} | InvocationId: {invocationId}");
            statusTableEntity.Status = "[SW] Completed";
            await AddOrUpdateStatusAsync(tableClient, Constants.StoragePartitionKey, rowKey, statusTableEntity);
        }
        else
        {
            _logger.LogWarning($"Received a message that could not be deserialized | InvocationId: {invocationId}");
        }
    }

    private async Task<bool> IsCancelled(TableClient tableClient, StatusTableEntity? statusTableEntity, string? data, string? invocationId)
    {
        if (statusTableEntity == null)
            return false;

        if (!statusTableEntity.CancelOperation)
            return false;

        statusTableEntity.Status = "Cancelled";
        await AddOrUpdateStatusAsync(tableClient, Constants.StoragePartitionKey, statusTableEntity.RowKey, statusTableEntity);
        _logger.LogWarning($"<--Q--< mId: {statusTableEntity.RowKey} | {data} | InvocationId: {invocationId}");
        return true;
    }

    private static BigInteger DoWorkSimulation(int completedWorkEffort)
    {
        var workLevel = completedWorkEffort * 10;

        // Simulate memory intensive work: allocate and fill an array
        var arraySize = 10_000_000 * workLevel; // 10,000,000 is about 80MB for int[]
        var data = new int[arraySize];
        for (var i = 0; i < arraySize; i++)
            data[i] = i % workLevel;

        // Simulate CPU intensive work: perform some calculations
        BigInteger sum = 0;
        for (var i = 0; i < data.Length; i++)
            sum += data[i] * (i % 7);
        return sum;
    }

    private async Task EnsureHubContextAsync()
    {
        _hubContext ??= await _signalRServiceManager.CreateHubContextAsync(Constants.SignalRHubName, CancellationToken.None);
    }

    private async Task SendSignalRMessage(StatusDto status)
    {
        await EnsureHubContextAsync();
        if (_hubContext == null)
        {
            _logger.LogError("SignalR Hub Context is not initialized.");
            return;
        }
        _logger.LogInformation($"Sending SignalR message for RowKey: {status.RowKey}, Status: {status.Status}");
        await _hubContext.Clients.All.SendAsync(Constants.SignalRMethodName, status);
    }

    private async Task<StatusTableEntity> AddStatusAsync(TableClient tableClient, string partitionKey, string rowKey, StatusTableEntity entity)
    {
        try
        {
            await tableClient.AddEntityAsync(entity);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 409)
        {
            _logger.LogWarning($"{nameof(AddStatusAsync)} entity with PartitionKey '{partitionKey}' and RowKey '{rowKey}' already exists. Fetching existing entity. Exception: {ex.Message}");
            var existing = await tableClient.GetEntityIfExistsAsync<StatusTableEntity>(partitionKey, rowKey);
            if (existing.HasValue && existing.Value != null)
                entity = existing.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(AddStatusAsync)} failed to add entity with PartitionKey '{partitionKey}' and RowKey '{rowKey}'.");
            throw;
        }

        await SendSignalRMessage(entity);
        var refetch = await tableClient.GetEntityIfExistsAsync<StatusTableEntity>(partitionKey, rowKey);
        if (refetch.HasValue && refetch.Value != null)
            entity = refetch.Value;

        return entity;
    }

    private async Task<StatusTableEntity> AddOrUpdateStatusAsync(TableClient tableClient, string partitionKey, string rowKey, StatusTableEntity entity)
    {
        try
        {
            var existingEntity = await GetStatusIfExistsAsync(tableClient, partitionKey, rowKey);
            if (existingEntity == null)
                return await AddStatusAsync(tableClient, partitionKey, rowKey, entity);

            await SendSignalRMessage(entity);
            entity.ETag = existingEntity.ETag;
            await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
            return entity;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 412)
        {
            _logger.LogWarning($"{nameof(AddOrUpdateStatusAsync)} ETag conflict when updating entity with PartitionKey '{partitionKey}' and RowKey '{rowKey}'. Exception: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(AddOrUpdateStatusAsync)} failed to add or update entity with PartitionKey '{partitionKey}' and RowKey '{rowKey}'.");
            throw;
        }
    }

    private async Task<StatusTableEntity?> GetStatusIfExistsAsync(TableClient tableClient, string partitionKey, string rowKey)
    {
        try
        {
            var response = await tableClient.GetEntityIfExistsAsync<StatusTableEntity>(partitionKey, rowKey);
            return response.HasValue ? response.Value : null;
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, $"{nameof(GetStatusIfExistsAsync)} failed to get entity with PartitionKey '{partitionKey}' and RowKey '{rowKey}'.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(GetStatusIfExistsAsync)} unexpected error when getting entity with PartitionKey '{partitionKey}' and RowKey '{rowKey}'.");
            return null;
        }
    }
}