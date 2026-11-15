using System.Text;
using SelfTracker.Domain.Common;

namespace SelfTracker.Domain.Services;

/// <summary>Upload hardening rules (spec 6.2): name sanitising, blocked extensions, content sniffing, inline policy.</summary>
public static class FileRules
{
    public const int MaxNameLength = 200;

    public static readonly IReadOnlySet<string> BlockedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".ps1", ".sh", ".dll", ".msi", ".js", ".vbs", ".scr", ".com",
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".csv", ".json", ".yml", ".yaml", ".xml", ".log", ".cs", ".ts", ".tsx", ".js",
        ".py", ".sql", ".sh", ".ps1", ".html", ".htm", ".svg", ".css", ".mermaid", ".mmd",
    };

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".ts", ".tsx", ".js", ".py", ".sql", ".sh", ".ps1", ".json", ".yml", ".yaml", ".xml", ".css", ".html", ".htm",
    };

    /// <summary>Strip directories, path separators and control characters; cap the length.</summary>
    public static string SanitizeName(string? name)
    {
        var raw = (name ?? "").Replace('\\', '/');
        var lastSlash = raw.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            raw = raw[(lastSlash + 1)..];
        }

        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (!char.IsControl(c) && c is not ('/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'))
            {
                sb.Append(c);
            }
        }

        var cleaned = sb.ToString().Trim().Trim('.');
        if (cleaned.Length == 0)
        {
            cleaned = "file";
        }

        if (cleaned.Length > MaxNameLength)
        {
            var ext = SafeExtension(cleaned);
            cleaned = cleaned[..(MaxNameLength - ext.Length)] + ext;
        }

        return cleaned;
    }

    /// <summary>Lower-case extension including the dot, only [a-z0-9]{1,10}; otherwise empty.</summary>
    public static string SafeExtension(string name)
    {
        var dot = name.LastIndexOf('.');
        if (dot < 0 || dot == name.Length - 1)
        {
            return "";
        }

        var ext = name[dot..].ToLowerInvariant();
        return ext.Length <= 11 && ext[1..].All(char.IsAsciiLetterOrDigit) ? ext : "";
    }

    public static bool IsBlocked(string extension) => BlockedExtensions.Contains(extension);

    /// <summary>Detect the content type from the first bytes; the client's claim is never trusted.</summary>
    public static string Sniff(ReadOnlySpan<byte> head, string extension)
    {
        if (head.StartsWith("%PDF-"u8)) return "application/pdf";
        if (head.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })) return "image/png";
        if (head.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return "image/jpeg";
        if (head.StartsWith("GIF8"u8)) return "image/gif";
        if (head.Length >= 12 && head.StartsWith("RIFF"u8))
        {
            var fourcc = head.Slice(8, 4);
            if (fourcc.SequenceEqual("WEBP"u8)) return "image/webp";
            if (fourcc.SequenceEqual("WAVE"u8)) return "audio/wav";
            if (fourcc.SequenceEqual("AVI "u8)) return "video/x-msvideo";
        }

        if (head.Length >= 12 && head.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = Encoding.ASCII.GetString(head.Slice(8, 4));
            if (brand.StartsWith("M4A", StringComparison.Ordinal)) return "audio/mp4";
            if (brand.StartsWith("qt", StringComparison.Ordinal)) return "video/quicktime";
            return "video/mp4";
        }

        if (head.StartsWith(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
        {
            return extension is ".mkv" ? "video/x-matroska" : extension is ".weba" ? "audio/webm" : "video/webm";
        }

        if (head.StartsWith("OggS"u8)) return extension is ".ogv" ? "video/ogg" : "audio/ogg";
        if (head.StartsWith("ID3"u8) || (head.Length >= 2 && head[0] == 0xFF && (head[1] & 0xE0) == 0xE0)) return "audio/mpeg";
        if (head.StartsWith("fLaC"u8)) return "audio/flac";
        if (head.StartsWith("PK\u0003\u0004"u8))
        {
            return extension switch
            {
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                _ => "application/zip",
            };
        }

        if (head.StartsWith(new byte[] { 0x1F, 0x8B })) return "application/gzip";
        if (head.StartsWith("7z"u8)) return "application/x-7z-compressed";

        if (LooksLikeText(head))
        {
            return extension switch
            {
                ".md" or ".markdown" => "text/markdown",
                ".svg" => "image/svg+xml",
                ".html" or ".htm" => "text/html",
                ".csv" => "text/csv",
                ".json" => "application/json",
                ".xml" => "application/xml",
                _ => "text/plain",
            };
        }

        return "application/octet-stream";
    }

    public static FileKind KindFor(string contentType, string extension)
    {
        if (contentType.StartsWith("image/", StringComparison.Ordinal)) return FileKind.Image;
        if (contentType.StartsWith("audio/", StringComparison.Ordinal)) return FileKind.Audio;
        if (contentType.StartsWith("video/", StringComparison.Ordinal)) return FileKind.Video;
        if (contentType is "application/zip" or "application/gzip" or "application/x-7z-compressed") return FileKind.Archive;
        if (CodeExtensions.Contains(extension)) return FileKind.Code;
        if (contentType is "application/pdf" || contentType.StartsWith("text/", StringComparison.Ordinal)
            || contentType.Contains("officedocument", StringComparison.Ordinal) || TextExtensions.Contains(extension))
        {
            return FileKind.Document;
        }

        return FileKind.Other;
    }

    /// <summary>Inline preview only for images (not SVG), PDF, audio, video, plain text and Markdown.</summary>
    public static bool InlineAllowed(string contentType) =>
        (contentType.StartsWith("image/", StringComparison.Ordinal) && contentType != "image/svg+xml")
        || contentType == "application/pdf"
        || contentType.StartsWith("audio/", StringComparison.Ordinal)
        || contentType.StartsWith("video/", StringComparison.Ordinal)
        || contentType is "text/plain" or "text/markdown";

    private static bool LooksLikeText(ReadOnlySpan<byte> head)
    {
        if (head.IsEmpty)
        {
            return true;
        }

        if (head.Contains((byte)0))
        {
            return false;
        }

        // Tolerate a truncated multi-byte sequence at the end of the sniff buffer.
        var probe = head.Length > 4 ? head[..^4] : head;
        return System.Text.Unicode.Utf8.IsValid(probe);
    }
}
