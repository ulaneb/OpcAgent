using Microsoft.Azure.Amqp.Framing;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Net.Mime;
using System.Text;

namespace DeviceSdk;

public class Device
{
    private readonly DeviceClient client;

    public Device(DeviceClient deviceClient)
    {
        this.client = deviceClient;
    }

    [Flags]
    public enum ErrorFlags
    {
        None = 0,            // 0000
        EmergencyStop = 1,   // 0001
        PowerFailure = 2,    // 0010
        SensorFailure = 4,   // 0100
        Unknown = 8          // 1000
    }

    #region Sending Message (Telemetry) Device to Cloud
    public async Task SendMessage(IEnumerable<OpcValue> job)
    {
        Console.WriteLine($"Device sending message to IoTHub...\n");
        var telemetryData = new Dictionary<string, object>();

        int index = 0;
        foreach (var item in job)
        {
            switch (index)
            {
                case 1: telemetryData["ProductionStatus"] = item.Value; break;
                case 3: telemetryData["ProductionRate"] = item.Value; break;
                case 5: telemetryData["WorkorderId"] = item.Value; break;
                case 7: telemetryData["Temperature"] = item.Value; break;
                case 9: telemetryData["GoodCount"] = item.Value; break;
                case 11: telemetryData["BadCount"] = item.Value; break;
                default: break;
            }
            index++;
        }

        var dataString = JsonConvert.SerializeObject(telemetryData);
        Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
        eventMessage.ContentType = MediaTypeNames.Application.Json;
        eventMessage.ContentEncoding = "utf-8";

        await client.SendEventAsync(eventMessage);
        Console.WriteLine($"\t {DateTime.Now.ToLocalTime()} > Sending message: Data [{dataString}");

        await Task.Delay(1000);
    }
    #endregion

    #region Device Twin
    public async Task UpdateTwinAsync(ErrorFlags currentErrorValue,OpcValue productionRate)
    {
        var twin = await client.GetTwinAsync();
        Console.WriteLine($"\n Initial twin value received: \n {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
        Console.WriteLine();

        var reportedProperties = new TwinCollection();
        reportedProperties["DeviceError"] = new
        {
            Value = (int)currentErrorValue,
            Description = currentErrorValue.ToString()
        };
        reportedProperties["ProductionRate"] = productionRate.Value.ToString();

        await client.UpdateReportedPropertiesAsync(reportedProperties);
        Console.WriteLine("Device Twin updated.");
    }
    private async Task OnDesiredPropertyChange(TwinCollection desiredProperties, object userContext)
    {
        Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
        TwinCollection reportedCollection = new TwinCollection();
        reportedCollection["ProductionRate"] = desiredProperties["ProductionRate"];
        using (var client = new OpcClient("opc.tcp://localhost:4840/"))
        {
            client.Connect();
            client.WriteNode("ns=2;s=Device 1/ProductionRate", (int)desiredProperties["ProductionRate"]);
            client.WriteNode("ns=2;s=Device 1/DeviceError", (int)desiredProperties["DeviceError"].Value);
            Console.WriteLine($"Updated: {client.ReadNode("ns=2;s=Device 1/ProductionRate")}");
        }
        await client.UpdateReportedPropertiesAsync(reportedCollection).ConfigureAwait(false);
    }
    public async Task SendMessageWhenValueChanges(ErrorFlags currentErrorValue, ErrorFlags previousDeviceError)
    {
        Console.WriteLine($"Change value - device sending message to IoTHub...\n");
        Message eventMessage = new Message(Encoding.UTF8.GetBytes($"Device Error value changes from {currentErrorValue.ToString()} to {previousDeviceError.ToString()}"));
        eventMessage.ContentType = MediaTypeNames.Application.Json;
        eventMessage.ContentEncoding = "utf-8";

        await client.SendEventAsync(eventMessage);

        await Task.Delay(1000);
    }
    #endregion

    #region Direct Methods - uruchomienie serwisu zdalnie z clouda
    private async Task<MethodResponse> EmergencyStopHandler(MethodRequest methodRequest, object userContext)
    {
        Console.WriteLine($"\t METHOD EXECUTED: {methodRequest.Name}");
        var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new { deviceNumber = default(int) });

        using (var client = new OpcClient("opc.tcp://localhost:4840/"))
        {
            client.Connect();
            client.WriteNode($"ns=2;s=Device 1/DeviceError",1);
        }
        await UpdateTwinAsync(ErrorFlags.EmergencyStop, "ns=2;s=Device 1/DeviceError");
        return new MethodResponse(0);
    }
    private async Task<MethodResponse> ResetErrorStatusHandler(MethodRequest methodRequest, object userContext)
    {
        Console.WriteLine($"\t METHOD EXECUTED: {methodRequest.Name}");
        var payload = JsonConvert.DeserializeAnonymousType(methodRequest.DataAsJson, new { deviceNumber = default(int) });
        using (var client = new OpcClient("opc.tcp://localhost:4840/"))
        {
            client.Connect();
            client.WriteNode($"ns=2;s=Device 1/DeviceError", 0);
        }
        await UpdateTwinAsync(ErrorFlags.None, "ns=2;s=Device 1/DeviceError");
        return new MethodResponse(0);
    }
    #endregion

    public async Task InitializeHandlers()
    {
        await client.SetMethodHandlerAsync("EmergencyStop", EmergencyStopHandler, client);
        await client.SetMethodHandlerAsync("ResetErrorStatus", ResetErrorStatusHandler, client);

        await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChange, client);
    }
}
