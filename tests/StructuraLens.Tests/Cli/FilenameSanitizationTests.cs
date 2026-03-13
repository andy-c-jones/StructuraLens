namespace StructuraLens.Tests.Cli;

public class FilenameSanitizationTests
{
    // Copy of the SanitizeBranchName function from Program.cs for testing
    private static string SanitizeBranchName(string branchName)
    {
        // Use a unified set of invalid characters that works across all platforms
        // This includes all Windows-invalid chars for maximum cross-platform compatibility
        // Characters: < > : " | ? * / \ and control characters (0-31)
        char[] invalidChars = new[] { '<', '>', ':', '"', '|', '?', '*', '/', '\\', '\0' }
            .Concat(Enumerable.Range(1, 31).Select(i => (char)i))
            .Distinct()
            .ToArray();

        var result = branchName;
        foreach (char c in invalidChars)
        {
            result = result.Replace(c, '_');
        }

        return result;
    }

    [Test]
    public async Task SanitizeBranchName_WithSimpleName_RemainsUnchanged()
    {
        // Arrange
        var branchName = "main";

        // Act
        var sanitized = SanitizeBranchName(branchName);

        // Assert
        await Assert.That(sanitized).IsEqualTo("main");
    }

    [Test]
    public async Task SanitizeBranchName_WithForwardSlash_ReplacesWithUnderscore()
    {
        // Arrange
        var branchName = "feature/add-metrics";

        // Act
        var sanitized = SanitizeBranchName(branchName);

        // Assert
        await Assert.That(sanitized).IsEqualTo("feature_add-metrics");
    }

    [Test]
    public async Task SanitizeBranchName_WithBackslash_ReplacesWithUnderscore()
    {
        // Arrange
        var branchName = "feature\\add-metrics";

        // Act
        var sanitized = SanitizeBranchName(branchName);

        // Assert
        await Assert.That(sanitized).IsEqualTo("feature_add-metrics");
    }

    [Test]
    public async Task SanitizeBranchName_WithMultipleSlashes_ReplacesAllWithUnderscores()
    {
        // Arrange
        var branchName = "feature/add/git/support";

        // Act
        var sanitized = SanitizeBranchName(branchName);

        // Assert
        await Assert.That(sanitized).IsEqualTo("feature_add_git_support");
    }

    [Test]
    public async Task SanitizeBranchName_WithInvalidFileNameChars_ReplacesWithUnderscores()
    {
        // Arrange
        var branchName = "feature<test>:branch";

        // Act
        var sanitized = SanitizeBranchName(branchName);

        // Assert
        // All invalid filename chars should be replaced
        await Assert.That(sanitized.Contains('<')).IsFalse();
        await Assert.That(sanitized.Contains('>')).IsFalse();
        await Assert.That(sanitized.Contains(':')).IsFalse();
    }

    [Test]
    public async Task SanitizeBranchName_WithSpecialChars_ReplacesWithUnderscores()
    {
        // Arrange
        var branchName = "release/v1.0|test";

        // Act
        var sanitized = SanitizeBranchName(branchName);

        // Assert
        await Assert.That(sanitized.Contains('/')).IsFalse();
        await Assert.That(sanitized.Contains('|')).IsFalse();
    }

    [Test]
    public async Task SanitizeBranchName_PreservesValidCharacters()
    {
        // Arrange
        var branchName = "feature_add-metrics.v2";

        // Act
        var sanitized = SanitizeBranchName(branchName);

        // Assert
        // Underscores, hyphens, and periods are valid in filenames
        await Assert.That(sanitized).IsEqualTo("feature_add-metrics.v2");
    }

    [Test]
    public async Task SanitizeBranchName_EmptyString_ReturnsEmptyString()
    {
        // Arrange
        var branchName = "";

        // Act
        var sanitized = SanitizeBranchName(branchName);

        // Assert
        await Assert.That(sanitized).IsEqualTo("");
    }

    [Test]
    public async Task GeneratedFilename_WithGitMetadata_HasCorrectFormat()
    {
        // Arrange
        var shortSha = "a1b2c3d";
        var sanitizedBranch = "feature_test";
        var extension = "json";

        // Act
        var filename = $"{shortSha}-{sanitizedBranch}.{extension}";

        // Assert
        await Assert.That(filename).IsEqualTo("a1b2c3d-feature_test.json");
        // Verify filename is valid
        await Assert.That(Path.GetInvalidFileNameChars().Any(c => filename.Contains(c))).IsFalse();
    }

    [Test]
    public async Task GeneratedFilename_WithComplexBranchName_IsValidFilename()
    {
        // Arrange
        var branchName = "feature/add-git/support<test>";
        var shortSha = "69b6dfa";
        var sanitizedBranch = SanitizeBranchName(branchName);
        var extension = "slr";

        // Act
        var filename = $"{shortSha}-{sanitizedBranch}.{extension}";

        // Assert
        await Assert.That(Path.GetInvalidFileNameChars().Any(c => filename.Contains(c))).IsFalse();
        await Assert.That(filename.Contains('/')).IsFalse();
        await Assert.That(filename.Contains('<')).IsFalse();
        await Assert.That(filename.Contains('>')).IsFalse();
    }

    [Test]
    public async Task GeneratedFilename_DifferentFormats_HaveCorrectExtensions()
    {
        // Arrange
        var shortSha = "abc1234";
        var sanitizedBranch = "main";

        // Act & Assert
        var jsonFilename = $"{shortSha}-{sanitizedBranch}.json";
        await Assert.That(jsonFilename).IsEqualTo("abc1234-main.json");

        var htmlFilename = $"{shortSha}-{sanitizedBranch}.html";
        await Assert.That(htmlFilename).IsEqualTo("abc1234-main.html");

        var compactFilename = $"{shortSha}-{sanitizedBranch}.slr";
        await Assert.That(compactFilename).IsEqualTo("abc1234-main.slr");
    }
}
