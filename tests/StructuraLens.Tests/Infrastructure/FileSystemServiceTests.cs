using StructuraLens.Core.Infrastructure;
using TUnit.Core;

namespace StructuraLens.Tests.Infrastructure;

public class FileSystemServiceTests
{
    private readonly string _testDirectory = Path.Combine(Path.GetTempPath(), "StructuraLensTests", Guid.NewGuid().ToString());

    [Before(Test)]
    public void Setup()
    {
        Directory.CreateDirectory(_testDirectory);
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Test]
    public async Task FileExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var service = new FileSystemService();
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        var exists = service.FileExists(testFile);

        // Assert
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task FileExists_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var service = new FileSystemService();
        var testFile = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var exists = service.FileExists(testFile);

        // Assert
        await Assert.That(exists).IsFalse();
    }

    [Test]
    public async Task GetFullPath_WithRelativePath_ReturnsAbsolutePath()
    {
        // Arrange
        var service = new FileSystemService();
        var relativePath = "test/path.txt";

        // Act
        var fullPath = service.GetFullPath(relativePath);

        // Assert
        await Assert.That(Path.IsPathRooted(fullPath)).IsTrue();
        await Assert.That(fullPath.EndsWith(relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()))).IsTrue();
    }

    [Test]
    public async Task GetFullPath_WithAbsolutePath_ReturnsSamePath()
    {
        // Arrange
        var service = new FileSystemService();
        var absolutePath = Path.Combine(_testDirectory, "test.txt");

        // Act
        var fullPath = service.GetFullPath(absolutePath);

        // Assert
        await Assert.That(fullPath).IsEqualTo(absolutePath);
    }

    [Test]
    public async Task WriteAllTextAsync_WithContent_WritesSuccessfully()
    {
        // Arrange
        var service = new FileSystemService();
        var testFile = Path.Combine(_testDirectory, "output.txt");
        var content = "Test content to write";

        // Act
        await service.WriteAllTextAsync(testFile, content);

        // Assert
        await Assert.That(File.Exists(testFile)).IsTrue();
        var writtenContent = await File.ReadAllTextAsync(testFile);
        await Assert.That(writtenContent).IsEqualTo(content);
    }

    [Test]
    public async Task WriteAllTextAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var service = new FileSystemService();
        var testFile = Path.Combine(_testDirectory, "cancel.txt");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await service.WriteAllTextAsync(testFile, "content", cts.Token);
        });
    }

    [Test]
    public async Task WriteAllTextAsync_OverwritesExistingFile()
    {
        // Arrange
        var service = new FileSystemService();
        var testFile = Path.Combine(_testDirectory, "overwrite.txt");
        await File.WriteAllTextAsync(testFile, "Original content");

        // Act
        await service.WriteAllTextAsync(testFile, "New content");

        // Assert
        var content = await File.ReadAllTextAsync(testFile);
        await Assert.That(content).IsEqualTo("New content");
    }
}
