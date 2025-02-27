﻿using Azure.Messaging.ServiceBus;
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
var productionKpiProcessorTask = productionKpiProcessor.StartProcessingAsync();
var deviceErrorsProcessorTask = deviceErrorsProcessor.StartProcessingAsync();
Console.WriteLine("Waiting for messages... Press Enter to stop");
await Task.WhenAll(productionKpiProcessorTask, deviceErrorsProcessorTask);
Console.ReadLine();
Console.WriteLine("\nStopping receiving messages");
var stopProductionKpiProcessorTask = productionKpiProcessor.StopProcessingAsync();
var stopDeviceErrorsProcessorTask = deviceErrorsProcessor.StopProcessingAsync();

await Task.WhenAll(stopProductionKpiProcessorTask, stopDeviceErrorsProcessorTask);
#endregion


async Task Processor_ProcessMessageAsync(ProcessMessageEventArgs arg)
{
    try
    {
        Console.WriteLine($"Received message: \n\t {arg.Message.Body}\n");
        var productionData = JsonSerializer.Deserialize<ProductionData>(arg.Message.Body.ToString());
        manager.DecreaseProductionRateDesiredTwin(productionData.DeviceId);
        Console.WriteLine($"ProductionRate has been decreased by 10");
        await arg.CompleteMessageAsync(arg.Message);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error processing message: {ex.Message}");
    }
}

async Task Processor_ProcessTriggerAsync(ProcessMessageEventArgs arg)
{
    try
    {
        Console.WriteLine($"Received message: {arg.Message.Body.ToString()}");
        var message = JsonSerializer.Deserialize<ErrorData>(arg.Message.Body.ToString());
        string deviceId = message.DeviceId;
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
        var methodInvocation = new CloudToDeviceMethod("EmergencyStop")
        {
            ResponseTimeout = TimeSpan.FromSeconds(30)
        };
        var response = await serviceClient.InvokeDeviceMethodAsync(deviceId, methodInvocation);

        Console.WriteLine($"EmergencyStop invoked.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error invoking EmergencyStop: {ex.Message}");
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