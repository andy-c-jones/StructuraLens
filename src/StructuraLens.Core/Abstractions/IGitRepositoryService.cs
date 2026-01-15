namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Provides git repository information for analysis metadata.
/// </summary>
public interface IGitRepositoryService
{
    /// <summary>
    /// Checks if the given path is within a git repository.
    /// </summary>
    /// <param name="path">The file or directory path to check.</param>
    /// <returns>True if the path is within a git repository; otherwise false.</returns>
    bool IsGitRepository(string path);
    
    /// <summary>
    /// Gets git metadata for the repository containing the given path.
    /// </summary>
    /// <param name="path">The file or directory path within the repository.</param>
    /// <returns>Git metadata if the path is in a repository; otherwise null.</returns>
    GitMetadata? GetGitMetadata(string path);
}

/// <summary>
/// Represents git repository metadata.
/// </summary>
public record GitMetadata(
    string CommitSha,
    string ShortCommitSha,
    string BranchName,
    string? RemoteUrl,
    bool IsDirty);
