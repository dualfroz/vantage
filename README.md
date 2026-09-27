# Vantage

Vantage is a desktop dashboard for a self-hosted homelab, written in C# with Avalonia 11 on
`net6.0`. It polls every configured Linux host over SSH (or scrapes node_exporter), shows
CPU, memory, disk, load and uptime with live sparklines, and gives each host panels for
Docker containers, network throughput, processes, systemd services, temperature sensors and
logs. It also runs HTTP and TCP health checks, evaluates threshold alert rules, records metric
history in SQLite and includes an editor for the host list. Out of the box it runs on
deterministic mock providers, so every screen works without a single reachable host.

## Screenshots

| Screen | Image |
|---|---|
| Fleet | ![Fleet](docs/screenshots/fleet.png) |
| Host metrics | ![Host metrics](docs/screenshots/host-metrics.png) |
| Docker | ![Docker](docs/screenshots/docker.png) |
| Network | ![Network](docs/screenshots/network.png) |
| Processes | ![Processes](docs/screenshots/processes.png) |
| Services | ![Services](docs/screenshots/services.png) |
| Sensors | ![Sensors](docs/screenshots/sensors.png) |
| Logs | ![Logs](docs/screenshots/logs.png) |
| Alerts | ![Alerts](docs/screenshots/alerts.png) |
| Health | ![Health](docs/screenshots/health.png) |
| Host editor | ![Host editor](docs/screenshots/host-editor.png) |

## Screens

The window has a host list on the left, a monitoring indicator with a "Refresh now" button on
top, five main tabs, and a status line at the bottom. "Manage" next to the host list opens
the host editor.

### Fleet

A summary strip with the number of hosts, online and offline counts, average CPU across
online hosts, the host with the highest CPU and the number of firing alerts. Below it every
host is a card with its status and current CPU, memory and disk usage.

### Host

The header shows the selected host's name, status and `user@address:port`. Seven panels sit
under it:

- **Metrics** - CPU and memory with progress bars and sparklines of the recent history,
  disk usage of the configured mount, load averages (1, 5, 15 min) and uptime. Poll errors
  are shown in full.
- **Docker** - every container (`docker ps -a`) with a running indicator, image and status.
  Start, Stop and Restart act on the selected container.
- **Network** - receive and transmit rates summed over all non-loopback interfaces, each
  with a sparkline, plus a per-interface table with current rates, byte totals and packet
  counters. Rates come from the difference between two samples, so they appear after the
  second refresh. Loopback can be shown with a checkbox.
- **Processes** - the top 25, 50, 100 or 250 processes by CPU. Columns (PID, user, CPU,
  memory, command) sort by clicking their header, a filter matches command, user or PID
  within the loaded list, and the footer says how many rows are shown out of how many were
  loaded. Terminate sends SIGTERM and Kill sends SIGKILL to the selected process after an
  inline confirmation. PID 1 cannot be signalled.
- **Services** - all systemd service units with load, active and sub state, colour coded,
  failed units first. Counters for total, running, failed and inactive units, a state filter
  and a text filter over unit name and description. Start and Restart act on the selected
  unit, Stop asks for confirmation first.
- **Sensors** - every thermal zone from `/sys/class/thermal` as a bar coloured by
  configurable warm and hot thresholds, the hottest reading with a sparkline of its history,
  and NVIDIA GPUs (via `nvidia-smi`) with temperature, utilization and memory use. Hosts
  without thermal zones or GPUs get an explicit empty state.
- **Logs** - reads a configured journal unit (`journalctl -u`) or file (`tail`). Choose the
  number of lines (100 to 1000), a time range for journal sources (last 15 minutes to since
  yesterday) and an exact-text filter that runs on the host as `grep -F`. The panel shows the
  command it reads from, the number of lines and the load time, and can reload on every
  refresh.

Metrics are polled for all hosts on every refresh interval. Network, processes and sensors
reload with each refresh while their panel is open; Docker, services and logs load when the
panel is opened or the host changes, and on demand.

### Alerts

The current state of every alert rule on every host (OK, Pending, Firing) with the rule
name, host name, condition, latest value, severity and, for pending and firing alerts, the
time the state began. Below it, a list of the alerts that fired or resolved while the app
was running.

### Health

