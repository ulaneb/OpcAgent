using Microsoft.Azure.Devices;

namespace ServiceBus.Lib;

public class IoTHubManager
{
    private readonly ServiceClient client;
    private readonly RegistryManager registry;

    public IoTHubManager(ServiceClient client, RegistryManager registry)
    {
        this.client = client;
        this.registry = registry;
    }

    public async Task DecreaseProductionRateDesiredTwin(string deviceId)
    {
        var twin = await registry.GetTwinAsync(deviceId);
        var currentProductionRate = twin.Properties.Reported["ProductionRate"];
        if (currentProductionRate >= 10)
            twin.Properties.Desired["ProductionRate"] = currentProductionRate - 10;
        await registry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
    }
}
