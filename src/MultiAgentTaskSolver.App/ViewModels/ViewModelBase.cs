using CommunityToolkit.Mvvm.ComponentModel;

namespace MultiAgentTaskSolver.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    protected async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            IsBusy = true;
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
