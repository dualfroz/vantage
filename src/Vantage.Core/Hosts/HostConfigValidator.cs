using Vantage.Core.Models;

namespace Vantage.Core.Hosts;

public static class HostConfigValidator
{
    public static IReadOnlyList<string> Validate(HostConfig host)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(host.Address))
            errors.Add("Address is required.");
        else if (host.Address.Trim().Contains(' '))
            errors.Add("Address must not contain spaces.");

        if (host.Port is < 1 or > 65535)
            errors.Add("Port must be between 1 and 65535.");

        if (string.IsNullOrWhiteSpace(host.Username))
            errors.Add("Username is required.");

        switch (host.AuthMethod)
        {
            case SshAuthMethod.PrivateKey when string.IsNullOrWhiteSpace(host.PrivateKeyPath):
                errors.Add("Private key path is required for key authentication.");
                break;
            case SshAuthMethod.Password when string.IsNullOrEmpty(host.Password):
                errors.Add("Password is required for password authentication.");
                break;
        }

        if (string.IsNullOrWhiteSpace(host.DiskMount) || !host.DiskMount.StartsWith('/'))
            errors.Add("Disk mount must be an absolute path such as /.");

        if (!string.IsNullOrWhiteSpace(host.MetricsEndpoint) &&
            (!Uri.TryCreate(host.MetricsEndpoint, UriKind.Absolute, out var endpoint) ||
             (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)))
            errors.Add("Metrics endpoint must be an absolute http or https URL.");

        return errors;
    }
}
