using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using StructuraLens.Core.Analysis.Logging;

namespace StructuraLens.Core.Analysis;

internal static class CompilationCacheBuilder
{
    public static async Task<ConcurrentDictionary<string, Compilation>> BuildAsync(
        IReadOnlyList<Project> projects,
        ConcurrentBag<string> warnings,
        ILogger<SolutionAnalyzer> logger,
        CancellationToken cancellationToken)
    {
        SolutionAnalyzerLog.PreFetchingCompilations(logger);

        var compilationCache = new ConcurrentDictionary<string, Compilation>();

        await Parallel.ForEachAsync(projects, new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount),
            CancellationToken = cancellationToken
        }, async (project, ct) =>
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation != null)
            {
                compilationCache[project.Name] = compilation;
            }
            else
            {
                warnings.Add($"Could not get compilation for project: {project.Name}");
                SolutionAnalyzerLog.CouldNotGetCompilation(logger, project.Name);
            }
        });

        SolutionAnalyzerLog.CachedCompilations(logger, compilationCache.Count);
        return compilationCache;
    }
}
