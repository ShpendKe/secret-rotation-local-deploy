using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SecretRotationExtension;

public class SecretRotatorFactory
{
    private readonly Func<string, ISecretClient> _createSecretClient;
    private readonly ILogger<SecretRotator> _logger;
    private readonly ConcurrentDictionary<string, SecretRotator> _secretRotators = [];

    public SecretRotatorFactory(
        ILogger<SecretRotator> logger,
        Func<string, ISecretClient> createSecretClient)
    {
        _logger = logger;
        _createSecretClient = createSecretClient;
    }

    public SecretRotator Create(string tenantId, int rotateSecretsExpiringWithinDays)
    {
        return _secretRotators.GetOrAdd(
            tenantId,
            _ => new SecretRotator(
                _createSecretClient(tenantId),
                rotateSecretsExpiringWithinDays,
                _logger));
    }
}
