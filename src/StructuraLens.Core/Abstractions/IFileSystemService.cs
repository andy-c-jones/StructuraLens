namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Abstracts file system operations for testability.
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Returns the absolute path for the specified path string.
    /// </summary>
    string GetFullPath(string path);

    /// <summary>
    /// Asynchronously creates a new file, writes the specified string to the file, and then closes the file.
    /// </summary>
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
}
