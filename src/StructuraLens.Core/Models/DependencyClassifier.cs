namespace StructuraLens.Core.Models;

/// <summary>
/// Classifies dependency names into built-in framework dependencies and third-party packages.
/// </summary>
public static class DependencyClassifier
{
    public static bool IsBclDependency(string dependencyName)
    {
        ArgumentNullException.ThrowIfNull(dependencyName);

        return dependencyName.Equals("System", StringComparison.Ordinal) ||
               dependencyName.StartsWith("System.", StringComparison.Ordinal) ||
               dependencyName.StartsWith("Microsoft.", StringComparison.Ordinal);
    }
}
