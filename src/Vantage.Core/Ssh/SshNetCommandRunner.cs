using Renci.SshNet;
using Renci.SshNet.Common;
using Vantage.Core.Models;

namespace Vantage.Core.Ssh;

public sealed class SshNetCommandRunner : ISshCommandRunner
{
    public async Task<string> RunAsync(HostConfig host, string command, CancellationToken cancellationToken)
    {
        var connectionInfo = BuildConnectionInfo(host);

        using var client = new SshClient(connectionInfo);
        await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);
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

    private static ConnectionInfo BuildConnectionInfo(HostConfig host)
    {
        var authentication = host.AuthMethod switch
        {
            SshAuthMethod.PrivateKey => PrivateKeyAuth(host),
            SshAuthMethod.Password => new PasswordAuthenticationMethod(host.Username, host.Password ?? ""),
            SshAuthMethod.Agent => throw new NotSupportedException(
                "SSH agent authentication is not wired yet; configure PrivateKey or Password."),
            _ => throw new ArgumentOutOfRangeException(nameof(host))
        };

        return new ConnectionInfo(host.Address, host.Port, host.Username, authentication);
    }

    private static AuthenticationMethod PrivateKeyAuth(HostConfig host)
    {
        if (string.IsNullOrWhiteSpace(host.PrivateKeyPath))
            throw new InvalidOperationException($"Host '{host.Name}' uses PrivateKey auth but no key path is set.");

        var keyFile = string.IsNullOrEmpty(host.PrivateKeyPassphrase)
            ? new PrivateKeyFile(host.PrivateKeyPath)
            : new PrivateKeyFile(host.PrivateKeyPath, host.PrivateKeyPassphrase);

        return new PrivateKeyAuthenticationMethod(host.Username, keyFile);
    }
}
