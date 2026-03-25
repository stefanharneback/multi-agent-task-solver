using System.Text.Json;
using MultiAgentTaskSolver.Core.Abstractions;
using MultiAgentTaskSolver.Core.Models;

namespace MultiAgentTaskSolver.App.Services;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _settingsFilePath;

    public JsonAppSettingsStore()
    {
        _settingsFilePath = Path.Combine(FileSystem.AppDataDirectory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return CreateDefaultSettings();
        }

        await using var stream = File.OpenRead(_settingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
        return settings ?? CreateDefaultSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        await using var stream = File.Create(_settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }

    private static AppSettings CreateDefaultSettings()
    {
        var workspaceRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MultiAgentTaskSolver",
            "Tasks");

        return new AppSettings
        {
            WorkspaceRootPath = workspaceRoot,
            OpenAiGatewayBaseUrl = "http://localhost:3000",
            DefaultProviderId = "openai",
        };
    }
}
