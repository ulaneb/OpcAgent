using Opc.UaFx;
using Opc.UaFx.Client;

using Microsoft.Azure.Devices.Client;
using DeviceSdk;

const string deviceConnectionString = "HostName=CenterName.azure-devices.net;DeviceId=OpcDeviceSdk1;SharedAccessKey=r51seffyASoR0kfHuaAdDNAaxjRLB71PfbjniJlzuVE=";
using var deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionString, TransportType.Mqtt);
await deviceClient.OpenAsync();
var device = new Device(deviceClient);
await device.InitializeHandlers();

int previousDeviceError = 0;

using (var client = new OpcClient("opc.tcp://localhost:4840/"))
{
    client.Connect();

    OpcReadNode[] commands = new OpcReadNode[] {
        new OpcReadNode("ns=2;s=Device 1/ProductionStatus", OpcAttribute.DisplayName),
        new OpcReadNode("ns=2;s=Device 1/ProductionStatus"),
        new OpcReadNode("ns=2;s=Device 1/ProductionRate", OpcAttribute.DisplayName),
        new OpcReadNode("ns=2;s=Device 1/ProductionRate"),
        new OpcReadNode("ns=2;s=Device 1/WorkorderId", OpcAttribute.DisplayName),
        new OpcReadNode("ns=2;s=Device 1/WorkorderId"),
        new OpcReadNode("ns=2;s=Device 1/Temperature", OpcAttribute.DisplayName),
        new OpcReadNode("ns=2;s=Device 1/Temperature"),
        new OpcReadNode("ns=2;s=Device 1/GoodCount", OpcAttribute.DisplayName),
        new OpcReadNode("ns=2;s=Device 1/GoodCount"),
        new OpcReadNode("ns=2;s=Device 1/BadCount", OpcAttribute.DisplayName),
        new OpcReadNode("ns=2;s=Device 1/BadCount"),
        new OpcReadNode("ns=2;s=Device 1/DeviceError", OpcAttribute.DisplayName),
        new OpcReadNode("ns=2;s=Device 1/DeviceError"),
    };

    Console.WriteLine("Sending telemetry from device to cloud...\n");
    while(true){
        IEnumerable<OpcValue> job = client.ReadNodes(commands);
    
        //foreach (var item in job)
        //{
        //    Console.WriteLine(item.Value);
        //}

        await device.SendMessage(job);

        var currentErrorValue = Convert.ToInt32(job.ElementAt(13).Value);

        if (currentErrorValue != previousDeviceError)
        {
            Console.WriteLine("Device Error Changes");

            await device.UpdateTwinAsync(currentErrorValue, job.ElementAt(3));
            await device.SendMessageWhenValueChanges(currentErrorValue, previousDeviceError);

            previousDeviceError = currentErrorValue;
        }
    }
}
