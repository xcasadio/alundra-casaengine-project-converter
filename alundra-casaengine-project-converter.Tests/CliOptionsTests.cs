using Xunit;

namespace AlundraCasaEngineProjectConverter.Tests;

public class CliOptionsTests
{
    private static readonly string[] RequiredPositionals = { "data-extracted", "out" };

    [Fact]
    public void Parse_WithOnlyPositionalArguments_RunsEveryPhaseAndVerifies()
    {
        var options = CliOptions.Parse(RequiredPositionals);

        Assert.NotNull(options);
        Assert.Equal("data-extracted", options.InputDirectory);
        Assert.Equal("out", options.OutputDirectory);
        Assert.Null(options.MapFilter);
        Assert.True(options.Verify);
        Assert.Equal(int.MaxValue, options.Phase);
    }

    [Fact]
    public void Parse_WithMapList_KeepsEveryIndexInOrder()
    {
        var options = CliOptions.Parse(new[] { "data-extracted", "out", "--maps", "0, 4,10" });

        Assert.NotNull(options);
        Assert.Equal(new[] { 0, 4, 10 }, options.MapFilter);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0,abc,10")]
    [InlineData("4.5")]
    public void Parse_WithANonIntegerMapIndex_FailsInsteadOfThrowing(string mapList)
    {
        // --phase already answers a typo with a message; --maps used to answer with a stack trace.
        var exception = Record.Exception(
            () => CliOptions.Parse(new[] { "data-extracted", "out", "--maps", mapList }));

        Assert.Null(exception);
        Assert.Null(CliOptions.Parse(new[] { "data-extracted", "out", "--maps", mapList }));
    }

    [Fact]
    public void Parse_WithNoVerify_TurnsVerificationOff()
    {
        var options = CliOptions.Parse(new[] { "data-extracted", "out", "--no-verify" });

        Assert.NotNull(options);
        Assert.False(options.Verify);
    }

    [Fact]
    public void Parse_WithoutBothPositionalArguments_Fails()
    {
        Assert.Null(CliOptions.Parse(new[] { "data-extracted" }));
        Assert.Null(CliOptions.Parse(Array.Empty<string>()));
    }

    [Fact]
    public void Parse_WithANonIntegerPhase_Fails()
    {
        Assert.Null(CliOptions.Parse(new[] { "data-extracted", "out", "--phase", "later" }));
    }

    [Fact]
    public void IsFullRun_WithNoMapsFilterAndNoPhaseCeiling_IsTrue()
    {
        var options = CliOptions.Parse(RequiredPositionals);

        Assert.NotNull(options);
        Assert.True(options.IsFullRun);
    }

    [Fact]
    public void IsFullRun_WithAMapsFilter_IsFalse()
    {
        var options = CliOptions.Parse(new[] { "data-extracted", "out", "--maps", "4" });

        Assert.NotNull(options);
        Assert.False(options.IsFullRun);
    }

    [Fact]
    public void IsFullRun_WithAPhaseCeilingBelowTheLastPhase_IsFalse()
    {
        var options = CliOptions.Parse(new[] { "data-extracted", "out", "--phase", "6" });

        Assert.NotNull(options);
        Assert.False(options.IsFullRun);
    }

    [Fact]
    public void IsFullRun_WithAPhaseCeilingAtOrAboveTheLastPhase_IsTrue()
    {
        var options = CliOptions.Parse(new[] { "data-extracted", "out", "--phase", "9" });

        Assert.NotNull(options);
        Assert.True(options.IsFullRun);
    }
}
