using Bradford.Core.Models;
using Bradford.Core.Services;
using Xunit;

namespace Bradford.Tests;

public class IsToolQueryTests
{
    [Theory]
    [InlineData("When is my bin collected?",          true)]
    [InlineData("What is my council tax band?",       true)]
    [InlineData("Find my nearest library",            true)]
    [InlineData("BD5 8LT",                            true)]
    [InlineData("My postcode is BD1 1AA",             true)]
    [InlineData("What recycling collection days?",    true)]
    [InlineData("I live at 5 High Street",            true)]
    [InlineData("Nearest library to me",              true)]
    [InlineData("Hello how are you?",                 false)]
    [InlineData("What are your opening hours?",       false)]
    [InlineData("How do I appeal a parking fine?",    false)]
    public void IsToolQuery_MatchesExpected(string message, bool expected)
    {
        Assert.Equal(expected, AgentService.IsToolQuery(message));
    }

    [Theory]
    [InlineData("BD1 1AA",     true)]
    [InlineData("BD21 3PQ",    true)]
    [InlineData("BD5 8LT",     true)]
    [InlineData("SW1A 1AA",    false)]  // London postcode — not a Bradford BD postcode
    [InlineData("LS1 1BA",     false)]  // Leeds postcode
    public void IsToolQuery_BradfordPostcodesOnly(string postcode, bool expected)
    {
        Assert.Equal(expected, AgentService.IsToolQuery(postcode));
    }
}

public class ExtractStructuredDataTests
{
    private static ToolResultMessage Msg(string content) =>
        new() { ToolCallId = "t1", Content = content };

    [Fact]
    public void ExtractStructuredData_NoMarkers_ReturnsAllNull()
    {
        var history = new List<LlmMessage> { Msg("No structured data here.") };
        var (addr, bin, lib, ct, ctProp) = AgentService.ExtractStructuredData(history);
        Assert.Null(addr);
        Assert.Null(bin);
        Assert.Null(lib);
        Assert.Null(ct);
        Assert.Null(ctProp);
    }

    [Fact]
    public void ExtractStructuredData_AddressList_Parsed()
    {
        var json = """[{"number":1,"line1":"14 High St","city":"Bradford","postcode":"BD1 1AA"}]""";
        var history = new List<LlmMessage>
        {
            Msg($"[[ADDRESS_LIST]]{json}[[/ADDRESS_LIST]]")
        };
        var (addr, _, _, _, _) = AgentService.ExtractStructuredData(history);
        Assert.NotNull(addr);
        Assert.Single(addr);
        Assert.Equal("14 High St", addr[0].Line1);
    }

    [Fact]
    public void ExtractStructuredData_BinDateCard_Parsed()
    {
        var json = """{"address":"14 High St","greyBin":"Mon 12 May","greenBin":"Mon 19 May","brownBin":"Fri 9 May"}""";
        var history = new List<LlmMessage>
        {
            Msg($"[[BIN_DATE_CARD]]{json}[[/BIN_DATE_CARD]]")
        };
        var (_, bin, _, _, _) = AgentService.ExtractStructuredData(history);
        Assert.NotNull(bin);
        Assert.Equal("Mon 12 May", bin.GreyBin);
    }

    [Fact]
    public void ExtractStructuredData_KeepsLargestAddressList()
    {
        var small = """[{"number":1,"line1":"1 Road","city":"Bradford","postcode":"BD1 1AA"}]""";
        var large = """[{"number":1,"line1":"A","city":"Bradford","postcode":"BD1 1AA"},{"number":2,"line1":"B","city":"Bradford","postcode":"BD1 1AA"}]""";
        var history = new List<LlmMessage>
        {
            Msg($"[[ADDRESS_LIST]]{small}[[/ADDRESS_LIST]]"),
            Msg($"[[ADDRESS_LIST]]{large}[[/ADDRESS_LIST]]"),
        };
        var (addr, _, _, _, _) = AgentService.ExtractStructuredData(history);
        Assert.Equal(2, addr?.Count);
    }

    [Fact]
    public void ExtractStructuredData_MalformedJson_ReturnsNull()
    {
        var history = new List<LlmMessage>
        {
            Msg("[[ADDRESS_LIST]]{not valid json}[[/ADDRESS_LIST]]")
        };
        var (addr, _, _, _, _) = AgentService.ExtractStructuredData(history);
        Assert.Null(addr);
    }
}
