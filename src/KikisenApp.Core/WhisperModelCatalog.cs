using Whisper.net.Ggml;

namespace KikisenApp.Core;

public sealed record WhisperModelDefinition(
    string Id,
    string DisplayName,
    string Description,
    long ApproximateSizeBytes,
    GgmlType ModelType,
    string FileName)
{
    public string SizeLabel => FormatSize(ApproximateSizeBytes);

    private static string FormatSize(long bytes)
    {
        const double kib = 1024d;
        const double mib = kib * 1024d;
        const double gib = mib * 1024d;

        if (bytes >= gib)
        {
            return $"約 {bytes / gib:F1} GiB";
        }

        return $"約 {bytes / mib:F0} MiB";
    }
}

public static class WhisperModelCatalog
{
    private static readonly IReadOnlyList<WhisperModelDefinition> Models =
    [
        new(
            Id: "tiny",
            DisplayName: "Tiny",
            Description: "いちばん軽いです。速度重視です。",
            ApproximateSizeBytes: 75L * 1024 * 1024,
            ModelType: GgmlType.Tiny,
            FileName: "ggml-tiny.bin"),
        new(
            Id: "base",
            DisplayName: "Base",
            Description: "軽さと精度のバランスです。",
            ApproximateSizeBytes: 142L * 1024 * 1024,
            ModelType: GgmlType.Base,
            FileName: "ggml-base.bin"),
        new(
            Id: "small",
            DisplayName: "Small",
            Description: "精度を少し上げたいとき向けです。",
            ApproximateSizeBytes: 466L * 1024 * 1024,
            ModelType: GgmlType.Small,
            FileName: "ggml-small.bin"),
        new(
            Id: "medium",
            DisplayName: "Medium",
            Description: "かなり重いですが精度は高めです。",
            ApproximateSizeBytes: 1530L * 1024 * 1024,
            ModelType: GgmlType.Medium,
            FileName: "ggml-medium.bin"),
        new(
            Id: "large-v3",
            DisplayName: "Large V3",
            Description: "いちばん重いです。高性能PC向けです。",
            ApproximateSizeBytes: 2900L * 1024 * 1024,
            ModelType: GgmlType.LargeV3,
            FileName: "ggml-large-v3.bin")
    ];

    public static IReadOnlyList<WhisperModelDefinition> GetAll()
    {
        return Models;
    }

    public static WhisperModelDefinition GetById(string? id)
    {
        return Models.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Models[0];
    }
}
