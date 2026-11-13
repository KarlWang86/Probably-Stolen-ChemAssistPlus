using System.Reflection;
using System.Runtime.Loader;

// Run the game's scanner on modern .NET; Windows PowerShell 5 uses .NET Framework.
string managed = Path.GetFullPath(args[0]);
string folder = Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string path = Path.Combine(managed, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
try
{
    var game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed, "Assembly-CSharp.dll"));
    var scanner = game.GetType("ModScanner", true);
    string[] references = Directory.GetFiles(managed, "*.dll").Select(Path.GetFileNameWithoutExtension).ToArray();
    var result = scanner.GetMethod("Scan").Invoke(null, new object[]
    {
        new[] { Path.Combine(folder, "ChemAssistPlus.dll") }, references, folder, true
    });
    object Read(string name) => result.GetType().GetField(name)?.GetValue(result)
        ?? result.GetType().GetProperty(name)?.GetValue(result);
    string findings = (string)result.GetType().GetMethod("FormatFindings").Invoke(result, new object[] { 100 });
    var capabilities = (IEnumerable<string>)Read("Capabilities");
    if ((bool)Read("Refused") || !string.IsNullOrEmpty((string)Read("Error")) || capabilities.Any())
        throw new InvalidOperationException("Native mod scan failed: " + findings + " " + Read("Error"));
    Console.WriteLine("Native ModScanner: PASS");
    Console.WriteLine("Methods scanned: " + Read("MethodsScanned"));
    Console.WriteLine("Additional capabilities: none");
}
catch (Exception error)
{
    Console.Error.WriteLine(error.ToString());
    return 1;
}
return 0;
