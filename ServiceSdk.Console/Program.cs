using Microsoft.Azure.Devices;
using ServiceSdk;
using System;

string serviceConnectionString = "HostName=CenterName.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=4YRYs8K6uxRPIeG05HwzUoFTyowWmu6+nAIoTOxvXnY=";

using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

var manager = new IoTHubManager(serviceClient, registryManager);

int input;
do
{
    Console.WriteLine("\nType your device ID (confirm with enter):");
    string deviceId = Console.ReadLine() ?? string.Empty;

    Console.WriteLine("\nType value for ProductionRate (confirm with enter):");
    string propertyValue = Console.ReadLine() ?? "0";

    await manager.UpdateDesiredTwin(deviceId, propertyValue);
} while (true);