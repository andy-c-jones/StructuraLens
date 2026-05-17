using System.Reflection;

namespace StructuraLens.Cli;

internal static class VersionProvider
{
    public static string GetVersion()
    {
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return version ?? "unknown";
    }
}
