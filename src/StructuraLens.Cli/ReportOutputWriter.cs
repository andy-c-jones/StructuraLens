namespace StructuraLens.Cli;

internal static class ReportOutputWriter
{
    public static async Task WriteOrPrintAsync(
        string content,
        string? output,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(output))
        {
            await File.WriteAllTextAsync(output, content, cancellationToken);
            return;
        }

        Console.WriteLine(content);
    }
}
