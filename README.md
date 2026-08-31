# Vantage

A desktop dashboard for a self-hosted homelab: per-host CPU / memory / disk / load / uptime,
Docker container control, and HTTP / TCP health checks. Built with C# and Avalonia 11,
targeting `net6.0`.

Vantage is designed to be dogfooded against a real VPS and lab hosts over SSH, but it runs
fully offline out of the box using deterministic mock providers, so the UI and the whole
data flow work without a single live host.

## Architecture

Two projects plus two test projects:

- `src/Vantage.Core` - models, provider abstractions, parsers, health checks, config store.
  No UI dependency, fully unit tested.
- `src/Vantage.App` - the Avalonia application: views, view models, a hand-rolled sparkline
  control, dependency injection wiring.
- `tests/Vantage.Core.Tests` - parsers, providers, health checks, config store.
- `tests/Vantage.App.Tests` - view models and a headless Avalonia smoke test that loads the
  real window and binds the real view model.

### Provider abstractions

Everything that touches a remote host sits behind an interface, with a real SSH implementation
and a mock implementation:

- `IMetricsProvider` - `SshMetricsProvider` runs one batched shell command per poll
  (`/proc/loadavg`, `/proc/uptime`, `/proc/meminfo`, `df`, two `/proc/stat` snapshots for the
  CPU delta) and parses it; `MockMetricsProvider` synthesizes time-varying, per-host-stable
  metrics so sparklines animate offline.
- `IDockerProvider` - `SshDockerProvider` shells out to `docker ps` / `docker start|stop|restart`;
  `MockDockerProvider` keeps an in-memory container set whose state transitions on actions.
- `IHealthChecker` - `HealthChecker` is a real implementation (HttpClient for HTTP, TcpClient
  for TCP) and works against any reachable endpoint right now.

The SSH transport is isolated in `ISshCommandRunner` / `SshNetCommandRunner`. Because of that,
the SSH providers are exercised in tests with a fake runner feeding captured command output -
no live host needed to test the parse path.

### Metrics parse path

The remote command emits marker lines (`@@vantage:loadavg`, `@@vantage:df`, ...). Each section
has its own pure parser (`LoadAvgParser`, `MemInfoParser`, `DfParser`, `ProcStatParser`,
`CpuUsage`), and `MetricsBundleParser` stitches them into a `HostMetrics`. Every parser is a
pure function with a fixture-based test.

## Wave 2 (depth)

Built additively on top of v1, all in `Vantage.Core` behind the same interface-plus-mock pattern:

- **Metrics history** - `IMetricsStore` / `SqliteMetricsStore` writes a time-series of cpu / memory /
  disk / load into SQLite (`~/.config/vantage/history.db`), with range queries and rolling
  retention (`PruneOlderThanAsync`). The monitor loop records every successful poll.
- **Alerts** - `AlertRule` (threshold + comparison + `For` duration + severity) evaluated per host
  by `AlertRuleEvaluator` (Ok -> Pending -> Firing, with a Resolved transition on recovery).
  `AlertEngine` fans transitions out to sinks: `LogAlertSink` (in-memory alert log),
  `DesktopAlertSink` (via an `IDesktopNotifier` seam), and `WebhookAlertSink` (POSTs JSON).
  The app wires the engine into the poll loop and shows live alert state in an Alerts tab.
- **More collectors** - each is parser + `ISomethingProvider` + SSH impl + mock:
  network rx/tx (`/proc/net/dev` + throughput delta), top processes (`ps`), systemd services
  (`systemctl list-units`), temperatures (`/sys/class/thermal`) and GPU (`nvidia-smi`).
- **Log viewer** - `LogCommand` builds `journalctl -u ... -o short-iso` or `tail -n ... file`
  (with an optional shell-quoted `grep` filter), `LogLineParser` splits timestamp / unit / message,
  behind `ILogProvider` (SSH + mock).
- **Prometheus provider** - `PrometheusTextParser` parses the text exposition format (labels with
  escapes, `+Inf` / `-Inf` / `NaN`, optional timestamp); `PrometheusMetricsProvider` scrapes a
  node_exporter `/metrics` endpoint and maps it to `HostMetrics` (CPU from a two-scrape counter
  delta). Select it with `VANTAGE_PROVIDERS=prometheus`.
- **Multi-host view** - `OverviewViewModel` aggregates online/offline counts, average and peak CPU,
  and firing-alert count; surfaced as a Fleet grid tab.

## Running

Requires the .NET 6 SDK.

```
dotnet build
dotnet test
dotnet run --project src/Vantage.App
```

By default the app uses the mock providers, so it comes up populated with demo hosts and
health checks and needs nothing configured. To point it at real hosts over SSH:

```
VANTAGE_PROVIDERS=ssh dotnet run --project src/Vantage.App
```

`VANTAGE_PROVIDERS` accepts `mock` (default), `ssh`, or `prometheus`.

### Configuration

Host and health-check configuration persists as JSON at
`%APPDATA%/vantage/config.json` (Linux: `~/.config/vantage/config.json`). On first run with no
config file, the app seeds demo data. The persistence layer (`JsonConfigStore`) is done and
tested; for now edit that JSON file and restart to change hosts. An in-app editor is the next
piece of UI (see below).

## Verification status

- `dotnet build` is clean (0 warnings, 0 errors).
- `dotnet test` is green (110 tests): v1 parsers and providers, plus wave-2 coverage - SQLite
  time-series write/read/retention, alert-rule evaluation (pending/firing/resolved and the
  counterfactual "never fires early" cases), alert sinks (webhook posts JSON to a stubbed
  handler, log/desktop sinks), the network / process / systemd / thermal / GPU parsers, log
  command building and line parsing, the Prometheus text parser and node_exporter mapping
  (including two-scrape CPU rate), and the app-side wiring test proving a poll records history
  and fires an alert.
- A headless Avalonia test loads the actual `MainWindow` (Fleet / Host / Alerts / Docker / Health
  tabs), binds the real view model, and asserts the visual tree materialized.

This environment is headless (no X server), so the window's **appearance has not been visually
verified**. Screenshots need to be taken on a machine with a display.

## What is intentionally not in v1

- In-app host / health-check editor forms (config is file-based for now).
- SSH connection pooling (a fresh connection per poll today) and SSH-agent auth
  (`SshAuthMethod.Agent` throws a clear NotSupported until wired).
- Packaging / installers, mobile, live charting library (the sparkline is hand-rolled).
