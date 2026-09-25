using Vantage.Core.Collectors.Network;
using Vantage.Core.Collectors.Processes;
using Vantage.Core.Collectors.Sensors;
using Vantage.Core.Collectors.Systemd;
using Vantage.Core.Hosts;
using Vantage.Core.Logs;
using Vantage.Core.Ssh;

namespace Vantage.App;

public sealed record HostCollectors(
    INetworkProvider Network,
    IProcessProvider Processes,
    ISystemdProvider Systemd,
    ISensorProvider Sensors,
    ILogProvider Logs,
    IHostConnectionTester ConnectionTester,
    IHostKeyStore? HostKeys = null);
