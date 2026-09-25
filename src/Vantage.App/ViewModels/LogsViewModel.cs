using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vantage.Core.Logs;
using Vantage.Core.Models;

namespace Vantage.App.ViewModels;

public partial class LogsViewModel : ViewModelBase, IHostPanel
{
    private readonly ILogProvider logProvider;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? lastError;
    [ObservableProperty] private LogSource? selectedSource;
    [ObservableProperty] private int selectedLineCount = 200;
    [ObservableProperty] private LogSincePreset selectedSince = LogSince.Presets[0];
    [ObservableProperty] private string filterText = "";
    [ObservableProperty] private bool autoRefresh;
    [ObservableProperty] private bool hasLoaded;
    [ObservableProperty] private string loadedAtText = "";
    [ObservableProperty] private string appliedFilterText = "";

    public LogsViewModel(ILogProvider logProvider)
    {
        this.logProvider = logProvider;
    }

    public ObservableCollection<LogSource> Sources { get; } = new();

    public ObservableCollection<LogLineViewModel> Lines { get; } = new();

    public IReadOnlyList<int> LineCounts { get; } = new[] { 100, 200, 500, 1000 };

    public IReadOnlyList<LogSincePreset> SincePresets => LogSince.Presets;

    public HostConfig? Host { get; private set; }

    public bool RefreshOnTick => AutoRefresh;

    public bool HasSources => Sources.Count > 0;

    public bool HasNoSources => Sources.Count == 0;

    public bool IsJournalSource => SelectedSource?.Kind == LogSourceKind.JournalUnit;

    public string SourceTargetText => SelectedSource is null
        ? ""
        : SelectedSource.Kind == LogSourceKind.JournalUnit
            ? $"journalctl -u {SelectedSource.Target}"
            : $"tail {SelectedSource.Target}";

    public bool IsEmpty => HasLoaded && Lines.Count == 0 && string.IsNullOrEmpty(LastError);

    public string SummaryText => !HasLoaded
        ? ""
        : string.IsNullOrEmpty(AppliedFilterText)
            ? $"{Lines.Count} lines"
            : $"{Lines.Count} lines containing \"{AppliedFilterText}\"";

    public void SetSources(IEnumerable<LogSource> sources)
    {
        var previous = SelectedSource;
        Sources.Clear();
        foreach (var source in sources)
            Sources.Add(source);

        SelectedSource = Sources.FirstOrDefault(s => previous is not null && s.Kind == previous.Kind && s.Target == previous.Target)
                         ?? Sources.FirstOrDefault();
        OnPropertyChanged(nameof(HasSources));
        OnPropertyChanged(nameof(HasNoSources));
    }

    public void SetHost(HostConfig? host)
    {
        Host = host;
        Lines.Clear();
        LastError = null;
        HasLoaded = false;
        LoadedAtText = "";
        AppliedFilterText = "";
        NotifyLines();
    }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (Host is null || SelectedSource is null)
            return;

        var host = Host;
        var source = SelectedSource;
        var filter = string.IsNullOrWhiteSpace(FilterText) ? null : FilterText.Trim();
        var query = new LogQuery
        {
            Lines = SelectedLineCount,
            Filter = filter,
            Since = source.Kind == LogSourceKind.JournalUnit ? SelectedSince.Value : null
        };

        IsBusy = true;
        LastError = null;
        try
        {
            var lines = await logProvider.ReadAsync(host, source, query, cancellationToken);
            if (!ReferenceEquals(host, Host) || !ReferenceEquals(source, SelectedSource))
                return;

            Lines.Clear();
            foreach (var line in lines)
                Lines.Add(new LogLineViewModel(line));

            AppliedFilterText = filter ?? "";
            LoadedAtText = "Loaded " + DateTime.Now.ToString("HH:mm:ss");
            HasLoaded = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
            NotifyLines();
        }
    }

    [RelayCommand]
    private Task ClearFilterAsync()
    {
        FilterText = "";
        return RefreshAsync(CancellationToken.None);
    }

    partial void OnSelectedSourceChanged(LogSource? value)
    {
        OnPropertyChanged(nameof(IsJournalSource));
        OnPropertyChanged(nameof(SourceTargetText));
        Lines.Clear();
        HasLoaded = false;
        NotifyLines();
        if (Host is not null && value is not null)
            _ = RefreshAsync(CancellationToken.None);
    }

    partial void OnSelectedLineCountChanged(int value) => ReloadIfLoaded();
    partial void OnSelectedSinceChanged(LogSincePreset value) => ReloadIfLoaded();
    partial void OnAutoRefreshChanged(bool value) => OnPropertyChanged(nameof(RefreshOnTick));

    private void ReloadIfLoaded()
    {
        if (Host is not null && SelectedSource is not null && HasLoaded)
            _ = RefreshAsync(CancellationToken.None);
    }

    private void NotifyLines()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SummaryText));
    }
}

public sealed class LogLineViewModel : ViewModelBase
{
    public LogLineViewModel(LogLine line)
    {
        Line = line;
    }

    public LogLine Line { get; }

    public string TimeText => Line.Timestamp is { } timestamp
        ? timestamp.ToLocalTime().ToString("MM-dd HH:mm:ss")
        : "";

    public string UnitText => Line.Unit ?? "";

    public string Message => Line.Message;

    public string Raw => Line.Raw;
}
