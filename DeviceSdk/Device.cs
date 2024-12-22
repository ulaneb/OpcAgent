using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
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

    private string GetFlagName(int value)
    {
        switch (value)
        {
            case 0: return "None";
            case 1: return "Emergency Stop";
            case 2: return "Power Failure";
            case 4: return "Sensor Failure";
            case 8: return "Unknown";
            default: return null;
        }
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
                //case 13: telemetryData["DeviceError"] = item.Value; break;
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
    public async Task UpdateTwinAsync(int currentErrorValue,OpcValue productionRate)
    {
        var twin = await client.GetTwinAsync();
        Console.WriteLine($"\n Initial twin value received: \n {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
        Console.WriteLine();

        var reportedProperties = new TwinCollection();
        reportedProperties["DeviceError"] = new
        {
            Value = currentErrorValue,
            Description = GetFlagName(currentErrorValue)
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

        await client.UpdateReportedPropertiesAsync(reportedCollection).ConfigureAwait(false);
    }
    public async Task SendMessageWhenValueChanges(int currentErrorValue, int previousDeviceError)
    {
        Console.WriteLine($"Change value - device sending message to IoTHub...\n");
        Message eventMessage = new Message(Encoding.UTF8.GetBytes($"Device Error value changes from {GetFlagName(currentErrorValue)} to {GetFlagName(previousDeviceError)}"));
        eventMessage.ContentType = MediaTypeNames.Application.Json;
        eventMessage.ContentEncoding = "utf-8";

        await client.SendEventAsync(eventMessage);

        await Task.Delay(1000);
    }
    #endregion

    public async Task InitializeHandlers()
    {
        //await client.SetReceiveMessageHandlerAsync(OnC2DMessageReceivedAsync, client);

        //await client.SetMethodHandlerAsync("SendMessage", SendMessageHandler, client);
        //await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);

        await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChange, client);
    }
}
