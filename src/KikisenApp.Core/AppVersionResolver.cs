namespace KikisenApp.Core;

public static class AppVersionResolver
{
    public static bool HasUpdate(string latestVersion, params string?[] installedVersions)
    {
        var normalizedLatest = Normalize(latestVersion);
        if (string.IsNullOrWhiteSpace(normalizedLatest))
        {
            return false;
        }

        var normalizedInstalled = installedVersions
            .Select(Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedInstalled.Count == 0)
        {
            return true;
        }

        if (normalizedInstalled.Contains(normalizedLatest, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Version.TryParse(normalizedLatest, out var latest))
        {
            var installedParsable = normalizedInstalled
                .Select(x => Version.TryParse(x, out var parsed) ? parsed : null)
                .Where(x => x is not null)
                .Cast<Version>()
                .ToList();

            if (installedParsable.Count > 0)
            {
                return latest > installedParsable.Max();
            }
        }

        return normalizedInstalled.All(x => !string.Equals(x, normalizedLatest, StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(string? version)
    {
        return (version ?? string.Empty).Trim().TrimStart('v', 'V');
    }
}
