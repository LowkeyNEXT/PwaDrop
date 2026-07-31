using System.Text;

namespace PwaDrop.Core;

public static class FileNameSanitizer
{
    private const int MaxFileNameLength = 180;

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string Sanitize(string? displayName, int fallbackIndex)
    {
        var candidate = string.IsNullOrWhiteSpace(displayName)
            ? $"PWADrop item {fallbackIndex + 1}"
            : Path.GetFileName(displayName.Trim());

        var builder = new StringBuilder(candidate.Length);
        foreach (var character in candidate.Normalize(NormalizationForm.FormC))
        {
            builder.Append(character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' || character < 32
                ? '_'
                : character);
        }

        candidate = builder.ToString().Trim().TrimEnd('.', ' ');
        if (candidate is "" or "." or "..")
        {
            candidate = $"PWADrop item {fallbackIndex + 1}";
        }

        var extension = Path.GetExtension(candidate);
        var stem = Path.GetFileNameWithoutExtension(candidate);
        if (ReservedNames.Contains(stem))
        {
            stem = $"_{stem}";
        }

        var maximumStemLength = Math.Max(1, MaxFileNameLength - extension.Length);
        if (stem.Length > maximumStemLength)
        {
            stem = stem[..maximumStemLength];
        }

        return stem + extension;
    }

    public static string MakeUnique(string fileName, ISet<string> claimedNames)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(claimedNames);

        if (claimedNames.Add(fileName))
        {
            return fileName;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var suffixText = $" ({suffix})";
            var maximumStemLength = Math.Max(1, MaxFileNameLength - extension.Length - suffixText.Length);
            var shortenedStem = stem.Length > maximumStemLength ? stem[..maximumStemLength] : stem;
            var candidate = shortenedStem + suffixText + extension;
            if (claimedNames.Add(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Unable to allocate a unique temporary file name.");
    }
}
