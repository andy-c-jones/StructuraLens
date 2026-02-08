namespace StructuraLens.Core.Models;

/// <summary>
/// Extension methods for working with type names and namespaces.
/// Provides consistent namespace extraction logic across the codebase.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Extracts the namespace from a fully-qualified type name.
    /// </summary>
    /// <param name="fullTypeName">The fully-qualified type name (e.g., "System.Collections.Generic.List")</param>
    /// <param name="defaultValue">The value to return if no namespace is found (default: empty string)</param>
    /// <returns>The namespace portion of the type name, or the default value if none exists</returns>
    public static string GetNamespace(this string fullTypeName, string defaultValue = "")
    {
        var lastDot = fullTypeName.LastIndexOf('.');
        return lastDot > 0 ? fullTypeName[..lastDot] : defaultValue;
    }
}
