namespace Vantage.Core.Metrics.Parsing;

public static class CpuUsage
{
    public static double Compute(CpuTimes previous, CpuTimes current)
    {
        var totalDelta = current.TotalTime - previous.TotalTime;
        var idleDelta = current.IdleTime - previous.IdleTime;

        if (totalDelta <= 0)
            return 0;

        var busyRatio = (double)(totalDelta - idleDelta) / totalDelta;
        return Math.Clamp(Math.Round(busyRatio * 100.0, 1), 0, 100);
    }
}
