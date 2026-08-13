using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Collectors.Sensors;

public sealed class SshSensorProvider : ISensorProvider
{
    public const string GpuMarker = "@@vantage:gpu";

    private readonly ISshCommandRunner runner;

    public SshSensorProvider(ISshCommandRunner runner)
    {
        this.runner = runner;
    }

    public static string BuildCommand() =>
        "for z in /sys/class/thermal/thermal_zone*; do " +
        "[ -r \"$z/temp\" ] && echo \"$(cat \"$z/type\" 2>/dev/null):$(cat \"$z/temp\" 2>/dev/null)\"; done ; " +
        $"echo '{GpuMarker}' ; " +
        $"(command -v nvidia-smi >/dev/null 2>&1 && nvidia-smi {NvidiaSmiParser.QueryArgs} 2>/dev/null || true)";

    public static SensorSnapshot ParseBundle(string bundle)
    {
        var normalized = bundle.Replace("\r\n", "\n");
        var markerIndex = normalized.IndexOf(GpuMarker, StringComparison.Ordinal);

        string thermalPart;
        string gpuPart;
        if (markerIndex < 0)
        {
            thermalPart = normalized;
            gpuPart = "";
        }
        else
        {
            thermalPart = normalized[..markerIndex];
            gpuPart = normalized[(markerIndex + GpuMarker.Length)..];
        }

        return new SensorSnapshot(
            ThermalZoneParser.Parse(thermalPart),
            NvidiaSmiParser.Parse(gpuPart));
    }

    public async Task<SensorSnapshot> GetSnapshotAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var output = await runner.RunAsync(host, BuildCommand(), cancellationToken).ConfigureAwait(false);
        return ParseBundle(output);
    }
}
