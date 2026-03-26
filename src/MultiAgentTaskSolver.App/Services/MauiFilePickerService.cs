namespace MultiAgentTaskSolver.App.Services;

public sealed class MauiFilePickerService : IFilePickerService
{
    public async Task<IReadOnlyList<string>> PickFilesAsync(CancellationToken cancellationToken = default)
    {
        var files = await FilePicker.Default.PickMultipleAsync();
        if (files is null)
        {
            return [];
        }

        return files
            .Select(static file => file?.FullPath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
    }
}
