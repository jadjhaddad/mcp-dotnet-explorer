using System.Reflection;
using System.Runtime.InteropServices;

namespace DllInspectorMcp;

public static class DllLoader
{
    public static Assembly LoadAssembly(string path)
    {
        // Convert Windows paths to WSL paths if needed
        path = ConvertToAbsolutePath(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Assembly not found: {path}");
        }

        var directory = Path.GetDirectoryName(path)!;

        // Build comprehensive assembly path list for MetadataLoadContext
        var assemblyPaths = new List<string>();

        // 1. Add ALL DLLs from the target directory (e.g., Civil3D C3D folder)
        if (Directory.Exists(directory))
        {
            assemblyPaths.AddRange(Directory.GetFiles(directory, "*.dll"));
        }

        // 2. Add AutoCAD managed DLLs from parent directory
        var parentDir = Path.GetDirectoryName(directory);
        if (parentDir != null && Directory.Exists(parentDir))
        {
            // Add all managed DLLs (acdbmgd.dll, acmgd.dll, accoremgd.dll, etc.)
            var parentDlls = Directory.GetFiles(parentDir, "*.dll")
                .Where(f => {
                    var name = Path.GetFileName(f).ToLowerInvariant();
                    return name.Contains("mgd") || name.StartsWith("ac");
                });
            assemblyPaths.AddRange(parentDlls);
        }

        // 3. Add .NET runtime assemblies (required for System.* types)
        var runtimePath = RuntimeEnvironment.GetRuntimeDirectory();
        assemblyPaths.AddRange(Directory.GetFiles(runtimePath, "*.dll"));

        // 4. Remove duplicates
        assemblyPaths = assemblyPaths.Distinct().ToList();

        Console.WriteLine($"Loading assembly: {Path.GetFileName(path)}");
        Console.WriteLine($"Dependency resolver has access to {assemblyPaths.Count} DLLs");

        // Create resolver with all possible assemblies
        var resolver = new PathAssemblyResolver(assemblyPaths);
        var mlc = new MetadataLoadContext(resolver);

        // Load the target assembly
        return mlc.LoadFromAssemblyPath(path);
    }

    public static string ConvertToAbsolutePath(string path)
    {
        // If running on Linux/WSL and path looks like a Windows path
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            path.Length >= 2 &&
            char.IsLetter(path[0]) &&
            path[1] == ':')
        {
            // Convert C:\path to /mnt/c/path
            var driveLetter = char.ToLowerInvariant(path[0]);
            var pathWithoutDrive = path.Substring(2).Replace('\\', '/');
            return $"/mnt/{driveLetter}{pathWithoutDrive}";
        }

        return path;
    }
}
