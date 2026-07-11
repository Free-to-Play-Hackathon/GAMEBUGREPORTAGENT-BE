using FluentAssertions;
using GameBug.Application.Vision;
using Xunit;

namespace GameBug.Application.UnitTests;

public sealed class VisionOptionsTests
{
    [Fact]
    public void Defaults_ShouldKeepVisionSafeOff()
    {
        var options = new VisionOptions();

        options.Enabled.Should().BeFalse();
        options.Required.Should().BeFalse();
        options.Provider.Should().Be("Disabled");
        options.StageVersion.Should().Be("vision-safe-off-v1");
        VisionOptions.IsValid(options).Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(121, 2)]
    [InlineData(20, 0)]
    [InlineData(20, 11)]
    public void Validate_ShouldRejectInvalidLimits(int timeoutSeconds, int maxImagesPerAnalysis)
    {
        var options = new VisionOptions
        {
            TimeoutSeconds = timeoutSeconds,
            MaxImagesPerAnalysis = maxImagesPerAnalysis
        };

        VisionOptions.IsValid(options).Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldRejectRequiredVisionForMvp()
    {
        var options = new VisionOptions { Required = true };

        VisionOptions.IsValid(options).Should().BeFalse();
    }
}
