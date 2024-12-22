using Microsoft.Azure.Devices;

namespace ServiceSdk;

public class IoTHubManager
{
    private readonly ServiceClient client;
    private readonly RegistryManager registry;

    public IoTHubManager(ServiceClient client, RegistryManager registry)
    {
        this.client = client;
        this.registry = registry;
    }

    public async Task UpdateDesiredTwin(string deviceId, dynamic propertyValue)
    {
        var twin = await registry.GetTwinAsync(deviceId);
        twin.Properties.Desired["ProductionRate"] = propertyValue;
        await registry.UpdateTwinAsync(twin.DeviceId, twin, twin.ETag);
    }
}
