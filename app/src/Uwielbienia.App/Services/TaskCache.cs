namespace Uwielbienia.App.Services;

/// <summary>
/// Wyniki zadań w tle według klucza: każdy klucz liczony raz, ostatnio używane zostają (najstarsze wypadają ponad
/// <paramref name="capacity"/>), a nieudany wynik (<c>null</c>, np. plik chwilowo zajęty) nie zostaje — następna prośba próbuje jeszcze raz.
/// </summary>
public sealed class TaskCache<T>(int capacity) where T : class
{
    private readonly Dictionary<string, Task<T?>> _items = [];
    private readonly LinkedList<string> _order = [];

    public Task<T?> Get(string key, Func<Task<T?>> start)
    {
        lock (_items)
        {
            if (_items.TryGetValue(key, out var cached))
            {
                _order.Remove(key);
                _order.AddFirst(key);
                return cached;
            }
            var task = start();
            _items[key] = task;
            _order.AddFirst(key);
            _ = task.ContinueWith(t => ForgetFailed(key, t), TaskScheduler.Default);
            // Usunięty z pamięci podręcznej wynik może być jeszcze używany — zwalnia go dopiero GC.
            while (_order.Count > capacity)
            {
                _items.Remove(_order.Last!.Value);
                _order.RemoveLast();
            }
            return task;
        }
    }

    private void ForgetFailed(string key, Task<T?> task)
    {
        if (task.IsCompletedSuccessfully && task.Result is not null)
            return;
        lock (_items)
        {
            if (_items.TryGetValue(key, out var cached) && cached == task)
            {
                _items.Remove(key);
                _order.Remove(key);
            }
        }
    }
}
