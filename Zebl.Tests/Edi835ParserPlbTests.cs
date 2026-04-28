using Zebl.Application.Edi.Parsing;
using Xunit;

namespace Zebl.Tests;

public class Edi835ParserPlbTests
{
    [Fact]
    public void Parse835_CapturesAllPlbAdjustmentPairs()
    {
        const string edi = "ISA*00*          *00*          *ZZ*SENDER         *ZZ*RECEIVER       *260423*1200*^*00501*000000123*0*T*:~" +
                           "GS*HP*SENDER*RECEIVER*20260423*1200*1*X*005010X221A1~" +
                           "ST*835*0001~" +
                           "N1*PR*PAYER~" +
                           "CLP*CLAIM1*1*100*80~" +
                           "CAS*CO*45*20~" +
                           "PLB*PROV1*20251231*WO:ABC*10.00*L6:DEF*-2.50*72:GHI*3.25~" +
                           "SE*7*0001~GE*1*1~IEA*1*000000123~";

        var result = Edi835Parser.Parse(edi);

        Assert.Equal(3, result.ProviderAdjustments.Count);
        Assert.Equal("WO:ABC", result.ProviderAdjustments[0].AdjustmentIdentifier);
        Assert.Equal(10.00m, result.ProviderAdjustments[0].Amount);
        Assert.Equal("L6:DEF", result.ProviderAdjustments[1].AdjustmentIdentifier);
        Assert.Equal(-2.50m, result.ProviderAdjustments[1].Amount);
        Assert.Equal("72:GHI", result.ProviderAdjustments[2].AdjustmentIdentifier);
        Assert.Equal(3.25m, result.ProviderAdjustments[2].Amount);
    }
}

