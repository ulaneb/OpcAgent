using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using ServiceBus.Lib;
using System.Text.Json;

var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sharedsettings.json");
var json = File.ReadAllText(jsonPath);
var config = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);

string serviceBusConnectionString = config["ConnectionStrings"]["ServiceBus"];
string serviceConnectionString = config["ConnectionStrings"]["Service"];

using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

var manager = new IoTHubManager(serviceClient, registryManager);

await using ServiceBusClient client = new ServiceBusClient(serviceBusConnectionString);

#region Production Kpi ServiceBus
string productionKpiQueueName = config["ConnectionStrings"]["ProductionKpiQueueName"]; 
await using ServiceBusProcessor productionKpiProcessor = client.CreateProcessor(productionKpiQueueName);

productionKpiProcessor.ProcessMessageAsync += Processor_ProcessMessageAsync;
productionKpiProcessor.ProcessErrorAsync += Process_ErrorAsync;
#endregion

#region Device Errors ServiceBus
string deviceErrorsQueueName = config["ConnectionStrings"]["DeviceErrorsQueueName"];
await using ServiceBusProcessor deviceErrorsProcessor = client.CreateProcessor(deviceErrorsQueueName);

deviceErrorsProcessor.ProcessMessageAsync += Processor_ProcessTriggerAsync;
deviceErrorsProcessor.ProcessErrorAsync += Process_ErrorAsync;
#endregion

#region Starting Processors
await productionKpiProcessor.StartProcessingAsync();
await deviceErrorsProcessor.StartProcessingAsync();
Console.WriteLine("Waiting for messages... Press Enter to stop");
Console.ReadLine();
Console.WriteLine("\nStopping receiving messages");
await productionKpiProcessor.StopProcessingAsync();
await deviceErrorsProcessor.StopProcessingAsync();
#endregion


async Task Processor_ProcessMessageAsync(ProcessMessageEventArgs arg)
{
    Console.WriteLine($"Received message: \n\t {arg.Message.Body}\n");
    var productionData = JsonSerializer.Deserialize<ProductionData>(arg.Message.Body.ToString());
    if (productionData.ProcentOfGoodProduction < 90)
    {
        manager.DecreaseProductionRateDesiredTwin(productionData.DeviceId);
    }
    await arg.CompleteMessageAsync(arg.Message);
}

async Task Processor_ProcessTriggerAsync(ProcessMessageEventArgs arg)
{
    try
    {
        string messageBody = arg.Message.Body.ToString();
        Console.WriteLine($"Received message: {messageBody}");

        var message = JsonSerializer.Deserialize<ErrorData>(arg.Message.Body.ToString());
        string deviceId = message.DeviceId;
        Console.WriteLine($"Triggering Emergency Stop for Device ID: {deviceId}");
        await TriggerEmergencyStop(deviceId);
        await arg.CompleteMessageAsync(arg.Message);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing message: {ex.Message}");
    }
}

async Task TriggerEmergencyStop(string deviceId)
{
    try
    {
        var methodInvocation = new CloudToDeviceMethod("EmergencyStop") // The method name must match the device's implementation
        {
            ResponseTimeout = TimeSpan.FromSeconds(30)
        };
        var response = await serviceClient.InvokeDeviceMethodAsync(deviceId, methodInvocation);

        Console.WriteLine($"EmergencyStop invoked for {deviceId}.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error invoking EmergencyStop on {deviceId}: {ex.Message}");
    }
}

Task Process_ErrorAsync(ProcessErrorEventArgs arg)
{
    Console.WriteLine(arg.Exception.ToString);
    return Task.CompletedTask;
}
public class ProductionData
{
    public string DeviceId { get; set; }
    public float ProcentOfGoodProduction { get; set; }
}
public class ErrorData
{
    public string DeviceId { get; set; }
    public double OccuredErrors { get; set; }
}