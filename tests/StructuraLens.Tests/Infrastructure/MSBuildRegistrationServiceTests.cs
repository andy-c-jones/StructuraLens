using StructuraLens.Core.Infrastructure;
using TUnit.Core;

namespace StructuraLens.Tests.Infrastructure;

public class MSBuildRegistrationServiceTests
{
    [Test]
    public async Task EnsureRegistered_FirstCall_RegistersMSBuild()
    {
        // Arrange
        var service = new MSBuildRegistrationService();

        // Act
        service.EnsureMSBuildRegistered();

        // Assert - No exception should be thrown
        // If MSBuild is not registered, subsequent MSBuild operations would fail
        await Task.CompletedTask;
    }

    [Test]
    public async Task EnsureRegistered_SubsequentCalls_DoesNotReregister()
    {
        // Arrange
        var service = new MSBuildRegistrationService();

        // Act - Call multiple times
        service.EnsureMSBuildRegistered();
        service.EnsureMSBuildRegistered();
        service.EnsureMSBuildRegistered();

        // Assert - No exception should be thrown
        // MSBuildLocator.RegisterInstance can only be called once, so if it were called
        // multiple times it would throw an exception
        await Task.CompletedTask;
    }

    [Test]
    public async Task EnsureRegistered_ThreadSafe_ConcurrentCallsSucceed()
    {
        // Arrange
        var service = new MSBuildRegistrationService();
        var tasks = new List<Task>();

        // Act - Call from multiple threads concurrently
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => service.EnsureMSBuildRegistered()));
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Assert - No exception should be thrown
        await Task.CompletedTask;
    }

    [Test]
    public async Task EnsureRegistered_CallFromDifferentInstances_WorksCorrectly()
    {
        // Arrange
        var service1 = new MSBuildRegistrationService();
        var service2 = new MSBuildRegistrationService();

        // Act - Call from different instances
        service1.EnsureMSBuildRegistered();
        service2.EnsureMSBuildRegistered();

        // Assert - No exception should be thrown
        // The static flag ensures registration only happens once across all instances
        await Task.CompletedTask;
    }
}
