using KikisenApp.Core;

namespace KikisenApp.Core.Tests;

public sealed class VbCablePackageSelectorTests
{
    [Fact]
    public void SelectFromHtml_FindsOfficialDownloadUrl()
    {
        const string html = """
            <html>
            <body>
            <a href="https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip">download</a>
            </body>
            </html>
            """;

        var package = VbCablePackageSelector.SelectFromHtml(html);

        Assert.Equal("https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack45.zip", package.DownloadUrl);
        Assert.Equal("VBCABLE_Driver_Pack45.zip", package.ArchiveFileName);
    }
}
