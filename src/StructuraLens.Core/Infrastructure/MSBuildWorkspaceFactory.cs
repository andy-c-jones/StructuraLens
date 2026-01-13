using Microsoft.CodeAnalysis.MSBuild;
using StructuraLens.Core.Abstractions;

namespace StructuraLens.Core.Infrastructure;

/// <summary>
/// Factory for creating MSBuildWorkspace instances.
/// Ensures MSBuild is registered before creating workspaces.
/// </summary>
public sealed class MSBuildWorkspaceFactory : IMSBuildWorkspaceFactory
{
    private readonly IMSBuildRegistrationService _registrationService;

    public MSBuildWorkspaceFactory(IMSBuildRegistrationService registrationService)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
    }

    /// <inheritdoc />
    public MSBuildWorkspace Create()
    {
        _registrationService.EnsureMSBuildRegistered();
        return MSBuildWorkspace.Create();
    }
}
