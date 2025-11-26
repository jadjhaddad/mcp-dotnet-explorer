using System.Reflection;
using System.Runtime.InteropServices;

namespace DllInspectorMcp;

// This is the FIXED version of MetadataLoadContext approach
public static class DllLoaderMetadataFixed
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

        // Build comprehensive assembly path list
        var assemblyPaths = new List<string>();

        // 1. Add ALL DLLs from the target directory (Civil3D C3D folder)
        if (Directory.Exists(directory))
        {
            assemblyPaths.AddRange(Directory.GetFiles(directory, "*.dll"));
        }

        // 2. Add AutoCAD managed DLLs from parent directory
        var parentDir = Path.GetDirectoryName(directory);
        if (parentDir != null && Directory.Exists(parentDir))
        {
            assemblyPaths.AddRange(Directory.GetFiles(parentDir, "*mgd.dll"));
            assemblyPaths.AddRange(Directory.GetFiles(parentDir, "accore*.dll"));
        }

        // 3. Add .NET runtime assemblies (required for System.* types)
        var runtimePath = RuntimeEnvironment.GetRuntimeDirectory();
        assemblyPaths.AddRange(Directory.GetFiles(runtimePath, "*.dll"));

        // 4. Remove duplicates
        assemblyPaths = assemblyPaths.Distinct().ToList();

        Console.WriteLine($"Loading {path}");
        Console.WriteLine($"Found {assemblyPaths.Count} potential dependency DLLs");

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
