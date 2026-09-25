using Renci.SshNet;
using Renci.SshNet.Common;
using Vantage.Core.Models;

namespace Vantage.Core.Ssh;

public sealed class SshNetCommandRunner : ISshCommandRunner
{
    private readonly IHostKeyStore hostKeys;

    public SshNetCommandRunner(IHostKeyStore hostKeys)
    {
        this.hostKeys = hostKeys;
    }

    public async Task<string> RunAsync(HostConfig host, string command, CancellationToken cancellationToken)
    {
        var connectionInfo = BuildConnectionInfo(host);
        PreferKnownKeyTypes(connectionInfo, hostKeys.KnownKeyTypes(host.Address, host.Port));

        using var client = new SshClient(connectionInfo);
        HostKeyCheck? rejected = null;
        client.HostKeyReceived += (_, args) => rejected = VerifyHostKey(hostKeys, host, args);

        try
        {
            await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);
        }
        catch (SshConnectionException) when (rejected is not null)
        {
            throw new HostKeyVerificationException(rejected);
        }

        try
        {
            using var sshCommand = client.CreateCommand(command);
            var output = await Task.Factory
                .FromAsync(sshCommand.BeginExecute(), sshCommand.EndExecute)
                .ConfigureAwait(false);

            if (sshCommand.ExitStatus != 0 && !string.IsNullOrEmpty(sshCommand.Error))
                throw new SshException($"Remote command failed (exit {sshCommand.ExitStatus}): {sshCommand.Error.Trim()}");

            return output;
        }
        finally
        {
            client.Disconnect();
        }
    }

    public static HostKeyCheck? VerifyHostKey(IHostKeyStore hostKeys, HostConfig host, HostKeyEventArgs args)
    {
        var check = hostKeys.Check(host.Address, host.Port, args.HostKey);
        args.CanTrust = check.Status == HostKeyStatus.Trusted;
        return args.CanTrust ? null : check;
    }

    public static void PreferKnownKeyTypes(ConnectionInfo connectionInfo, IReadOnlyList<string> knownKeyTypes)
    {
        if (knownKeyTypes.Count == 0)
            return;

        var algorithms = connectionInfo.HostKeyAlgorithms.ToArray();
        var preferred = algorithms.Where(algorithm => knownKeyTypes.Contains(KeyTypeOf(algorithm.Key))).ToArray();
        if (preferred.Length == 0)
            return;

        connectionInfo.HostKeyAlgorithms.Clear();
        foreach (var algorithm in preferred.Concat(algorithms.Except(preferred)))
            connectionInfo.HostKeyAlgorithms.Add(algorithm.Key, algorithm.Value);
    }

    private static string KeyTypeOf(string algorithm) =>
        algorithm is "rsa-sha2-256" or "rsa-sha2-512" ? "ssh-rsa" : algorithm;

    private static ConnectionInfo BuildConnectionInfo(HostConfig host)
    {
        var authentication = host.AuthMethod switch
        {
            SshAuthMethod.PrivateKey => PrivateKeyAuth(host),
            SshAuthMethod.Password => new PasswordAuthenticationMethod(host.Username, host.Password ?? ""),
            _ => throw new ArgumentOutOfRangeException(nameof(host))
        };

        return new ConnectionInfo(host.Address, host.Port, host.Username, authentication);
    }

    private static AuthenticationMethod PrivateKeyAuth(HostConfig host)
    {
        if (string.IsNullOrWhiteSpace(host.PrivateKeyPath))
            throw new InvalidOperationException($"Host '{host.Name}' uses PrivateKey auth but no key path is set.");

        var keyPath = ShellArgs.ExpandHome(host.PrivateKeyPath);
        var keyFile = string.IsNullOrEmpty(host.PrivateKeyPassphrase)
            ? new PrivateKeyFile(keyPath)
            : new PrivateKeyFile(keyPath, host.PrivateKeyPassphrase);

        return new PrivateKeyAuthenticationMethod(host.Username, keyFile);
    }
}
