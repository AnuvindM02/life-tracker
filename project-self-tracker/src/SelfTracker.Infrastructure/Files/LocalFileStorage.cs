using System.Security.Cryptography;
using SelfTracker.Application.Abstractions;

namespace SelfTracker.Infrastructure.Files;

/// <summary>
/// Files under FileRoot as {yyyy}/{MM}/{guid}{ext}. Temp files live in FileRoot/.tmp (same volume) so the final
/// move is atomic. All relative paths are validated to stay inside the root (no traversal).
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private const string TempFolder = ".tmp";
    private const int HeadSize = 4096;

    public LocalFileStorage(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Path.Combine(Root, TempFolder));
    }

    public string Root { get; }

    public async Task<StoredFile> WriteTempAsync(Stream source, long maxBytes, CancellationToken ct)
    {
        var tempPath = Path.Combine(Root, TempFolder, $"{Guid.NewGuid():N}.part");
        var head = new byte[HeadSize];
        var headLength = 0;
        long total = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try
        {
            await using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    total += read;
                    if (total > maxBytes)
                    {
                        throw new FileTooLargeException(maxBytes);
                    }

                    if (headLength < HeadSize)
                    {
                        var take = Math.Min(read, HeadSize - headLength);
                        Buffer.BlockCopy(buffer, 0, head, headLength, take);
                        headLength += take;
                    }

                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), ct);
                }

                await target.FlushAsync(ct);
            }

            return new StoredFile(tempPath, total, Convert.ToHexStringLower(hash.GetHashAndReset()), head[..headLength]);
        }
        catch
        {
            DeleteTemp(tempPath);
            throw;
        }
    }

    public void Commit(string tempPath, string relativePath)
    {
        var target = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Move(tempPath, target, overwrite: false);
    }

    public void DeleteTemp(string tempPath)
    {
        var full = Path.GetFullPath(tempPath);
        if (full.StartsWith(Path.Combine(Root, TempFolder), StringComparison.OrdinalIgnoreCase) && File.Exists(full))
        {
            File.Delete(full);
        }
    }

    public Stream OpenRead(string relativePath) =>
        new FileStream(Resolve(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

    public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));

    public void Delete(string relativePath)
    {
        var path = Resolve(relativePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public long? FreeBytes()
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(Root)!).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public bool IsWritable()
    {
        try
        {
            var probe = Path.Combine(Root, TempFolder, $"probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public IEnumerable<string> EnumerateFiles()
    {
        var tempRoot = Path.Combine(Root, TempFolder);
        return Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'));
    }

    public async Task<string> ComputeSha256Async(string relativePath, CancellationToken ct)
    {
        await using var stream = OpenRead(relativePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hash);
    }

    private string Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new BadRequestException("Invalid storage path.");
        }

        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Invalid storage path.");
        }

        return full;
    }
}
