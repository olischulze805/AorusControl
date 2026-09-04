using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Mechanical, syntax-aware recovery. Never replaces an existing function.
if (args.Length != 2) throw new ArgumentException("Usage: RecoverDiagnostics current.cs decompiled.cs");
string currentPath = Path.GetFullPath(args[0]);
string current = File.ReadAllText(currentPath);
var root = CSharpSyntaxTree.ParseText(current).GetCompilationUnitRoot();
var recovered = CSharpSyntaxTree.ParseText(File.ReadAllText(args[1])).GetCompilationUnitRoot();
var main = recovered.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(x => x.Identifier.Text == "Main");
var names = root.Members.OfType<GlobalStatementSyntax>()
    .Select(x => x.Statement).OfType<LocalFunctionStatementSyntax>()
    .Select(x => x.Identifier.Text).ToHashSet(StringComparer.Ordinal);
var missing = main.Body!.Statements.OfType<LocalFunctionStatementSyntax>()
    .Where(x => !names.Contains(x.Identifier.Text)).ToArray();
if (missing.Length == 0) { Console.WriteLine("No missing functions; unchanged."); return; }
int insertion = root.Members.First(x => x is not GlobalStatementSyntax).FullSpan.Start;
string addition = "\n// Recovered from the preserved 2026-09-03 15:44 diagnostics assembly.\n" +
    string.Join("\n\n", missing.Select(x => x.ToFullString())) + "\n";
File.WriteAllText(currentPath, current.Insert(insertion, addition));
Console.WriteLine($"Recovered {missing.Length} missing functions; existing functions untouched:");
foreach (var method in missing) Console.WriteLine(method.Identifier.Text);
