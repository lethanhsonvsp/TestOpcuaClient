using Opc.Ua.Client;
using Opc.Ua;

public class OpcUaClientService
{
    private readonly OpcUaClient _opcUaClient;
    public List<MonitoredItem> MonitoredItems { get; private set; }
    private Action? _onChange;
    private readonly string _endpointUrl = "opc.tcp://localhost:8080";
    private readonly string _namespaceUri = "http://amr150.rcs_server/";

    public OpcUaClientService()
    {
        _opcUaClient = new OpcUaClient(_endpointUrl, _namespaceUri);
        MonitoredItems = new List<MonitoredItem>();
    }

    public async Task StartAsync()
    {
        await _opcUaClient.ConnectAsync();
        _opcUaClient.MonitoredItemNotificationEvent += OnMonitoredItemNotification;
    }

    public void NotifyChange() => _onChange?.Invoke();

    public void Stop() => _opcUaClient.Disconnect();

    private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e) => NotifyChange();

    #region Properties
    // Các thuộc tính để lấy giá trị từ OPC UA server
    public double? RobotPoseX => GetDoubleValue("RobotPoseX");
    public double? RobotPoseY => GetDoubleValue("RobotPoseY");
    public double? RobotPoseYaw => GetDoubleValue("RobotPoseYaw");
    public string? SlamState => GetStringValue("SlamState");
    public string? SlamStateDetail => GetStringValue("SlamStateDetail");
    public string? CurrentActiveMap => GetStringValue("CurrentActiveMap");
    public double? LocalizationQuality => GetDoubleValue("LocalizationQuality");
    public double? BatteryState => GetDoubleValue("BatteryState");
    public double? BatterySoC => GetDoubleValue("BatterySoC");
    public double? BatteryCycles => GetDoubleValue("BatteryCycles");
    public double? BatteryVoltage => GetDoubleValue("BatteryVoltage");
    public double? BatteryCurrent => GetDoubleValue("BatteryCurrent");
    public double? LinearVelocity => GetDoubleValue("LinearVelocity");
    public double? AngularVelocity => GetDoubleValue("AngularVelocity");
    public double[]? CurrentPath => GetDoubleArrayValue("CurrentPath");
    public double[]? LaserScanData => GetDoubleArrayValue("LaserScanData");

    private double? GetDoubleValue(string name) =>
        _opcUaClient.GetMonitoredItemValue(name)?.Value is double value ? value : null;

    private string? GetStringValue(string name) =>
        _opcUaClient.GetMonitoredItemValue(name)?.Value as string;

    private double[]? GetDoubleArrayValue(string name) =>
        _opcUaClient.GetMonitoredItemValue(name)?.Value as double[];
    #endregion

    #region Method
    // Thêm các phương thức dịch vụ
    
    public ServiceResult StartMapping()
    {
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=StartMapping"), new List<object>(), new List<object>());
    }

    public ServiceResult StopMapping()
    {
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=StopMapping"), new List<object>(), new List<object>());
    }

    public ServiceResult MoveToNode(double x, double y, double yaw, double vmax, double accuracy)
    {
        var inputArguments = new List<object> { x, y, yaw, vmax, accuracy };
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=MoveToNode"), inputArguments, new List<object>());
    }

    public ServiceResult ActivateMap(string mapName)
    {
        var inputArguments = new List<object> { mapName };
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=ActivateMap"), inputArguments, new List<object>());
    }

    public ServiceResult SetInitialPose(double x, double y, double yaw)
    {
        var inputArguments = new List<object> { x, y, yaw };
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=SetInitialPose"), inputArguments, new List<object>());
    }

    public ServiceResult ResetSlamError()
    {
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=ResetSlamError"), new List<object>(), new List<object>());
    }

    public ServiceResult DockToShelf(double x, double y, double yaw, double vmax, double accuracy)
    {
        var inputArguments = new List<object> { x, y, yaw, vmax, accuracy };
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=DockToShelf"), inputArguments, new List<object>());
    }

    public ServiceResult CancelNavigation(bool softStop)
    {
        var inputArguments = new List<object> { softStop };
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=CancelNavigation"), inputArguments, new List<object>());
    }

    public ServiceResult Pause()
    {
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=Pause"), new List<object>(), new List<object>());
    }

    public ServiceResult Resume()
    {
        return _opcUaClient.CallMethod(new NodeId($"ns={_opcUaClient.NamespaceIndex};s=Resume"), new List<object>(), new List<object>());
    }

    #endregion
}
