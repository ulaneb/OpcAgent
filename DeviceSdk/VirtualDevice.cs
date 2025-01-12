using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using Opc.UaFx;
using Opc.UaFx.Client;
using OpcAgent;
using System.Net.Mime;
using System.Text;
using Azure.Communication.Email;
using Azure;

namespace DeviceSdk;

public class VirtualDevice
{
    private readonly DeviceClient client;
    private readonly OpcNodeInfo opcNodeInfo;
    private readonly OpcClientConnection opcClientConnection;
    private readonly OpcClient opcClient;
    private readonly string nodeId;
    private readonly string senderAddress;
    private readonly EmailClient senderClient;
    private readonly string sender;
    private readonly string receiverAddress;
    private readonly EmailRecipients receiver;

    public IEnumerable<OpcValue> job;
    public ErrorFlags previousDeviceError = 0;

    public VirtualDevice(DeviceClient deviceClient, string nodeId, OpcClient opcClient, string senderAddress, string receiverAddress, string sender)
    {
        this.client = deviceClient;
        this.nodeId = nodeId;
        this.opcClientConnection = new OpcClientConnection(nodeId);
        this.opcClient = opcClient;
        this.senderAddress = senderAddress;
        senderClient = new EmailClient(senderAddress);
        this.receiverAddress = receiverAddress;
        this.sender = sender;
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
    public async Task SendTelemetry()
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
        await SendMessage(telemetryData);
        Console.WriteLine($"\t {DateTime.Now.ToLocalTime()} > Sending message: Data [{telemetryData}");

        await Task.Delay(5000);
    }
    #endregion

    #region Device Twin
    public async Task UpdateTwinAsync()
    {
        var twin = await client.GetTwinAsync();
        Console.WriteLine($"\n Initial twin value received: \n {JsonConvert.SerializeObject(twin, Formatting.Indented)}");
        Console.WriteLine();

        var currentErrorValue = (ErrorFlags)job.ElementAt(13).Value;

        var reportedProperties = new TwinCollection {
            ["DeviceError"] = (int)currentErrorValue, ///!!! currentErrorValue.ToString(),
            ["ProductionRate"] = job.ElementAt(3).Value.ToString()
        };

        if (currentErrorValue != previousDeviceError)
        {
            Console.WriteLine("Device Error Changes");
            if (currentErrorValue > previousDeviceError)
            {
                var errorData = new
                {
                    IsNewError = 1
                };
                await SendMessage(errorData);
                Console.WriteLine($"{errorData}");
                await SendMessageToEmailsAsync(currentErrorValue-previousDeviceError);
                Console.WriteLine("Email sent");
            }

            var changingData = new
            {
                Message = $"Device error has changed from {previousDeviceError} to {currentErrorValue}"
            };
            await SendMessage(changingData);
            Console.WriteLine($"Reported Twin Updated: {changingData}");

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
        opcClient.Connect();
        opcClient.WriteNode($"ns=2;s={nodeId}/ProductionRate", (int)desiredProperties["ProductionRate"]);
        Console.WriteLine($"Updated: {opcClient.ReadNode($"ns=2;s={nodeId}/ProductionRate")}");

        await client.UpdateReportedPropertiesAsync(reportedCollection).ConfigureAwait(false);
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
            Console.WriteLine("Failed");
        await Task.Delay(1000);
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
            EmailMessage emailMessage = new EmailMessage(sender, receiverAddress, emailContent);
            
            EmailSendOperation emailSendOperation = await senderClient.SendAsync(Azure.WaitUntil.Completed, emailMessage);
            Console.WriteLine($"{DateTime.Now}: Notification about an error has been sent successfully.");
        }
        catch (RequestFailedException ex)
        {
            throw new RequestFailedException("Invalid email sender username. Please use a username from the list of valid usernames configured by your admin.");
        }
    }
    #endregion

    public async Task InitializeHandlers()
    {
        await client.SetMethodHandlerAsync("EmergencyStop", MethodHandler, client);
        await client.SetMethodHandlerAsync("ResetErrorStatus", MethodHandler, client);

        await client.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertyChange, client);
    }
}
