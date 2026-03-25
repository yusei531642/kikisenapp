using System.Management;
using System.Runtime.Versioning;

namespace KikisenApp.Core;

public enum VoicevoxEngineVariant
{
    DirectMl,
    Nvidia
}

public static class VoicevoxEngineVariantDetector
{
    public static VoicevoxEngineVariant DetectPreferredVariant()
    {
        if (!OperatingSystem.IsWindows())
        {
            return VoicevoxEngineVariant.DirectMl;
        }

        try
        {
            return DetectOnWindows();
        }
        catch
        {
            return VoicevoxEngineVariant.DirectMl;
        }
    }

    [SupportedOSPlatform("windows")]
    private static VoicevoxEngineVariant DetectOnWindows()
    {
        using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterCompatibility FROM Win32_VideoController");
        using var results = searcher.Get();

        foreach (var result in results.Cast<ManagementObject>())
        {
            var name = result["Name"]?.ToString() ?? string.Empty;
            var vendor = result["AdapterCompatibility"]?.ToString() ?? string.Empty;

            if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                vendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            {
                return VoicevoxEngineVariant.Nvidia;
            }
        }

        return VoicevoxEngineVariant.DirectMl;
    }
}
