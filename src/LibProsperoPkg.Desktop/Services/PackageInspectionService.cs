using Avalonia.Media.Imaging;
using LibProsperoPkg.PKG;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LibProsperoPkg.Desktop.Services;

public sealed record PackageEntryInfo(
    string Path,
    long Size,
    long CompressedSize,
    bool IsCompressed,
    bool IsEncrypted,
    string Kind);

public sealed class PackageInspectionResult
{
    public required string PackagePath { get; init; }
    public required string PackageType { get; init; }
    public required string PackageContentId { get; init; }
    public required string PackageTitleId { get; init; }
    public required string PackageTitle { get; init; }
    public required string PackageVersion { get; init; }
    public required string PackageContentVersion { get; init; }
    public required string PackageMasterVersion { get; init; }
    public required string PackageDrmType { get; init; }
    public required string PackageContentType { get; init; }
    public required string SignedByte { get; init; }
    public required string PackageBodyOffset { get; init; }
    public required string PackageBodySize { get; init; }
    public required string PackageSize { get; init; }
    public required string PackageEntryCount { get; init; }
    public required string PackageStatusMessage { get; init; }
    public Bitmap? IconPreview { get; init; }
    public Bitmap? BackgroundPreview { get; init; }
    public IReadOnlyList<PackageEntryInfo> Entries { get; init; } = Array.Empty<PackageEntryInfo>();
}

public static class PackageInspectionService
{
    public static PackageInspectionResult Inspect(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Package file was not found.", packagePath);
        }

        ProsperoPkg pkg = ProsperoPkgReader.Read(packagePath);
        long packageSize = new FileInfo(packagePath).Length;
        long bodyOffset = pkg.Header is null ? 0L : (long)pkg.Header.BodyOffset;
        long bodySize = pkg.Header is null ? 0L : (long)pkg.Header.BodySize;

        long dataBaseOffset = GetDataBaseOffset(pkg);
        var entries = BuildEntries(packagePath, pkg);
        byte[]? paramJson = ReadEntryBytes(packagePath, pkg.Entries, dataBaseOffset, "param.json", "sce_sys/param.json", "param.sfo");
        string contentId = pkg.Header?.ContentId ?? string.Empty;
        string title = ReadTitle(paramJson);
        string titleId = ReadTitleId(paramJson, contentId);
        string version = ReadString(paramJson, "contentVersion") ?? "unknown";
        string contentVersion = ReadString(paramJson, "contentVersion") ?? string.Empty;
        string masterVersion = ReadString(paramJson, "masterVersion") ?? string.Empty;
        string drmType = ReadString(paramJson, "applicationDrmType") ?? "unknown";
        string packageContentType = pkg.Header is null ? "unknown" : $"0x{pkg.Header.ContentType:X8}";

        if (string.IsNullOrWhiteSpace(title))
        {
            title = titleId;
        }

        Bitmap? icon = LoadPreviewBitmapFromEntries(packagePath, pkg, dataBaseOffset,
            "icon0.png", "icon0.dds", "sce_sys/icon0.png", "sce_sys/icon0.dds");

        Bitmap? background = LoadPreviewBitmapFromEntries(packagePath, pkg, dataBaseOffset,
            "pic0.png", "pic0.dds", "sce_sys/pic0.png", "sce_sys/pic0.dds",
            "pic1.png", "pic1.dds", "sce_sys/pic1.png", "sce_sys/pic1.dds",
            "pic2.png", "pic2.dds", "sce_sys/pic2.png", "sce_sys/pic2.dds");