HTTP and TCP checks with status, message, latency and the time of the last check. "Run all"
checks everything, "Check" on a row runs one check. HTTP checks are Up when the status code
is inside the expected range (200-399 by default) and Degraded otherwise; TCP checks are Up
when the connection opens. Both are Down on timeout or connection errors.

### Host editor

Add, edit and remove hosts. The form covers display name, address, SSH port, username,
authentication (private key with optional passphrase, or password), whether service and
process actions run through `sudo -n`, the disk mount used for disk usage and an optional
node_exporter URL. Drafts with unsaved changes are marked in the list, invalid input is
reported field by field, Save writes `config.json` and updates the host list immediately,
and removing a host asks for confirmation.

"Test connection" uses the values in the form, saved or not. With SSH providers it connects
and runs `uname -srm`, reports the result and round-trip time, checks passwordless sudo when
it is enabled, and in Prometheus mode also requests the metrics endpoint. With mock
providers it validates the settings and says plainly that no connection was made.

When the host presents a key that no known_hosts file lists, the test shows the key type and
its SHA256 fingerprint next to a "Trust and save key" button. Trusting writes the key to
Vantage's own `known_hosts` and runs the test again. A key that differs from a listed one is
never offered for trust; the test fails and shows both fingerprints.

## Build and run

Requires the .NET 6 SDK.

```
dotnet build
dotnet run --project src/Vantage.App
```

The provider set is chosen with the `VANTAGE_PROVIDERS` environment variable:

| Value | Behaviour |
|---|---|
| `mock` (default) | Simulated metrics, containers, interfaces, processes, units, sensors and logs |
| `ssh` | Everything is collected over SSH |
| `prometheus` | Host metrics from node_exporter, everything else over SSH |

```
VANTAGE_PROVIDERS=ssh dotnet run --project src/Vantage.App
```

Mock mode is fully interactive: container and service actions change state, killed
processes disappear, log filters and time ranges apply, and the network counters grow over
time. Health checks are always real network checks.

## Configuration

Settings live in `config.json` under the application data folder
(`~/.config/vantage/config.json` on Linux, `%APPDATA%\vantage\config.json` on Windows). If
the file is missing or lists no hosts and no health checks, Vantage starts with a demo set
that is written to disk the first time a host is saved in the editor. Hosts are managed in
the app; the other sections are edited in the file and read at startup. Anything Vantage
has to tell you about the file at startup (a migrated setting, a secret it could not read)
appears in a banner above the tabs until dismissed.

```json
{
  "hosts": [
    {
      "id": "3f2a9c0e5b7d4e1f8a6b2c4d9e0f1a2b",
      "name": "edge-01",
      "address": "192.0.2.11",
      "port": 22,
      "username": "ops",
      "authMethod": "privateKey",
      "privateKeyPath": "~/.ssh/id_ed25519",
      "privateKeyPassphraseRef": "keyring:3f2a9c0e5b7d4e1f8a6b2c4d9e0f1a2b/passphrase",
      "useSudo": true,
      "diskMount": "/",
      "metricsEndpoint": null
    }
  ],
  "healthChecks": [
    { "name": "Gateway HTTP", "kind": "http", "target": "https://example.com", "timeoutMs": 5000,
      "expectedStatusMin": 200, "expectedStatusMax": 399 },
    { "name": "DNS TCP", "kind": "tcp", "target": "1.1.1.1:53" }
  ],
  "alertRules": [
    { "name": "CPU saturated", "metric": "cpu", "comparison": "greaterOrEqual", "threshold": 80,
      "for": "00:00:30", "severity": "warning" }
  ],
  "logSources": [
    { "kind": "journalUnit", "target": "nginx", "label": "nginx" },
    { "kind": "file", "target": "/var/log/syslog", "label": "syslog" }
  ],
  "refreshIntervalSeconds": 5,
  "historyLength": 120,
  "historyRetentionHours": 168,
  "alertWebhookUrl": null,
  "desktopNotifications": true,
  "desktopNotificationsPerMinute": 3,
  "temperatureWarnCelsius": 70,
  "temperatureCriticalCelsius": 85
}
```

- `authMethod` is `privateKey` or `password`. A leading `~/` in `privateKeyPath` expands to
  the home directory. Older files with `"authMethod": "agent"` still load: such hosts switch
  to key authentication with the first of `~/.ssh/id_ed25519`, `id_ecdsa` and `id_rsa` that
  exists (or keep their `privateKeyPath`), the file is rewritten once and a notice says what
  changed.
