# Introduction

The purpose of **BlazorDise** (Blazor Paradise) is to demonstrate various scenarios of Blazor Server. It currently demonstrates the capabilities of Azure Storage and asynchronous handling of processing larger workloads separate from visitor access. This allows dynamic updates, including with reattachment. The purpose is to provide push updates, rather than polling. 

And, what proof-of-concept (POC) would be complete without theme music? [BlazorDise.mp3](./BlazorDise.mp3)

# Technology

- Visual Studio 2022 (Version 17.14.7+)
- .NET 8 "Core"
- (Optional) Azure SignalR
- (Optional) Azure Function App
- (Optional) Azure App Service

# Azure Resources

*All Azure Resources are optional as this application can be run locally.*

| Azure Resource Type | Plan                                     | Purpose                                                      |
| ------------------- | ---------------------------------------- | ------------------------------------------------------------ |
| Resource Group      | n/a                                      | Organization                                                 |
| SignalR             | Free_F1                                  | Push Notifications from Function App to Blazor Server        |
| Function App        | Consumption Serverless Plan - Dynamic Y1 | QueueTrigger (primary, supporting scalable operations across multiple functions), Azure Durable Function (example) |
| Storage Account     | (Part of Function App)                   | Function App support, Table (Status Tracking), Queue (QueueTrigger) |
| Web App             | B1                                       | Blazor Server UI                                             |

# Configuration

The folllowing configuration settings are used:

| Setting                      | Purpose                                                      | Locations Supported                                          | Project Used                  | Example Setting                                              |
| ---------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------ | ----------------------------- | ------------------------------------------------------------ |
| StorageAccountConnection     | Storage connection to hold state information to demonstrate progress. | secrets.json, App Service Environmental Variable             | BlazorDise.Fcn, BlazorDise.Ui | DefaultEndpointsProtocol=https;AccountName=\<storageaccountname>;AccountKey=\<accountkey>;EndpointSuffix=core.windows.net |
| AzureSignalRConnectionString | Push Notification Support                                    | secrets.json, App Service Environmental Variable             | BlazorDise.Fcn, BlazorDise.Ui | Endpoint=https://\<signalrname>.service.signalr.net;AccessKey=\<accesskey>;Version=1.0; |
| ApplicationTimeZone          | Set the desired time zone (defaults to *Central Standard Time*) | secrets.json, App Service Environmental Variable, local.settings.json | BlazorDise.Fcn                | Central Standard Time                                        |

For *local.settings.json* (which is not included, purposely, in source control) to run locally, reference [local.sample.settings.json](./BlazorDise.Fcn/local.sample.settings.json) (can copy the contents).

The file [host.json](./BlazorDise.fcn/host.json) has configuration information for the Azure Function and Queue Trigger.

# Storage Queue Trigger

An Azure Storage Queue is used by Blazor Server to place items that need to be processed by the Azure Function, decoupling processing of longer-term tasks from the visitor UI. The queue's name, *blazordise-queue-items*, remains there until the Azure Function picks them up. While Azure Storage Explorer displays the number of items in the queue (properties of the queue), once picked up they are hidden. Failed items are put into *blazordise-queue-items-poison* automatically (such as too many exceptions). Successful items are removed from the queue (the number of items decreases).

# SignalR

Be careful on what object type is used for communicating between producer (Azure Function) and consumer (Blazor Server) as implicit serialization and deserialization is used behind the scenes by the SignalR Management SDK.

### NuGet Packages

- Microsoft.AspNetCore.SignalR.Client 8.0.17 (Blazor Server) - to receive messages (9.0.6 is available, but supports .NET 9)
- Microsoft.Azure.SignalR 1.30.3 (Blazor Server, transitive to Azure Function) - indications it can be used as a SignalR backplane so multiple client instances will all receive the messages
- Microsoft.Azure.SignalR.Management 1.30.3 (Azure Function, Blazor Server)

# Azure Durable Function

An alternative considered to augment Azure Storage Queue (once originally faced with a limit of 5 exception failures, which includes Azure Function Consumption Plan run time limits of 10 minutes), but not needed because of the findings with Azure Storage Queue and Azure Function QueueTrigger being so robust (tested configuring 199 exception failures).

```
Blazor Website ---> Calculation Request ---> Azure Storage Queue ---> Azure Durable Function 
     ^                                                                     | Periodic Updates
     +------------------- Signal-R Updates --------------------------------+
```

Both Azure Functions can work on the Consumption Plan. The Azure Function TriggerQueue implements the launching of an Azure Durable Function.

Azure Storage Queue could have three queues:

- process-request
- process-request-history
- process-request-poison

Questions

- What happens with exception within the Durable Function?
- Resource Limits?

## Azure Function Storage Queues

This demonstrates, locally within Visual Studio Azure Function and via Azure Function App, using Azure Storage Queues to populate a JSON-based message and retrieve the message using a **QueueTrigger**.

1. If the Azure Function is restarted, it still gets the message when the Azure Function is started back up.
2. Multiple messages are able to be processed
3. Items in the Azure Storage Queue that are being processed by the Queue Trigger are not visible for a period of time (can get a count of the items, even if not visible by right-clicking the queue in Azure Storage Explorer)
4. By default, if there are 5 exceptions, the item is moved to a poison queue automatically, which is visible via Azure Storage Explorer - this setting can be changed via host.json
5. If an exception occurs, it will attempt
6. If an Azure Function exceeds the timeout period (maximum of 10 minutes for a Consumption Plan), that counts as an exception that cannot be caught in code.
7. If the Azure Function exits gracefully, the item is removed from the queue (it is completed)
8. We may wish to have a history queue (-history, similar to -poison) to help track (beyond the DB)

## History

| Date       | Notes                                                        |
| ---------- | ------------------------------------------------------------ |
| 2025-09-05 | Cleaned up and contributed to open source as initially anticipated. |
| 2025-06-25 | SignalR working between Azure Function (TriggerQueue and producer of the message) and Blazor Server (consumer of the message). Manually created an Azure SignalR resource (blazorusnorthasr) using free plan. |
| 2025-06-23 | Extensive testing and recovery performed with Azure Function Trigger Queue, including being operational within an Azure Function App. Verify creation of the resource done by hand and published via Blazor.Fcn project (right-click, Publish). |
| 2025-06-17 | Start of this Proof of Concept (POC) project.                |

# Contributors

- Hans Dickel [✉️](mailto:hans@geekfog.net) [🌍](https://www.linkedin.com/in/hansdickel) [💻](https://github.com/geekfog)

\~End~