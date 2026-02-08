using FakeItEasy;
using StructuraLens.Core.Abstractions;
using StructuraLens.Core.Infrastructure;

namespace StructuraLens.Tests.Infrastructure;

public class MSBuildWorkspaceFactoryTests
{
    [Test]
    public void Create_EnsuresMSBuildRegistration_CallsRegistrationService()
    {
        // Arrange
        var registrationService = A.Fake<IMSBuildRegistrationService>();
        var factory = new MSBuildWorkspaceFactory(registrationService);

        // Act
        using var workspace = factory.Create();

        // Assert
        A.CallTo(() => registrationService.EnsureMSBuildRegistered())
            .MustHaveHappenedOnceExactly();
    }

    [Test]
    public async Task Create_WithRegisteredMSBuild_ReturnsWorkspace()
    {
        // Arrange
        var registrationService = A.Fake<IMSBuildRegistrationService>();
        var factory = new MSBuildWorkspaceFactory(registrationService);

        // Act
        using var workspace = factory.Create();

        // Assert
        await Assert.That(workspace).IsNotNull();
    }

    [Test]
    public async Task Create_ReturnsDisposableWorkspace()
    {
        // Arrange
        var registrationService = A.Fake<IMSBuildRegistrationService>();
        var factory = new MSBuildWorkspaceFactory(registrationService);

        // Act
        var workspace = factory.Create();

        // Assert - Should be disposable
        await Assert.That(workspace is IDisposable).IsTrue();
        
        // Cleanup
        workspace.Dispose();
    }

    [Test]
    public void Constructor_WithNullRegistrationService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MSBuildWorkspaceFactory(null!));
    }

    [Test]
    public async Task Create_MultipleWorkspaces_EachGetsOwnInstance()
    {
        // Arrange
        var registrationService = A.Fake<IMSBuildRegistrationService>();
        var factory = new MSBuildWorkspaceFactory(registrationService);

        // Act
        using var workspace1 = factory.Create();
        using var workspace2 = factory.Create();

        // Assert - Should be different instances
        await Assert.That(workspace1).IsNotSameReferenceAs(workspace2);
    }
}
