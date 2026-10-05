using PrecastStudio.AI;
using PrecastStudio.Core.Panelization;

namespace PrecastStudio.Core.Tests;

public class PanelCommandParseTests
{
    private static readonly PanelizationOptions Current = new();

    [Fact]
    public void Valid_answer_becomes_options()
    {
        const string json = """
            {"maxPanelWidthMm":2400,"minPanelWidthMm":600,"jointWidthMm":15,"openingClearanceMm":150,"markPrefix":"PW","explanation":"Đã đổi tấm tối đa 2400 mm, khe 15 mm."}
            """;

        var result = ClaudePanelCommandInterpreter.Parse(json, Current);

        Assert.Equal(2400, result.Options.MaxPanelWidth);
        Assert.Equal(15, result.Options.JointWidth);
        Assert.Equal("PW", result.Options.MarkPrefix);
        Assert.Contains("2400", result.Explanation);
    }

    [Fact]
    public void Values_that_break_the_rules_are_rejected_even_if_the_model_proposes_them()
    {
        const string json = """
            {"maxPanelWidthMm":1000,"minPanelWidthMm":600,"jointWidthMm":20,"openingClearanceMm":150,"markPrefix":"W","explanation":"ok"}
            """;

        var ex = Assert.Throws<PanelCommandException>(() => ClaudePanelCommandInterpreter.Parse(json, Current));
        Assert.Contains("twice the min panel width", ex.Message);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"maxPanelWidthMm":2400}""")]
    public void Unreadable_answer_gives_a_friendly_error(string json)
    {
        Assert.Throws<PanelCommandException>(() => ClaudePanelCommandInterpreter.Parse(json, Current));
    }
}
