using StructuraLens.Core.Abstractions;

namespace StructuraLens.Core.Infrastructure;

/// <summary>
/// Default implementation of IFileSystemService using System.IO.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    /// <inheritdoc />
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc />
    public string GetFullPath(string path) => Path.GetFullPath(path);

    /// <inheritdoc />
    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        => File.WriteAllTextAsync(path, content, cancellationToken);
}
