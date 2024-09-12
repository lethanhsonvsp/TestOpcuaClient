using Opc.Ua.Client;
using Opc.Ua.Configuration;
using Opc.Ua.PubSub;
using Opc.Ua;

public class OpcUaClient
{
    private readonly string _endpointUrl;
    private readonly string _namespaceUri;
    private ApplicationInstance? _application;
    private Session? _session;
    private int _namespaceIndex;
    public int NamespaceIndex => _namespaceIndex; 

    private UaPubSubApplication? _pubSubApplication;
    private readonly Dictionary<string, PropertyState> _monitoredItemValues = new();
    private readonly Dictionary<string, MethodState> _methodNodeIds = new();


    public event MonitoredItemNotificationEventHandler? MonitoredItemNotificationEvent;
    public delegate void MonitoredItemNotificationEventHandler(MonitoredItem item, MonitoredItemNotificationEventArgs e);

    #region Connection Methods
    public OpcUaClient(string endpointUrl, string namespaceUri)
    {
        _endpointUrl = endpointUrl;
        _namespaceUri = namespaceUri;
    }
    public async Task ConnectAsync()
    {
        _application = ConfigureApplication();
        await CheckApplicationCertificateAsync();
        _session = await CreateSessionAsync();
        _namespaceIndex = GetNamespaceIndex(_session, _namespaceUri);

        _pubSubApplication = UaPubSubApplication.Create(CreatePubSubConfiguration());
        //Console.WriteLine("Starting PubSub application...");
        _pubSubApplication.Start();
        _pubSubApplication.DataReceived += OnDataReceived;
        //Console.WriteLine("Subscribed to data changes.");

        //Console.WriteLine($"Connected to server. Namespace index: {_namespaceIndex}");
        CreateSubscription();
    }

    public void Disconnect()
    {
        _session?.Close();
        //Console.WriteLine("Stopping PubSub application...");
        _pubSubApplication?.Stop();
        _pubSubApplication?.Dispose();
        //Console.WriteLine("PubSub application stopped.");
    }
    #endregion

