using System.Text;
using Uwielbienia.Link;

namespace Uwielbienia.Core.Tests;

public class LeaveCommandTests
{
    [Theory]
    [InlineData("""{"cmd":"leave"}""", true)]
    [InlineData("""{"cmd":"next"}""", false)]
    [InlineData("nie json", false)]
    public void Recognizes_leave(string json, bool expected) =>
        Assert.Equal(expected, RemoteCommandHandler.IsLeave(Encoding.UTF8.GetBytes(json)));
}
