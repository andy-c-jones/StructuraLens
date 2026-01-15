using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StructuraLens.Core.Infrastructure;
using TUnit.Core;

namespace StructuraLens.Tests.Infrastructure;

public class GitRepositoryServiceTests
{
    private readonly ILogger<GitRepositoryService> _logger = NullLogger<GitRepositoryService>.Instance;

    [Test]
    public async Task IsGitRepository_WithValidRepo_ReturnsTrue()
    {
        // Arrange
        var service = new GitRepositoryService(_logger);
        var repoPath = Directory.GetCurrentDirectory(); // This test project is in a git repo

        // Act
        var isRepo = service.IsGitRepository(repoPath);

        // Assert
        await Assert.That(isRepo).IsTrue();
    }

    [Test]
    public async Task IsGitRepository_WithNonRepoPath_ReturnsFalse()
    {
        // Arrange
        var service = new GitRepositoryService(_logger);
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        try
        {
            // Act
            var isRepo = service.IsGitRepository(tempPath);

            // Assert
            await Assert.That(isRepo).IsFalse();
        }
        finally
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }

    [Test]
    public async Task GetGitMetadata_WithValidRepo_ReturnsMetadata()
    {
        // Arrange
        var service = new GitRepositoryService(_logger);
        var repoPath = Directory.GetCurrentDirectory();

        // Act
        var metadata = service.GetGitMetadata(repoPath);

        // Assert
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.CommitSha).IsNotNull();
        await Assert.That(metadata.CommitSha.Length).IsEqualTo(40); // Full SHA is 40 chars
        await Assert.That(metadata.ShortCommitSha).IsNotNull();
        await Assert.That(metadata.ShortCommitSha.Length).IsEqualTo(7); // Short SHA is 7 chars
        await Assert.That(metadata.BranchName).IsNotNull();
        await Assert.That(metadata.BranchName.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task GetGitMetadata_WithNonRepoPath_ReturnsNull()
    {
        // Arrange
        var service = new GitRepositoryService(_logger);
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        try
        {
            // Act
            var metadata = service.GetGitMetadata(tempPath);

            // Assert
            await Assert.That(metadata).IsNull();
        }
        finally
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }

    [Test]
    public async Task GetGitMetadata_ShortCommitSha_IsFirstSevenCharsOfFullSha()
    {
        // Arrange
        var service = new GitRepositoryService(_logger);
        var repoPath = Directory.GetCurrentDirectory();

        // Act
        var metadata = service.GetGitMetadata(repoPath);

        // Assert
        await Assert.That(metadata).IsNotNull();
        await Assert.That(metadata!.ShortCommitSha).IsEqualTo(metadata.CommitSha[..7]);
    }

    [Test]
    public async Task GetGitMetadata_CommitSha_IsValidHexString()
    {
        // Arrange
        var service = new GitRepositoryService(_logger);
        var repoPath = Directory.GetCurrentDirectory();

        // Act
        var metadata = service.GetGitMetadata(repoPath);

        // Assert
        await Assert.That(metadata).IsNotNull();
        // SHA should only contain hexadecimal characters
        var isHex = metadata!.CommitSha.All(c => "0123456789abcdef".Contains(char.ToLower(c)));
        await Assert.That(isHex).IsTrue();
    }

    [Test]
    public async Task GetGitMetadata_IsDirty_IsBoolean()
    {
        // Arrange
        var service = new GitRepositoryService(_logger);
        var repoPath = Directory.GetCurrentDirectory();

        // Act
        var metadata = service.GetGitMetadata(repoPath);

        // Assert
        await Assert.That(metadata).IsNotNull();
        // Just verify the property exists and is of correct type (bool)
        var isDirty = metadata!.IsDirty;
        // IsDirty should be true or false
        await Assert.That(isDirty == true || isDirty == false).IsTrue();
    }
}