    #region Application Configuration
    private static ApplicationInstance ConfigureApplication()
    {
        return new ApplicationInstance
        {
            ApplicationName = "Simple OPC UA Client",
            ApplicationType = ApplicationType.Client,
            ApplicationConfiguration = new ApplicationConfiguration
            {
                ApplicationName = "Simple OPC UA Client",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    AutoAcceptUntrustedCertificates = true,
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = "Directory",
                        StorePath = "CertificateStores/MachineDefault"
                    }
                },
                ClientConfiguration = new ClientConfiguration(),
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 }
            }
        };
    }

    private async Task CheckApplicationCertificateAsync()
    {
        if (_application == null) throw new InvalidOperationException("Application instance is not configured.");
        _application.ApplicationConfiguration.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
        await _application.CheckApplicationInstanceCertificate(false, 0);
    }
    #endregion

    #region Session Management
    private async Task<Session> CreateSessionAsync()
    {
        if (_application == null) throw new InvalidOperationException("Application instance is not configured.");
        var endpoint = CoreClientUtils.SelectEndpoint(_endpointUrl, useSecurity: false)
                       ?? throw new Exception($"Could not find endpoint at {_endpointUrl}");

        var session = await Session.Create(_application.ApplicationConfiguration,
        new ConfiguredEndpoint(null, endpoint),
        false, "OPC UA Client Session",
        60000,
        new UserIdentity(new AnonymousIdentityToken()),
        null);

        //Console.WriteLine("Session created successfully.");
        return session ?? throw new Exception("Failed to create a session. Session is null.");
    }

    private static int GetNamespaceIndex(Session session, string namespaceUri)
    {
        int namespaceIndex = session.NamespaceUris.GetIndex(namespaceUri);
        if (namespaceIndex == -1)
            throw new Exception($"Namespace URI '{namespaceUri}' not found.");

        //Console.WriteLine($"Namespace Index: {namespaceIndex}");
        return namespaceIndex;
    }
    #endregion

    #region Subscription and Data Monitoring 
    private void CreateSubscription()
    {
        if (_session == null) throw new InvalidOperationException("Session is not established.");

        Subscription subscription = new(_session.DefaultSubscription)
        {
            PublishingInterval = 1000
        };
        _session.AddSubscription(subscription);
        subscription.Create();

        var monitoredItems = CreateMonitoredItems(subscription);
        var methodMonitoredItems = CreateMethodMonitoredItems(subscription);

        // Assign event handler for data changes
        foreach (var item in monitoredItems)
        {
            item.Notification += OnMonitoredItemNotification;
        }

        subscription.AddItems(monitoredItems);
        subscription.ApplyChanges();
    }

    private List<MonitoredItem> CreateMonitoredItems(Subscription subscription)
    {
        var monitoredItemNames = new List<string>
        {
            "RobotPoseX", "RobotPoseY", "RobotPoseYaw", "SlamState", "SlamStateDetail",
            "CurrentActiveMap", "LocalizationQuality", "LaserScanData", "BatteryState",
            "BatterySoC", "BatteryCycles", "BatteryVoltage", "BatteryCurrent",
            "LinearVelocity", "AngularVelocity", "CurrentPath"
        };

        var items = new List<MonitoredItem>();

        foreach (var name in monitoredItemNames)
        {
            var item = new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = name,
                StartNodeId = new NodeId($"ns={_namespaceIndex};s={name}"),
                AttributeId = Attributes.Value
            };

            // Initialize PropertyState for each monitored item
            _monitoredItemValues[name] = new PropertyState(null)
            {
                NodeId = item.StartNodeId,
                BrowseName = new QualifiedName(name, (ushort)_namespaceIndex)
            };

            items.Add(item);
        }

        return items;
    }

    private List<MonitoredItem> CreateMethodMonitoredItems(Subscription subscription)
    {
        var methodNames = new List<string>
        {
            "StartMapping", "StopMapping", "StartLocalization", "StopLocalization", 
            "ActivateMap", "SetInitialPose", "ResetSlamError", "StopCalibrate", "MoveToNode",
            "DockToShelf", "DropTheShelf", "Rotate", "MoveStraight", "DockToCharger",
            "UndockFromCharger", "CancelNavigation", "Pause", "Resume"
        };

        var items = new List<MonitoredItem>();

        foreach (var name in methodNames)
        {
            var item = new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = name,
                StartNodeId = new NodeId($"ns={_namespaceIndex};s={name}"),
                AttributeId = Attributes.Value
            };

            // Initialize MethodState for each monitored item
            _methodNodeIds[name] = new MethodState(null)
            {
                NodeId = item.StartNodeId,
                BrowseName = new QualifiedName(name, (ushort)_namespaceIndex)
            };

            items.Add(item);
        }

        return items;
    }

    public void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        foreach (var value in item.DequeueValues())
        {
            // Xử lý cho các property
            if (_monitoredItemValues.TryGetValue(item.DisplayName, out var propertyState))
            {
                propertyState.Value = value.Value;
                propertyState.Timestamp = value.SourceTimestamp;
                propertyState.StatusCode = value.StatusCode;

                MonitoredItemNotificationEvent?.Invoke(item, e);
            }

            // Xử lý cho các phương thức
            if (_methodNodeIds.TryGetValue(item.DisplayName, out var methodState))
            {
                List<object> inputArguments = new List<object>();
                List<object> outputArguments = new List<object> { 0, string.Empty };  // status, message

                // Nhóm 1: Phương thức không có tham số đầu vào
                if (IsNoInputMethod(item.DisplayName))
                {
                    var result = CallMethod(item.StartNodeId, inputArguments, outputArguments);
                    Console.WriteLine($"{item.DisplayName} result: {outputArguments[1]}");
                }

                // Nhóm 2: Phương thức có tham số đơn
                else if (IsSingleInputMethod(item.DisplayName))
                {
                    inputArguments = GetSingleInputArguments(item.DisplayName);  // Nhận đối số đầu vào
                    var result = CallMethod(item.StartNodeId, inputArguments, outputArguments);
                    Console.WriteLine($"{item.DisplayName} result: {outputArguments[1]}");
                }

                // Nhóm 3: Phương thức di chuyển với nhiều tham số
                else if (IsMovementMethod(item.DisplayName))
                {
                    inputArguments = GetMovementInputArguments(item.DisplayName);  // Nhận đối số đầu vào cho phương thức di chuyển
                    var result = CallMethod(item.StartNodeId, inputArguments, outputArguments);
                    Console.WriteLine($"{item.DisplayName} result: {outputArguments[1]}");
                }
            }
        }
    }

    private bool IsNoInputMethod(string methodName)
    {
        // Phương thức không có tham số
        var noInputMethods = new HashSet<string>
    {
        "StartMapping", "StopMapping", "StartLocalization", "StopLocalization",
        "ResetSlamError", "StopCalibrate", "Pause", "Resume"
    };
        return noInputMethods.Contains(methodName);
    }

    private bool IsSingleInputMethod(string methodName)
    {
        // Phương thức có một tham số đầu vào
        var singleInputMethods = new HashSet<string>
    {
        "ActivateMap", "SetInitialPose", "MoveStraight", "Rotate", "CancelNavigation"
    };
        return singleInputMethods.Contains(methodName);
    }

    private bool IsMovementMethod(string methodName)
    {
        // Phương thức di chuyển có nhiều tham số
        var movementMethods = new HashSet<string>
    {
        "MoveToNode", "DockToShelf", "DropTheShelf", "DockToCharger", "UndockFromCharger"
    };
        return movementMethods.Contains(methodName);
    }
    private List<object> GetSingleInputArguments(string methodName)
    {
        switch (methodName)
        {
            case "ActivateMap":
                return new List<object> { "MapName" };  // Tên bản đồ
            case "SetInitialPose":
                return new List<object> { 1.0, 2.0, 0.5 };
            case "MoveStraight":
                return new List<object> { 1.0, 2.0 };  // Tọa độ x, y
            case "Rotate":
                return new List<object> { 90.0 };  // Góc quay
            case "CancelNavigation":
                return new List<object> { true };  // softStop (boolean)
            default:
                return new List<object>();
        }
    }
    private List<object> GetMovementInputArguments(string methodName)
    {
        // Tham số cho các phương thức di chuyển
        switch (methodName)
        {
            case "MoveToNode":
                return new List<object> { 1.0, 2.0, 0.5, 1.0, 0.1 };
            case "DockToShelf":
                return new List<object> { 1.0, 2.0, 0.5, 1.0, 0.1 };
            case "DropTheShelf":
                return new List<object> { 1.0, 2.0, 0.5, 1.0, 0.1 };
            case "DockToCharger":
                return new List<object> { 1.0, 2.0, 0.5, 1.0, 0.1 };
            case "UndockFromCharger":
                return new List<object> { 1.0, 2.0, 0.5, 1.0, 0.1 };
            default:
                return new List<object>();
        }
    }
    public ServiceResult CallMethod(NodeId methodId, IList<object> inputArguments, IList<object> outputArguments)
    {
        if (_session == null) throw new InvalidOperationException("Session is not established.");

        // Create the method to call request
        CallMethodRequest request = new CallMethodRequest()
        {
            ObjectId = ObjectIds.ObjectsFolder,  // This should be the NodeId of the object that contains the method
            MethodId = methodId,
            InputArguments = inputArguments.Select(a => new Variant(a)).ToArray()
        };

        // Call the method on the server
        CallMethodResultCollection results;
        DiagnosticInfoCollection diagnosticInfos;

        ResponseHeader responseHeader = _session.Call(
        null,
        new CallMethodRequestCollection { request },
        out results,
        out diagnosticInfos);

        // Check if the call was successful
        if (StatusCode.IsBad(results[0].StatusCode))
        {
            Console.WriteLine($"Error calling method: {results[0].StatusCode}");
            return results[0].StatusCode;
        }

        // Copy the output arguments
        for (int i = 0; i < results[0].OutputArguments.Count; i++)
        {
            outputArguments[i] = results[0].OutputArguments[i].Value;
        }

        return ServiceResult.Good;
    }

    #endregion


    #region PubSub Configuration and Data Handling

    public PropertyState? GetMonitoredItemValue(string displayName)
    {
        return _monitoredItemValues.TryGetValue(displayName, out var value) ? value : null;
    }

    public MethodState? GetMethodNodeId(string displayName)
    {
        return _methodNodeIds.TryGetValue(displayName, out var value) ? value : null;
    }
    private PubSubConfigurationDataType CreatePubSubConfiguration()
    {
        try
        {
            var readerGroup = new ReaderGroupDataType
            {
                Name = "GroupReader",
                DataSetReaders = new DataSetReaderDataTypeCollection
                {
                    new DataSetReaderDataType
                    {
                        Name = "DataSetReader",
                        DataSetWriterId = 1,
                        PublisherId = 1,
                        SubscribedDataSet = new ExtensionObject(new TargetVariablesDataType())
                    }
                }
            };

            var connection = new PubSubConnectionDataType
            {
                Name = "MySubscriber",
                TransportProfileUri = Profiles.PubSubMqttJsonTransport,
                Address = new ExtensionObject(new NetworkAddressUrlDataType { Url = "mqtt://localhost:1883" }),
                ReaderGroups = new ReaderGroupDataTypeCollection { readerGroup }
            };

            return new PubSubConfigurationDataType { Connections = new PubSubConnectionDataTypeCollection { connection } };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating PubSub configuration: {ex.Message}");
            throw;
        }
    }

    private void OnDataReceived(object? sender, SubscribedDataEventArgs e)
    {
        foreach (var dataset in e.DataSets)
        {
            foreach (var field in dataset.Fields)
            {
                // Console.WriteLine($"Received data via PubSub: {field.FieldMetaData.Name} = {field.Value}");
            }
        }
    }

    #endregion
}