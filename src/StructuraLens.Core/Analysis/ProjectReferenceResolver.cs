using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace StructuraLens.Core.Analysis;

internal static class ProjectReferenceResolver
{
    public static List<string> GetProjectReferenceNames(Project project)
    {
        var projectFilePath = project.FilePath;
        if (string.IsNullOrWhiteSpace(projectFilePath) || !File.Exists(projectFilePath))
        {
            return [];
        }

        var solutionProjectsByPath = project.Solution.Projects
            .Where(p => !string.IsNullOrWhiteSpace(p.FilePath))
            .GroupBy(
                p => Path.GetFullPath(p.FilePath!),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Name,
                StringComparer.OrdinalIgnoreCase);

        var projectDirectory = Path.GetDirectoryName(projectFilePath)!;

        return GetDirectReferenceIncludes(projectFilePath, "ProjectReference")
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include.Replace('\\', '/'))))
            .Select(path => solutionProjectsByPath.TryGetValue(path, out var name)
                ? name
                : Path.GetFileNameWithoutExtension(path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> GetDirectReferenceIncludes(string projectFilePath, string itemName)
    {
        var document = XDocument.Load(projectFilePath);

        return document.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, itemName, StringComparison.Ordinal))
            .Select(e => e.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