        return new PackageInspectionResult
        {
            PackagePath = packagePath,
            PackageType = pkg.Type.ToString(),
            PackageContentId = contentId,
            PackageTitleId = titleId,
            PackageTitle = title,
            PackageVersion = version,
            PackageContentVersion = contentVersion,
            PackageMasterVersion = masterVersion,
            PackageDrmType = drmType,
            PackageContentType = packageContentType,
            SignedByte = pkg.Fih is null ? "n/a" : $"0x{pkg.Fih.SignedByte:X2}",
            PackageBodyOffset = FormatHex(bodyOffset),
            PackageBodySize = FormatHex(bodySize),
            PackageSize = FormatHex(packageSize),
            PackageEntryCount = entries.Count.ToString(),
            PackageStatusMessage = entries.Count == 0 ? "PACKAGE OPENED, NO VISUAL ENTRIES FOUND" : "PACKAGE LOADED",
            IconPreview = icon,
            BackgroundPreview = background,
            Entries = entries,
        };
    }

    public static void ExtractFoundFiles(string packagePath, string outputFolder, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFolder);
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Package file was not found.", packagePath);
        }

        ProsperoPkg pkg = ProsperoPkgReader.Read(packagePath);
        Directory.CreateDirectory(outputFolder);

        foreach (ProsperoPkgEntry entry in pkg.Entries.OrderBy(entry => entry.DataOffset))
        {
            if (entry.Encrypted)
            {
                log?.Invoke($"Skipped encrypted entry {entry.Name ?? entry.RawId.ToString("X8")}.");
                continue;
            }

            byte[]? bytes = ReadEntryBytes(packagePath, entry, GetDataBaseOffset(pkg));
            if (bytes is null)
            {
                log?.Invoke($"Skipped unreadable entry {entry.Name ?? entry.RawId.ToString("X8")}.");
                continue;
            }

            string relativePath = NormalizePath(entry.Name ?? entry.RawId.ToString("X8"));
            string destinationPath = Path.Combine(outputFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.WriteAllBytes(destinationPath, bytes);
            log?.Invoke($"Extracted {relativePath}");
        }
    }

    private static List<PackageEntryInfo> BuildEntries(string packagePath, ProsperoPkg pkg)
    {
        return pkg.Entries
            .OrderBy(entry => entry.DataOffset)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry =>
            {
                string name = NormalizePath(entry.Name ?? $"entry_{entry.RawId:X8}");
                string kind = GetKind(name, entry.RawId);
                return new PackageEntryInfo(
                    Path: name,
                    Size: entry.DataSize,
                    CompressedSize: entry.DataSize,
                    IsCompressed: false,
                    IsEncrypted: entry.Encrypted,
                    Kind: kind);
            })
            .ToList();
    }

    private static Bitmap? LoadPreviewBitmapFromEntries(string packagePath, ProsperoPkg pkg, long dataBaseOffset, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            ProsperoPkgEntry? entry = FindEntry(pkg.Entries, candidate);
            if (entry is null || entry.Encrypted)
            {
                continue;
            }

            byte[]? bytes = ReadEntryBytes(packagePath, entry, dataBaseOffset);
            if (bytes is null || bytes.Length == 0)
            {
                continue;
            }

            Bitmap? bitmap = DecodeBitmap(bytes);
            if (bitmap is not null)
            {
                return bitmap;
            }
        }

        return null;
    }

    private static Bitmap? DecodeBitmap(byte[] bytes)
    {
        try
        {
            using var image = new ImageMagick.MagickImage(bytes);
            byte[] png = image.ToByteArray(ImageMagick.MagickFormat.Png);
            using var stream = new MemoryStream(png);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? ReadEntryBytes(string packagePath, ProsperoPkgEntry entry, long dataBaseOffset)
    {
        if (entry.DataSize == 0)
        {
            return Array.Empty<byte>();
        }

        long offset = dataBaseOffset + entry.DataOffset;
        long length = entry.DataSize;
        if (offset < 0 || length < 0)
        {
            return null;
        }

        using var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (offset + length > stream.Length)
        {
            return null;
        }

        stream.Position = offset;
        byte[] buffer = new byte[checked((int)length)];
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                return null;
            }

            total += read;
        }

        return buffer;
    }

    private static byte[]? ReadEntryBytes(string packagePath, IEnumerable<ProsperoPkgEntry> entries, long dataBaseOffset, params string[] candidates)
    {
        foreach (string candidate in candidates)
        {
            ProsperoPkgEntry? entry = FindEntry(entries, candidate);
            if (entry is null)
            {
                continue;
            }

            byte[]? bytes = ReadEntryBytes(packagePath, entry, dataBaseOffset);
            if (bytes is not null)
            {
                return bytes;
            }
        }

        return null;
    }

    private static ProsperoPkgEntry? FindEntry(IEnumerable<ProsperoPkgEntry> entries, string name)
    {
        foreach (ProsperoPkgEntry entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Name) &&
                string.Equals(NormalizePath(entry.Name), NormalizePath(name), StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static string GetKind(string name, uint rawId)
    {
        string extension = Path.GetExtension(name).TrimStart('.').ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension;
        }

        return rawId switch
        {
            0x2000 => "JSON",
            _ => "FILE",
        };
    }

    private static string ReadTitle(byte[]? paramJson)
    {
        if (paramJson is null || paramJson.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(paramJson);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            if (root.TryGetProperty("localizedParameters", out JsonElement localized) &&
                localized.ValueKind == JsonValueKind.Object)
            {
                string defaultLanguage = ReadString(localized, "defaultLanguage") ?? "en-US";
                if (localized.TryGetProperty(defaultLanguage, out JsonElement lang) &&
                    lang.ValueKind == JsonValueKind.Object)
                {
                    string? title = ReadString(lang, "titleName");
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        return title;
                    }
                }

                foreach (JsonProperty property in localized.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        string? title = ReadString(property.Value, "titleName");
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            return title;
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return string.Empty;
    }

    private static string ReadTitleId(byte[]? paramJson, string contentId)
    {
        string? titleId = ReadString(paramJson, "titleId");
        if (!string.IsNullOrWhiteSpace(titleId))
        {
            return titleId;
        }

        if (!string.IsNullOrWhiteSpace(contentId) && contentId.Length >= 16)
        {
            return contentId.Substring(7, 9);
        }

        return string.Empty;
    }

    private static string? ReadString(byte[]? paramJson, string name)
    {
        if (paramJson is null || paramJson.Length == 0)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(paramJson);
            return ReadString(doc.RootElement, name);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(name, out JsonElement value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string NormalizePath(string path)
    {
        string normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.StartsWith("uroot/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["uroot/".Length..];
        }

        return normalized;
    }

    private static long GetDataBaseOffset(ProsperoPkg pkg)
    {
        if (pkg.Fih is not null)
        {
            return (long)pkg.Fih.EmbeddedCntOffset;
        }

        return 0L;
    }

    private static string FormatHex(long value) => $"0x{value:X}";
}
