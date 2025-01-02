using Microsoft.Azure.Devices;
using ServiceSdk;

string serviceConnectionString = "HostName=IoTHub-UL.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=IefYFsC7NHDKgRH59sCbeUiEbiiIpFiEtAIoTL+GiNY=";

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