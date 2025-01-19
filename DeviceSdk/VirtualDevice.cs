using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Net.Mime;
using System.Text;
using Azure.Communication.Email;
using Azure;

namespace DeviceSdk;

public class VirtualDevice
{
    private readonly DeviceClient client;
    private readonly OpcClient opcClient;
    private readonly string nodeId;
    private readonly string emailConnectionString;
    private readonly EmailClient senderClient;
    private readonly string senderAddress;
    private readonly string receiverAddress;

    public IEnumerable<OpcValue> job;
    public ErrorFlags previousDeviceError = 0;

    public VirtualDevice(DeviceClient deviceClient, string nodeId, OpcClient opcClient, string senderAddress, string receiverAddress, string sender)
    {
        this.client = deviceClient;
        this.nodeId = nodeId;
        this.opcClient = opcClient;
        this.emailConnectionString = senderAddress;
        this.senderClient = new EmailClient(senderAddress);
        this.receiverAddress = receiverAddress;
        this.senderAddress = sender;
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
        opcClient.Connect();
        OpcReadNode[] commands = new OpcReadNode[] {
        new OpcReadNode($"ns=2;s={nodeId}/ProductionStatus"),
        new OpcReadNode($"ns=2;s={nodeId}/ProductionRate"),
        new OpcReadNode($"ns=2;s={nodeId}/WorkorderId"),
        new OpcReadNode($"ns=2;s={nodeId}/Temperature"),
        new OpcReadNode($"ns=2;s={nodeId}/GoodCount"),
        new OpcReadNode($"ns=2;s={nodeId}/BadCount"),
        new OpcReadNode($"ns=2;s={nodeId}/DeviceError")
        };
        job = opcClient.ReadNodes(commands);
    }

    #region Sending Message (Telemetry) Device to Cloud
    public async Task SendTelemetry()
    {
        Console.WriteLine($"{nodeId} sending telemetry to IoTHub...\n");
        var telemetryData = new
        {
            DeviceName = nodeId,
            ProductionStatus = job.ElementAt(0).Value,
            WorkorderId = job.ElementAt(2).Value,
            Temperature = job.ElementAt(3).Value,
            GoodCount = job.ElementAt(4).Value,
            BadCount = job.ElementAt(5).Value
        };
        await SendMessage(telemetryData);
        Console.WriteLine($"\t {DateTime.Now.ToLocalTime()} > Sending telemetry: {telemetryData}");
    }
    #endregion

    #region Device Twin
    public async Task UpdateTwinAsync()
    {
        var twin = await client.GetTwinAsync();

        var currentErrorValue = (ErrorFlags)job.ElementAt(6).Value;

        var reportedProperties = new TwinCollection {
            ["DeviceError"] = (int)currentErrorValue,
            ["ProductionRate"] = job.ElementAt(1).Value.ToString()
        };

        if (currentErrorValue != previousDeviceError)
        {
            if (currentErrorValue > previousDeviceError)
            {
                var errorData = new
                {
                    IsNewError = 1
                };
                await SendMessage(errorData);
                await SendMessageToEmailsAsync(currentErrorValue-previousDeviceError);
                Console.WriteLine("Email has been sent");
            }

            var changingData = new
            {
                Message = $"DeviceError has changed from {previousDeviceError} to {currentErrorValue}"
            };
            await SendMessage(changingData);
            Console.WriteLine($"{nodeId}: {changingData}");

            previousDeviceError = currentErrorValue;
        }

        await client.UpdateReportedPropertiesAsync(reportedProperties);
    }

    private async Task OnDesiredPropertyChange(TwinCollection desiredProperties, object userContext)
    {
        Console.WriteLine($"\t Desired property change: \n\t {JsonConvert.SerializeObject(desiredProperties)}");
        TwinCollection reportedCollection = new TwinCollection();
        reportedCollection["ProductionRate"] = desiredProperties["ProductionRate"];
        opcClient.Connect();
        opcClient.WriteNode($"ns=2;s={nodeId}/ProductionRate", (int)desiredProperties["ProductionRate"]);
        Console.WriteLine($"ProductionRate has been changed to: {opcClient.ReadNode($"ns=2;s={nodeId}/ProductionRate")}");

        await client.UpdateReportedPropertiesAsync(reportedCollection).ConfigureAwait(false);
    }
    #endregion

    #region Direct Methods - uruchomienie serwisu zdalnie z clouda
    private async Task<MethodResponse> MethodHandler(MethodRequest methodRequest, object userContext)
    {
        Console.WriteLine($"\t METHOD EXECUTED: {methodRequest.Name} on {nodeId}");
        var result = opcClient.CallMethod($"ns=2;s={nodeId}", $"ns=2;s={nodeId}/{methodRequest.Name}");
        if (result != null)
        {
            Console.WriteLine($"{methodRequest.Name} successed");
        }
        else
            Console.WriteLine($"{methodRequest.Name} failed");
        return new MethodResponse(0);
    }
    private async Task<MethodResponse> DefaultServiceHandler(MethodRequest methodRequest, object userContext)
    {
        Console.WriteLine($"\t UNKNOWN METHOD EXECUTED: {methodRequest.Name} on {nodeId}");
        return new MethodResponse(0);
    }
    #endregion

    #region Sending Message
    private async Task SendMessage(object data)
    {
        var dataString = JsonConvert.SerializeObject(data);
        Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataString));
        eventMessage.ContentType = MediaTypeNames.Application.Json;
        eventMessage.ContentEncoding = "utf-8";

        await client.SendEventAsync(eventMessage);
    }
    #endregion

    #region Sending Email
    private async Task SendMessageToEmailsAsync(int error)
    {
        try
        {
            var subject = $"New error occurs on {nodeId}";
            var body = ((ErrorFlags)error).ToString();

            EmailContent emailContent = new EmailContent(subject);
            emailContent.PlainText = body;
            EmailMessage emailMessage = new EmailMessage(senderAddress, receiverAddress, emailContent);
            
            EmailSendOperation emailSendOperation = await senderClient.SendAsync(Azure.WaitUntil.Completed, emailMessage);
        }
        catch (RequestFailedException ex)
        {
            throw new RequestFailedException("Invalid email sender username.");
        }
    }
    #endregion

    public async Task InitializeHandlers()
    {
        await client.SetMethodHandlerAsync("EmergencyStop", MethodHandler, client);
        await client.SetMethodHandlerAsync("ResetErrorStatus", MethodHandler, client);
        await client.SetMethodDefaultHandlerAsync(DefaultServiceHandler, client);

        await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChange, client);
    }
}