- `passwordRef` and `privateKeyPassphraseRef` point at secrets in the desktop keyring (see
  Security). Without a keyring the secrets are stored as `password` and
  `privateKeyPassphrase` in plain text instead.
- `useSudo` prefixes `systemctl` and `kill` with `sudo -n`, so a missing sudo rule fails
  with a clear error instead of waiting for a password.
- Alert rules take `metric` from `cpu`, `memory`, `disk`, `load1`, `load5` and `load15`,
  `comparison` from `greaterThan`, `greaterOrEqual`, `lessThan` and `lessOrEqual`, a `for`
  duration the condition must hold before the alert fires, a `severity` (`info`, `warning`,
  `critical`) and an optional `hostId` to limit the rule to one host.
- `alertWebhookUrl` receives a JSON POST for every alert that fires or resolves.
- `desktopNotifications` turns operating system notifications for alerts on or off, and
  `desktopNotificationsPerMinute` limits how many of them are shown per minute (at least 1).
- Metric history is stored in `history.db` next to `config.json`. Every successful poll
  writes CPU, memory, disk and load, and rows older than `historyRetentionHours` are pruned
  at startup and hourly.

## Security

**Host keys.** Every SSH connection checks the host key before authenticating. Keys are
looked up in `~/.ssh/known_hosts`, `/etc/ssh/ssh_known_hosts` and Vantage's own `known_hosts`
next to `config.json`; plain and hashed (`|1|...`) entries, `[host]:port` entries for
non-standard ports, `*`, `?` and `!` patterns and `@revoked` markers are understood
(`@cert-authority` lines are skipped). When a host has listed keys, their algorithms are
offered first so the server presents a key that can be checked. The outcome is one of:

| Host key | Result |
|---|---|
| Listed for the host | Connection continues |
| Not listed | Connection refused with the key type and SHA256 fingerprint; trust it from "Test connection" in the host editor or add it to `~/.ssh/known_hosts` yourself |
| Differs from a listed key of the same type | Connection refused as `HOST KEY CHANGED`, showing the presented and the expected fingerprints and the file and line to edit; there is no way to accept it from the app |
| Marked `@revoked` | Connection refused |

Vantage only ever writes to its own `known_hosts`; `~/.ssh/known_hosts` is read but never
changed.

**Passwords and passphrases.** On Linux, passwords and key passphrases are kept in the
desktop keyring through the Secret Service D-Bus API (provided by GNOME Keyring, for
example), using the `Tmds.DBus.Protocol` library that Avalonia already ships with. Each
secret is an item labelled "Vantage password for edge-01" with the attributes
`application=vantage` and `reference=keyring:<host id>/<password|passphrase>`, and
`config.json` stores only that reference. A locked keyring is unlocked through its own
prompt. Secrets of removed hosts, and secrets no longer used after switching the
authentication method, are deleted from the keyring on save. Plain secrets from an older
`config.json` are moved into the keyring the first time the configuration is saved. When no
keyring is reachable (no session bus, no Secret Service, or another operating system),
secrets stay in plain text in `config.json` as before and the host editor says so. Other
operating systems can plug in their own store through `ISecretStore`.

**Notifications.** On Linux, alerts that fire or resolve are shown as desktop notifications
through `org.freedesktop.Notifications.Notify`, with urgency taken from the alert severity.
At most three notifications are shown per minute (`desktopNotificationsPerMinute`); when
alerts are held back, the next
notification says how many. Every alert also stays in the in-app list.

## How it works

Everything that touches a host sits behind an interface with an SSH implementation and a mock:
`IMetricsProvider`, `IDockerProvider`, `INetworkProvider`, `IProcessProvider`,
`ISystemdProvider`, `ISensorProvider`, `ILogProvider` and `IHostConnectionTester`. The SSH
implementations share one `ISshCommandRunner` built on SSH.NET, which makes them testable
with captured command output instead of live hosts.

- Metrics: one batched command per poll prints marked sections (`/proc/loadavg`,
  `/proc/uptime`, `/proc/meminfo`, `df`, two `/proc/stat` snapshots); each section has its own
  pure parser and `MetricsBundleParser` assembles a `HostMetrics`.
- Prometheus: `PrometheusTextParser` handles the text exposition format (label escapes,
  `+Inf`, `-Inf`, `NaN`, optional timestamps) and `PrometheusNodeMapper` turns node_exporter
  series into `HostMetrics`, with CPU from the counter delta between two scrapes.
