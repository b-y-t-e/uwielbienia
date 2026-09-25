namespace Uwielbienia.Core;

/// <summary>Wykonanie kodu na wątku interfejsu — polecenia z sieci muszą zmieniać stan tam, gdzie UI.</summary>
public interface IUiDispatcher
{
    Task<T> InvokeAsync<T>(Func<T> action);
}

/// <summary>Wykonuje od razu — dla testów i kodu bez UI.</summary>
public sealed class ImmediateDispatcher : IUiDispatcher
{
    public Task<T> InvokeAsync<T>(Func<T> action) => Task.FromResult(action());
}
