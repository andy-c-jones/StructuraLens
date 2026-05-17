using FakeItEasy;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Infrastructure;

namespace StructuraLens.Tests.Infrastructure;

public class NuGetRestorerTests
{
    [Test]
    public async Task RestoreAsync_WithInvalidPath_DoesNotThrowException()
    {
        // Arrange
        var logger = A.Fake<ILogger<NuGetRestorer>>();
        // Configure the fake to enable Error level logging
        A.CallTo(() => logger.IsEnabled(LogLevel.Error)).Returns(true);

        var restorer = new NuGetRestorer(logger);
        var invalidPath = "nonexistent/solution.sln";

        // Act & Assert - Should not throw, but will log errors
        await restorer.RestorePackagesAsync(invalidPath);

        // Verify error was logged - source-generated logging calls Log with LogLevel.Error
        A.CallTo(logger)
            .Where(call => call.Method.Name == "Log" &&
                          call.Arguments.Get<LogLevel>(0) == LogLevel.Error)
            .MustHaveHappened();
    }

    [Test]
    public async Task RestoreAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var logger = A.Fake<ILogger<NuGetRestorer>>();
        var restorer = new NuGetRestorer(logger);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await restorer.RestorePackagesAsync("some/path.sln", cts.Token);
        });
    }

    [Test]
    public async Task RestoreAsync_LogsDebugMessages()
    {
        // Arrange
        var logger = A.Fake<ILogger<NuGetRestorer>>();
        // Configure the fake to enable Debug level logging
        A.CallTo(() => logger.IsEnabled(LogLevel.Debug)).Returns(true);

        var restorer = new NuGetRestorer(logger);
        var somePath = "test/path.sln";

        // Act
        await restorer.RestorePackagesAsync(somePath);

        // Assert - Verify debug logging occurred - source-generated logging calls Log with LogLevel.Debug
        A.CallTo(logger)
            .Where(call => call.Method.Name == "Log" &&
                          call.Arguments.Get<LogLevel>(0) == LogLevel.Debug)
            .MustHaveHappened();
    }

    [Test]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new NuGetRestorer(null!);
        });
    }
}
