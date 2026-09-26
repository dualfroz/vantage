using System.Diagnostics;
using Vantage.Core.Models;
using Vantage.Core.Ssh;

namespace Vantage.Core.Hosts;

public sealed class SshHostConnectionTester : IHostConnectionTester
{
    public const string ProbeCommand = "uname -srm";

    private readonly ISshCommandRunner runner;
    private readonly HttpClient? metricsClient;
    private readonly TimeSpan timeout;

    public SshHostConnectionTester(ISshCommandRunner runner, HttpClient? metricsClient = null, TimeSpan? timeout = null)
    {
        this.runner = runner;
        this.metricsClient = metricsClient;
        this.timeout = timeout ?? TimeSpan.FromSeconds(15);
    }

    public async Task<ConnectionTestResult> TestAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var errors = HostConfigValidator.Validate(host);
        if (errors.Count > 0)
            return ConnectionTestResult.Invalid(errors);

        var (sshStep, unknownHostKey) = await ProbeSshAsync(host, cancellationToken).ConfigureAwait(false);
        var steps = new List<ConnectionTestStep> { sshStep };

        if (host.UseSudo && sshStep.Succeeded)
            steps.Add(await ProbeSudoAsync(host, cancellationToken).ConfigureAwait(false));

        if (metricsClient is not null)
            steps.Add(await ProbeMetricsAsync(host, cancellationToken).ConfigureAwait(false));

        return new ConnectionTestResult(steps.All(step => step.Succeeded), false, steps, unknownHostKey);
    }

    private async Task<(ConnectionTestStep Step, HostKeyCandidate? UnknownHostKey)> ProbeSshAsync(
        HostConfig host,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var output = await runner.RunAsync(host, ProbeCommand, cancellationToken)
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();
            var detail = string.IsNullOrWhiteSpace(output) ? "Connected" : output.Trim();
            return (new ConnectionTestStep("SSH", true, detail, stopwatch.Elapsed.TotalMilliseconds), null);
        }
        catch (TimeoutException)
        {
            return (new ConnectionTestStep("SSH", false, $"No answer within {timeout.TotalSeconds:0} s", null), null);
        }
        catch (HostKeyVerificationException ex)
        {
            var unknown = ex.Check.Status == HostKeyStatus.Unknown ? ex.Check.Presented : null;
            return (new ConnectionTestStep("Host key", false, ex.Message, null), unknown);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (new ConnectionTestStep("SSH", false, ex.Message, null), null);
        }
    }

    private async Task<ConnectionTestStep> ProbeSudoAsync(HostConfig host, CancellationToken cancellationToken)
    {
        try
        {
            await runner.RunAsync(host, ShellArgs.Privileged(host, "true"), cancellationToken)
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            return new ConnectionTestStep("sudo", true, "Passwordless sudo works", null);
        }
        catch (TimeoutException)
        {
            return new ConnectionTestStep("sudo", false, $"No answer within {timeout.TotalSeconds:0} s", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ConnectionTestStep("sudo", false, ex.Message, null);
        }
    }

    private async Task<ConnectionTestStep> ProbeMetricsAsync(HostConfig host, CancellationToken cancellationToken)
    {
        var endpoint = host.ResolveMetricsEndpoint();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await metricsClient!
                .GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, linked.Token)
                .ConfigureAwait(false);
            stopwatch.Stop();
            var code = (int)response.StatusCode;
            return new ConnectionTestStep(
                "Metrics endpoint",
                response.IsSuccessStatusCode,
                $"{endpoint} answered HTTP {code}",
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ConnectionTestStep("Metrics endpoint", false, $"{endpoint} did not answer within {timeout.TotalSeconds:0} s", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ConnectionTestStep("Metrics endpoint", false, $"{endpoint}: {ex.Message}", null);
        }
    }
}
