using MultiAgentTaskSolver.Core.Abstractions;

namespace MultiAgentTaskSolver.App.Services;

public sealed class SecureStorageSecretStore : ISecretStore
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await SecureStorage.Default.GetAsync(key);
    }

    public async Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(value))
        {
            SecureStorage.Default.Remove(key);
            return;
        }

        await SecureStorage.Default.SetAsync(key, value);
    }
}
