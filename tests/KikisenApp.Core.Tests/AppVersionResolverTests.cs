namespace KikisenApp.Core.Tests;

public sealed class AppVersionResolverTests
{
    [Fact]
    public void HasUpdate_ReturnsFalse_WhenSameVersionExistsInInstalledCandidates()
    {
        var result = AppVersionResolver.HasUpdate("v1.2.2", "1.2.1", "1.2.2", "1.2.2.0");

        Assert.False(result);
    }

    [Fact]
    public void HasUpdate_ReturnsTrue_WhenLatestVersionIsHigherThanInstalledCandidates()
    {
        var result = AppVersionResolver.HasUpdate("v1.2.3", "1.2.2", "1.2.2.0");

        Assert.True(result);
    }

    [Fact]
    public void HasUpdate_ReturnsFalse_WhenLatestVersionIsOlderThanInstalledCandidates()
    {
        var result = AppVersionResolver.HasUpdate("v1.2.2", "1.2.3");

        Assert.False(result);
    }
}
