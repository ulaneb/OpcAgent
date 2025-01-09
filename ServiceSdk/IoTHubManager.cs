using Microsoft.Azure.Devices;
using System.Net.Mail;
using System.Net;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;

namespace ServiceSdk;

public class IoTHubManager
{
    private readonly ServiceClient client;
    private readonly RegistryManager registry;
    private readonly BlobContainerClient containerClient;
    private readonly string prefix;

    public IoTHubManager(ServiceClient client, RegistryManager registry, BlobContainerClient containerClient, string prefix)
    {
        this.client = client;
        this.registry = registry;
        this.containerClient = containerClient;
        this.prefix = prefix;
    }

    public async Task UpdateDesiredTwin(string deviceId, dynamic propertyValue)
    {
        var twin = await registry.GetTwinAsync(deviceId);
        twin.Properties.Desired["ProductionRate"] = propertyValue;
        await registry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
    }
    public async Task MonitorAndAdjustProductionRate()
    {
        // Replace this with the appropriate query or data fetch logic based on ASA output destination.
        var kpiData = await GetProductionKpiData();

        foreach (var kpi in kpiData)
        {
            if (kpi.ProcentOfGoodProduction < 90)
            {
                Console.WriteLine($"Device {kpi.DeviceId} below 90% production rate. Adjusting desired production rate...");
                await AdjustProductionRate(kpi.DeviceId);
            }
        }
    }

    private async Task<List<KpiData>> GetProductionKpiData()
    {
        var kpiList = new List<KpiData>();

        // List blobs in the specified folder or prefix
        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: prefix))
        {
            BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);

            // Download and process the blob content
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            using (var streamReader = new StreamReader(download.Content))
            {
                string content = await streamReader.ReadToEndAsync();
                var kpiData = JsonSerializer.Deserialize<List<KpiData>>(content);

                if (kpiData != null)
                {
                    kpiList.AddRange(kpiData);
                }
            }
        }

        return kpiList;
    }

    private async Task AdjustProductionRate(string deviceId)
    {
        var twin = await registry.GetTwinAsync(deviceId);

        // Retrieve the current desired production rate, defaulting to 100 if not set.
        double currentRate = twin.Properties.Desired.Contains("ProductionRate")
            ? twin.Properties.Desired["ProductionRate"]
            : 100;

        double newRate = Math.Max(currentRate - 10, 0); // Prevent negative rates.

        twin.Properties.Desired["ProductionRate"] = newRate;
        await registry.UpdateTwinAsync(deviceId, twin, twin.ETag);

        Console.WriteLine($"Updated desired production rate for device {deviceId} to {newRate}.");
    }
}

public class KpiData
{
    public string DeviceId { get; set; }
    public double ProcentOfGoodProduction { get; set; }
}
