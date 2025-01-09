using DeviceSdk;
using Microsoft.Azure.Devices.Client;
using System.Text.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using TransportType = Microsoft.Azure.Devices.Client.TransportType;
using System.Text.RegularExpressions;

var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sharedsettings.json");
var json = File.ReadAllText(jsonPath);
var config = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);

List<VirtualDevice> devices = new List<VirtualDevice>();

var client = new OpcClient("opc.tcp://localhost:4840/");
client.Connect();
foreach (var childNode in client.BrowseNode(OpcObjectTypes.ObjectsFolder).Children())
{
    var pattern = new Regex(@"^Device [0-9]+$");
    var nodeId = childNode.Attribute(OpcAttribute.DisplayName).Value.ToString();
    if (!pattern.Match(nodeId).Success)
        continue;
    var deviceConnectionString = config["ConnectionStrings"][$"{nodeId}"];

    var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
    await deviceClient.OpenAsync();
    var device = new VirtualDevice(deviceClient,nodeId);
    devices.Add(device);
}
while (true)
{
    foreach (var device in devices)
    {
        await device.InitializeHandlers();

        Console.WriteLine("Sending telemetry from device to cloud...\n");
    
        await device.ReadNodesAsync();
        await device.SendMessage();
        await device.UpdateTwinAsync();
    }
    Task.Delay(3000);
}