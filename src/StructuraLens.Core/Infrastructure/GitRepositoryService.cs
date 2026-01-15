using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Abstractions;

namespace StructuraLens.Core.Infrastructure;

/// <summary>
/// Default implementation of IGitRepositoryService using LibGit2Sharp.
/// </summary>
public sealed class GitRepositoryService : IGitRepositoryService
{
    private readonly ILogger<GitRepositoryService> _logger;

    public GitRepositoryService(ILogger<GitRepositoryService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsGitRepository(string path)
    {
        try
        {
            var repoPath = Repository.Discover(path);
            return !string.IsNullOrEmpty(repoPath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to discover git repository for path: {Path}", path);
            return false;
        }
    }

    /// <inheritdoc />
    public GitMetadata? GetGitMetadata(string path)
    {
        try
        {
            var repoPath = Repository.Discover(path);
            if (string.IsNullOrEmpty(repoPath))
            {
                return null;
            }

            using var repo = new Repository(repoPath);
            
            var head = repo.Head;
            if (head?.Tip == null)
            {
                _logger.LogWarning("Git repository found but HEAD is null or has no commits");
                return null;
            }

            var commitSha = head.Tip.Sha;
            var shortCommitSha = commitSha.Substring(0, Math.Min(7, commitSha.Length));
            
            // Get branch name - handle detached HEAD state
            var branchName = head.FriendlyName;
            if (string.IsNullOrEmpty(branchName) || branchName == "(no branch)")
            {
                branchName = $"detached-{shortCommitSha}";
            }

            // Get remote URL (origin)
            string? remoteUrl = null;
            var remote = repo.Network.Remotes["origin"];
            if (remote != null)
            {
                remoteUrl = remote.Url;
            }

            // Check if working tree is dirty (has uncommitted changes)
            var status = repo.RetrieveStatus(new StatusOptions());
            var isDirty = status.IsDirty;

            _logger.LogDebug(
                "Git metadata retrieved: Commit={Commit}, Branch={Branch}, Dirty={Dirty}", 
                shortCommitSha, branchName, isDirty);

            return new GitMetadata(
                CommitSha: commitSha,
                ShortCommitSha: shortCommitSha,
                BranchName: branchName,
                RemoteUrl: remoteUrl,
                IsDirty: isDirty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve git metadata for path: {Path}", path);
            return null;
        }
    }
}
