using Opc.UaFx;
using Opc.UaFx.Client;

namespace OpcAgent;

class OpcClientConnection
{
    private readonly string nodeId;
    private readonly OpcClient client;
    public OpcClientConnection(string nodeId)
    {
        this.client = new OpcClient("opc.tcp://localhost:4840/");
        client.Connect();
        this.nodeId = nodeId;
    }
    public IEnumerable<OpcValue> GetNodes()
    {
        OpcReadNode[] commands = new OpcReadNode[] {
        new OpcReadNode($"ns=2;s={nodeId}/ProductionStatus", OpcAttribute.DisplayName),
        new OpcReadNode($"ns=2;s={nodeId}/ProductionStatus"),
        new OpcReadNode($"ns=2;s={nodeId}/ProductionRate", OpcAttribute.DisplayName),
        new OpcReadNode($"ns=2;s={nodeId}/ProductionRate"),
        new OpcReadNode($"ns=2;s={nodeId}/WorkorderId", OpcAttribute.DisplayName),
        new OpcReadNode($"ns=2;s={nodeId}/WorkorderId"),
        new OpcReadNode($"ns=2;s={nodeId}/Temperature", OpcAttribute.DisplayName),
        new OpcReadNode($"ns=2;s={nodeId}/Temperature"),
        new OpcReadNode($"ns=2;s={nodeId}/GoodCount", OpcAttribute.DisplayName),
        new OpcReadNode($"ns=2;s={nodeId}/GoodCount"),
        new OpcReadNode($"ns=2;s={nodeId}/BadCount", OpcAttribute.DisplayName),
        new OpcReadNode($"ns=2;s={nodeId}/BadCount"),
        new OpcReadNode($"ns=2;s={nodeId}/DeviceError", OpcAttribute.DisplayName),
        new OpcReadNode($"ns=2;s={nodeId}/DeviceError")
        };
        return client.ReadNodes(commands);
    }
}
