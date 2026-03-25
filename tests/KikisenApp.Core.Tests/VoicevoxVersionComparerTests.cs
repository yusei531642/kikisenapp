using KikisenApp.Core;

namespace KikisenApp.Core.Tests;

public sealed class VoicevoxVersionComparerTests
{
    [Fact]
    public void IsNewerVersion_ReturnsTrue_WhenInstalledVersionIsMissing()
    {
        Assert.True(VoicevoxVersionComparer.IsNewerVersion("0.25.1", null));
    }

    [Fact]
    public void IsNewerVersion_ReturnsTrue_WhenLatestVersionIsHigher()
    {
        Assert.True(VoicevoxVersionComparer.IsNewerVersion("0.25.2", "0.25.1"));
    }

    [Fact]
    public void IsNewerVersion_ReturnsFalse_WhenVersionsAreSame()
    {
        Assert.False(VoicevoxVersionComparer.IsNewerVersion("v0.25.1", "0.25.1"));
    }
}
