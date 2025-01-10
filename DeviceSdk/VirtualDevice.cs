using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using OpcAgent;
using System.Net.Mime;
using System.Text;

namespace DeviceSdk;

public class VirtualDevice
{
    private readonly DeviceClient client;
    private readonly OpcNodeInfo opcNodeInfo;
    private readonly OpcClientConnection opcClientConnection;
    private readonly OpcClient opcClient;
    private readonly string nodeId;

    public IEnumerable<OpcValue> job;
    public ErrorFlags previousDeviceError = 0;

    public VirtualDevice(DeviceClient deviceClient, string nodeId, OpcClient opcClient)
    {
        this.client = deviceClient;
        this.nodeId = nodeId;
        this.opcClientConnection = new OpcClientConnection(nodeId);
        this.opcClient = opcClient;
    }

    [Flags]
    public enum ErrorFlags
    {
        None = 0,
        EmergencyStop = 1,
        PowerFailure = 2,
        SensorFailure = 4,
        Unknown = 8
    }

    public async Task ReadNodesAsync()
    {
        job = opcClientConnection.GetNodes();
    }

    #region Sending Message (Telemetry) Device to Cloud
    public async Task SendMessage()
    {
        Console.WriteLine($"Device sending message to IoTHub...\n");
        var telemetryData = new
        {
            DeviceName = nodeId,
            ProductionStatus = job.ElementAt(1).Value,
            WorkorderId = job.ElementAt(5).Value,
            Temperature = job.ElementAt(7).Value,
            GoodCount = job.ElementAt(9).Value,
            BadCount = job.ElementAt(11).Value
        };

        var dataString = JsonConvert.SerializeObject(telemetryData);
        Microsoft.Azure.Devices.Client.Message eventMessage = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(dataString));
        eventMessage.ContentType = MediaTypeNames.Application.Json;
        eventMessage.ContentEncoding = "utf-8";

        await client.SendEventAsync(eventMessage);
        Console.WriteLine($"\t {DateTime.Now.ToLocalTime()} > Sending message: Data [{dataString}");

        await Task.Delay(5000);
    }
    #endregion

    #region Device Twin
    public async Task UpdateTwinAsync()
    {
        var twin = await client.GetTwinAsync();
        Console.WriteLine($"\n Initial twin value received: \n {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
        Console.WriteLine();

        var reportedProperties = new TwinCollection();
        var currentErrorValue = (ErrorFlags)job.ElementAt(13).Value;
        reportedProperties["DeviceError"] = currentErrorValue.ToString();
        reportedProperties["ProductionRate"] = job.ElementAt(3).Value.ToString();

        if (currentErrorValue != previousDeviceError)
        {
            Console.WriteLine("Device Error Changes");

            await SendMessageWhenValueChanges(currentErrorValue, previousDeviceError);

            previousDeviceError = currentErrorValue;
        }

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
            client.WriteNode($"ns=2;s={nodeId}/ProductionRate", (int)desiredProperties["ProductionRate"]);
            Console.WriteLine($"Updated: {client.ReadNode($"ns=2;s={nodeId}/ProductionRate")}");
        }
        await client.UpdateReportedPropertiesAsync(reportedCollection).ConfigureAwait(false);
    }
    public async Task SendMessageWhenValueChanges(ErrorFlags currentErrorValue, ErrorFlags previousDeviceError)
    {
        Console.WriteLine($"Change value - device sending message to IoTHub...\n");
        var data = new
        {
            ErrorName = currentErrorValue.ToString(),
            NewErrors = NewErrorsCounter((int)currentErrorValue),
            DeviceName = nodeId,
            CurrentErrors = currentErrorValue.ToString()
        };
        var dataString = JsonConvert.SerializeObject(data);
        Microsoft.Azure.Devices.Client.Message eventMessage = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(dataString));
        eventMessage.ContentType = MediaTypeNames.Application.Json;
        eventMessage.ContentEncoding = "utf-8";

        await client.SendEventAsync(eventMessage);

        await Task.Delay(5000);
    }
    private int NewErrorsCounter(int errorCode)
    {
        int difference = errorCode - (int)previousDeviceError;
        if (difference <= 0) return 0;

        ErrorFlags error = (ErrorFlags)difference;
        return new[]
        {
            ErrorFlags.EmergencyStop,
            ErrorFlags.PowerFailure,
            ErrorFlags.SensorFailure,
            ErrorFlags.Unknown
        }.Count(flag => error.HasFlag(flag));
    }
    #endregion

    #region Direct Methods - uruchomienie serwisu zdalnie z clouda
    private async Task<MethodResponse> MethodHandler(MethodRequest methodRequest, object userContext)
    {
        Console.WriteLine($"\t METHOD EXECUTED: {methodRequest.Name}");
        var result = opcClient.CallMethod($"ns=2;s={nodeId}", $"ns=2;s={nodeId}/{methodRequest.Name}");
        if (result != null)
        {
            Console.WriteLine("Success");
        }
        else
            Console.WriteLine("failed");
        await Task.Delay(1000);
        return new MethodResponse(0);
    }
    private async Task<MethodResponse> ResetErrorStatusHandler(MethodRequest methodRequest, object userContext)
    {
        //TODO
        return new MethodResponse(0);
    }
    #endregion

    public async Task InitializeHandlers()
    {
        await client.SetMethodHandlerAsync("EmergencyStop", MethodHandler, client);
        await client.SetMethodHandlerAsync("ResetErrorStatus", MethodHandler, client);

        await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChange, client);
    }
    public async ValueTask DisposeAsync()
    {
        if (client != null)
        {
            await client.CloseAsync();
            client.Dispose();
        }
    }
}