- Network: `/proc/net/dev` parsed by `NetDevParser`, rates from `NetworkThroughput`.
- Processes: `ps -eo pid,user,pcpu,pmem,comm --sort=-pcpu`; signals with `kill -s TERM|KILL`.
- Services: `systemctl list-units --type=service --all`; actions with `systemctl start|stop|restart`.
- Sensors: thermal zones and `nvidia-smi --query-gpu` in one command.
- Logs: `LogCommand` builds `journalctl -u ... -o short-iso` or `tail -n`, with `--since` and
  `grep -F`; `LogLineParser` splits timestamp, unit and message.
- Alerts: `AlertRuleEvaluator` moves each rule and host through OK, Pending and Firing and
  emits fired and resolved transitions; `AlertEngine` sends them to the in-app list, the
  in-memory alert log, the desktop notifier and the optional webhook.
- Host keys: `KnownHostsFile` parses and matches known_hosts lines, `KnownHostsStore` decides
  trusted, unknown, changed or revoked, and `SshNetCommandRunner` applies the decision in
  SSH.NET's `HostKeyReceived` event.
- Secrets: `KeyringConfigStore` wraps `JsonConfigStore` and moves secrets between
  `config.json` and an `ISecretStore`; `SecretServiceStore` is the Secret Service client.

Every value that reaches a remote shell (unit names, file paths, filters, time ranges) is
single-quoted by `ShellArgs.Quote`.

## Tests

```
dotnet test
```

- `tests/Vantage.Core.Tests` - every parser against captured output, the SSH providers through
  a fake command runner (including the commands they send for kill and systemctl actions and
  their quoting), the mock providers, the Prometheus provider through a stubbed HTTP handler,
  health checks, alert evaluation and sinks, the notification rate limit, SQLite history,
  the config store with the agent migration and the keyring wrapper, known_hosts matching
  against keys and hashed entries made by `ssh-keygen`, the host key decision on real SSH.NET
  event arguments, host validation and the connection testers.
- `tests/Vantage.App.Tests` - view models for every screen (sorting, filtering,
  confirmations, throughput sampling, thresholds, the host editor with host key trust and its
  persistence, panel loading when a tab opens or the host changes), startup notices, the
  alert and history wiring, and headless
  Avalonia tests that open the real window and check that every host panel and the host
  editor materialize with their data.

## Project layout

```
src/Vantage.Core          models, providers, parsers, alerts, history, config, SSH transport
  Collectors/             network, processes, systemd, sensors
  Docker/ Health/ Logs/ Metrics/ Prometheus/ History/ Alerts/
  Hosts/                  host validation and connection testing
  Ssh/                    SSH.NET command runner, host key verification, shell quoting
  Secrets/                keyring storage for passwords and passphrases
  Desktop/                session D-Bus connection
src/Vantage.App           Avalonia application
  Views/                  one view per tab and host panel
  ViewModels/             CommunityToolkit.Mvvm view models
  Controls/               sparkline control
tests/Vantage.Core.Tests
tests/Vantage.App.Tests
docs/screenshots          images used in this README
```

## Limitations

- Remote hosts must be Linux: metrics read `/proc`, services need systemd, journal logs need
  `journalctl`, GPU readings need an NVIDIA card with `nvidia-smi`.
- SSH agent authentication is not supported, because SSH.NET 2024.1.0 has no agent client.
  Use a private key file or a password.
- Host keys are checked against the address exactly as configured; unlike OpenSSH, Vantage
  does not also check the resolved IP address, and host certificates (`@cert-authority`)
  are not supported.
- Each command opens a fresh SSH connection; there is no connection pooling.
- The keyring is only used on Linux with a Secret Service provider. Elsewhere, or without
  one, passwords and key passphrases are stored in plain text in `config.json`.
- Secrets travel to the Secret Service over the session bus with the `plain` algorithm, which
  is allowed by the specification but not encrypted on the bus.
- Desktop notifications exist only on Linux desktops that implement
  `org.freedesktop.Notifications`; elsewhere alerts are only shown in the app and sent to the
  webhook.
- The process list holds the top N processes by CPU from `ps`, and the command column is the
  executable name without arguments.
- Health checks, alert rules, log sources and the notification setting are edited in
  `config.json`, not in the app.

## License

MIT, see [LICENSE](LICENSE).
