using System.Reflection;
using Zebl.Api.Services;
using Xunit;

namespace Zebl.Tests;

public class ClaimBatchControlNumberValidationTests
{
    [Fact]
    public void BuildSingleInterchange_WhenIsa13Missing_ThrowsAndStopsGeneration()
    {
        var method = typeof(ClaimBatchService).GetMethod(
            "BuildSingleInterchange",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var claimInterchanges = new List<string>
        {
            "ISA*00*          *00*          *ZZ*SENDID         *ZZ*RECVID         *240101*1200*^*00501**0*T*:~" +
            "GS*HC*SENDER*RECEIVER*20240101*1200*123*X*005010X222A1~" +
            "ST*837*456~BHT*0019*00*1*20240101*1200*CH~CLM*1*0~SE*4*456~GE*1*123~IEA*1*~"
        };

        var ex = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(null, new object[] { claimInterchanges }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal("Missing ISA13 control number.", ex.InnerException!.Message);
    }
}
