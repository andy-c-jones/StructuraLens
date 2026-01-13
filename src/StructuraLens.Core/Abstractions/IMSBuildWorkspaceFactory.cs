using Microsoft.CodeAnalysis.MSBuild;

namespace StructuraLens.Core.Abstractions;

/// <summary>
/// Factory for creating MSBuild workspaces.
/// </summary>
public interface IMSBuildWorkspaceFactory
{
    /// <summary>
    /// Creates a new MSBuildWorkspace instance.
    /// Ensures MSBuild is registered before creating the workspace.
    /// </summary>
    MSBuildWorkspace Create();
}
