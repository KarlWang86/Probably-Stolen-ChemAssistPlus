using System.Security.Cryptography;
using Mono.Cecil;

// Only inspect metadata/IL: never load or invoke game methods.
if (args.Length < 3) { Console.Error.WriteLine("assembly type method [--il]; type/method accept *"); return 2; }
try
{
    string path = Path.GetFullPath(args[0]);
    bool Match(string value, string pattern) => pattern == "*" || value.Equals(pattern, StringComparison.Ordinal);
    using var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(Path.GetDirectoryName(path)!);
    using var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver });
    Console.WriteLine($"Assembly: {assembly.Name}");
    Console.WriteLine($"SHA256: {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");
    IEnumerable<TypeDefinition> Walk(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types) { yield return type; foreach (var nested in Walk(type.NestedTypes)) yield return nested; }
    }
    int count = 0;
    foreach (var type in Walk(assembly.MainModule.Types).Where(t => Match(t.FullName, args[1]) || Match(t.Name, args[1])))
    foreach (var method in type.Methods.Where(m => Match(m.Name, args[2])))
    {
        count++;
        Console.WriteLine(method.FullName);
        foreach (var parameter in method.Parameters)
            Console.WriteLine($"  {parameter.Index}: {parameter.ParameterType.FullName} {parameter.Name} {parameter.Attributes}");
        if (args.Contains("--il") && method.HasBody)
        {
            foreach (var variable in method.Body.Variables) Console.WriteLine($"  local {variable.Index}: {variable.VariableType}");
            foreach (var instruction in method.Body.Instructions) Console.WriteLine(instruction);
            foreach (var handler in method.Body.ExceptionHandlers)
                Console.WriteLine($"  EH {handler.HandlerType}: try {handler.TryStart} -> {handler.TryEnd}; handler {handler.HandlerStart} -> {handler.HandlerEnd}; filter {handler.FilterStart}; catch {handler.CatchType}");
        }
    }
    if (count == 0) { Console.Error.WriteLine("No matching method; use the exact type/method name or *."); return 1; }
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
