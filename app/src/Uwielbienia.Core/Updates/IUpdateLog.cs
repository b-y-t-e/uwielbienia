namespace Uwielbienia.Core.Updates;

/// <summary>Record of update failures the operator can send, because release builds have no debug output.</summary>
public interface IUpdateLog
{
    void Write(string message);
}
