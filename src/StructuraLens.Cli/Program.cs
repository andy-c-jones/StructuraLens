using StructuraLens.Core;

Console.WriteLine($"StructuraLens v{Analyzer.GetVersion()}");
Console.WriteLine("Usage: structuralens analyze <path> [--config <file>] [--out <file>] [--format json|html]");
Console.WriteLine();
Console.WriteLine("Run this tool after building your solution for full analysis.");
Console.WriteLine("If DLLs are not present, source-only analysis will be performed with reduced metrics.");

