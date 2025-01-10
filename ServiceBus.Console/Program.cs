using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Devices;
using ServiceBus.Lib;
using System.Text.Json;

var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sharedsettings.json");
var json = File.ReadAllText(jsonPath);
var config = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);

string serviceBusConnectionString = config["ConnectionStrings"]["ServiceBus"];
string productionKpiQueueName = config["ConnectionStrings"]["ProductionKpiQueueName"];
string serviceConnectionString = config["ConnectionStrings"]["Service"];

using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

var manager = new IoTHubManager(serviceClient, registryManager);

await using ServiceBusClient client = new ServiceBusClient(serviceBusConnectionString);
await using ServiceBusProcessor processor = client.CreateProcessor(productionKpiQueueName);

processor.ProcessMessageAsync += Processor_ProcessMessageAsync;
processor.ProcessErrorAsync += Process_ErrorAsync;

await processor.StartProcessingAsync();
Console.WriteLine("Waiting for messages... Press Enter to stop");
Console.ReadLine();
Console.WriteLine("\nStopping receiving messages");
await processor.StopProcessingAsync();
Console.WriteLine("\nStopped receiving messages");

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