using System.Text.RegularExpressions;
using StructuraLens.Core.Configuration;
using StructuraLens.Core.Models;

namespace StructuraLens.Core.Analysis;

/// <summary>
/// Evaluates architecture rules against coupling analysis results.
/// </summary>
public static class ArchitectureLinter
{
    /// <summary>
    /// Evaluates architecture rules against the provided coupling analysis.
    /// </summary>
    public static LintingResults Evaluate(CouplingAnalysis coupling, IReadOnlyList<ArchitectureRule> rules)
    {
        var violations = new List<LintViolation>();
        var enabledRules = rules.Where(r => r.Enabled).ToList();

        foreach (var rule in enabledRules)
        {
            var ruleViolations = EvaluateRule(rule, coupling.AllDependencies);
            violations.AddRange(ruleViolations);
        }

        return new LintingResults(DateTime.UtcNow)
        {
            Violations = violations,
            RulesEvaluated = enabledRules.Count
        };
    }

    private static List<LintViolation> EvaluateRule(ArchitectureRule rule, IReadOnlyList<DependencyEdge> dependencies)
    {
        var violations = new List<LintViolation>();

        // Filter dependencies by type if specified
        var relevantDependencies = rule.DependencyType switch
        {
            RuleDependencyType.Project => dependencies.Where(d => d.Type == DependencyType.ProjectReference),
            RuleDependencyType.Namespace => dependencies.Where(d => d.Type == DependencyType.NamespaceReference),
            RuleDependencyType.Type => dependencies.Where(d => d.Type == DependencyType.TypeReference),
            _ => dependencies
        };

        // Filter by "from" pattern
        var matchingDependencies = relevantDependencies
            .Where(d => MatchesPattern(d.FromEntity, rule.From))
            .ToList();

        foreach (var dep in matchingDependencies)
        {
            // Check disallow rules first (explicit denials)
            if (rule.Disallow.Count > 0)
            {
                foreach (var disallowPattern in rule.Disallow)
                {
                    if (MatchesPattern(dep.ToEntity, disallowPattern))
                    {
                        violations.Add(new LintViolation(
                            RuleId: rule.Id,
                            Message: rule.Description ?? $"Disallowed dependency: {dep.FromEntity} → {dep.ToEntity}",
                            Severity: rule.Severity)
                        {
                            FromEntity = dep.FromEntity,
                            ToEntity = dep.ToEntity,
                            SourceLocation = dep.SourceLocation
                        });
                        break; // Only report once per dependency
                    }
                }
            }

            // Check allow rules (if specified, anything not allowed is a violation)
            if (rule.Allow.Count > 0)
            {
                var isAllowed = rule.Allow.Any(allowPattern => MatchesPattern(dep.ToEntity, allowPattern));
                if (!isAllowed)
                {
                    violations.Add(new LintViolation(
                        RuleId: rule.Id,
                        Message: rule.Description ?? $"Dependency not in allow list: {dep.FromEntity} → {dep.ToEntity}",
                        Severity: rule.Severity)
                    {
                        FromEntity = dep.FromEntity,
                        ToEntity = dep.ToEntity,
                        SourceLocation = dep.SourceLocation
                    });
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Matches a value against a wildcard pattern.
    /// </summary>
    private static bool MatchesPattern(string value, string pattern)
    {
        if (pattern == "*") return true;

        // Convert wildcard to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }
}
