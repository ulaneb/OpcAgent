using Azure.Storage.Blobs;
using Microsoft.Azure.Devices;
using ServiceSdk;
using System.Text.Json;

var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sharedsettings.json");
var json = File.ReadAllText(jsonPath);
var config = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
var serviceConnectionString = config["ConnectionStrings"]["Service"];

using var serviceClient = ServiceClient.CreateFromConnectionString(serviceConnectionString);
using var registryManager = RegistryManager.CreateFromConnectionString(serviceConnectionString);

string storageConnectionString = config["ConnectionStrings"]["StorageAccount"];
string containerName = config["ConnectionStrings"]["ProductionKpisContainer"];
string prefix = config["ConnectionStrings"]["Prefix"];

BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnectionString);
BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

var manager = new IoTHubManager(serviceClient, registryManager, containerClient,prefix);

int input;
do
{
    Console.WriteLine("\nChoose an option:");
    Console.WriteLine("1: Update ProductionRate for a specific device.");
    Console.WriteLine("0: Exit.");

    input = int.TryParse(Console.ReadLine(), out int choice) ? choice : 0;

    switch (input)
    {
        case 1:
            Console.WriteLine("\nType your device ID (confirm with enter):");
            string deviceId = Console.ReadLine() ?? string.Empty;

            Console.WriteLine("\nType value for ProductionRate (confirm with enter):");
            string propertyValue = Console.ReadLine() ?? "0";

            await manager.UpdateDesiredTwin(deviceId, propertyValue);
            break;

        case 2:
            Console.WriteLine("\nMonitoring KPIs and adjusting ProductionRate...");
            await manager.MonitorAndAdjustProductionRate();
            break;

        case 0:
            Console.WriteLine("\nExiting.");
            break;

        default:
            Console.WriteLine("\nInvalid option.");
            break;
    }
} while (true);