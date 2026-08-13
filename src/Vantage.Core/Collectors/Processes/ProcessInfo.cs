namespace Vantage.Core.Collectors.Processes;

public readonly record struct ProcessInfo(
    int Pid,
    string User,
    double CpuPercent,
    double MemoryPercent,
    string Command);
