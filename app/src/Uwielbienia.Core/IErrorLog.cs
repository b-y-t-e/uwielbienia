namespace Uwielbienia.Core;

/// <summary>Dziennik błędów, które przechwycono, żeby aplikacja działała dalej — każdy musi zostawić ślad.</summary>
public interface IErrorLog
{
    void Report(Exception exception);
}
