using System.Text.Json;
using System.Text.Json.Serialization;

namespace StructuraLens.Cli;

internal static class CliJsonOptions
{
    public static readonly JsonSerializerOptions DefaultOutput = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions CompactOutput = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions Input = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
