public class RobotDataConsumer
{
    private readonly OpcUaClientService _opcUaClientService;

    public RobotDataConsumer(OpcUaClientService opcUaClientService)
    {
        _opcUaClientService = opcUaClientService;
    }
}
