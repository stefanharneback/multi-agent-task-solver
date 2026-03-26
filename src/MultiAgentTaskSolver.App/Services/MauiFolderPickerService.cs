using CommunityToolkit.Maui.Storage;

namespace MultiAgentTaskSolver.App.Services;

public sealed class MauiFolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var result = await FolderPicker.Default.PickAsync(cancellationToken);
        return result.IsSuccessful && !string.IsNullOrWhiteSpace(result.Folder?.Path)
            ? result.Folder.Path
            : null;
    }
}
