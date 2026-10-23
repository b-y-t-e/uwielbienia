namespace Uwielbienia.App.Services;

/// <summary>
/// Jedno zadanie naraz, a z czekających najpierw to o najwyższym priorytecie (przy równym — najnowsze):
/// obraz bieżącego slajdu na telefonie nie czeka za kilkudziesięcioma miniaturami panelu.
/// </summary>
public sealed class PriorityGate
{
    private readonly PriorityQueue<TaskCompletionSource, (int Priority, long Order)> _waiting = new();
    private long _order;
    private bool _busy;

    public async Task<T> RunAsync<T>(int priority, Func<Task<T>> work)
    {
        await EnterAsync(priority);
        try
        {
            return await work();
        }
        finally
        {
            Leave();
        }
    }

    private Task EnterAsync(int priority)
    {
        lock (_waiting)
        {
            if (!_busy)
            {
                _busy = true;
                return Task.CompletedTask;
            }
            var turn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiting.Enqueue(turn, (-priority, -++_order));
            return turn.Task;
        }
    }

    private void Leave()
    {
        lock (_waiting)
        {
            if (_waiting.TryDequeue(out var next, out _))
                next.SetResult();
            else
                _busy = false;
        }
    }
}
