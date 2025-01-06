using Microsoft.Azure.Devices;
using ServiceSdk;
using System.Text.Json;

var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sharedsettings.json");
var json = File.ReadAllText(jsonPath);
var config = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
var serviceConnectionString = config["ConnectionStrings"]["ServiceConnectionString"];

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