using Avalonia.Threading;
using Uwielbienia.Core;

namespace Uwielbienia.App.Services;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public Task<T> InvokeAsync<T>(Func<T> action) =>
        Dispatcher.UIThread.CheckAccess() ? Task.FromResult(action()) : Dispatcher.UIThread.InvokeAsync(action).GetTask();
}
