using System;
using System.Reflection;
using System.Runtime.InteropServices;

class TestDllLoader
{
    static void Main()
    {
        try
        {
            Console.WriteLine("Testing DLL loading with MetadataLoadContext...");

            var path = "C:\\Program Files\\Autodesk\\AutoCAD 2025\\C3D\\AeccPressurePipesMgd.dll";

            // Convert Windows path to WSL path if on Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
                path.Length >= 2 &&
                char.IsLetter(path[0]) &&
                path[1] == ':')
            {
                var driveLetter = char.ToLowerInvariant(path[0]);
                var pathWithoutDrive = path.Substring(2).Replace('\\', '/');
                path = $"/mnt/{driveLetter}{pathWithoutDrive}";
            }

            Console.WriteLine($"Path: {path}");

            if (!File.Exists(path))
            {
                Console.WriteLine($"ERROR: File not found at {path}");
                return;
            }

            var directory = Path.GetDirectoryName(path)!;
            Console.WriteLine($"Directory: {directory}");

            // Get paths to resolve dependencies
            var paths = new List<string>();

            if (Directory.Exists(directory))
            {
                var dlls = Directory.GetFiles(directory, "*.dll");
                Console.WriteLine($"Found {dlls.Length} DLLs in directory");
                paths.AddRange(dlls);
            }

            var runtimePath = RuntimeEnvironment.GetRuntimeDirectory();
            var runtimeDlls = Directory.GetFiles(runtimePath, "*.dll");
            Console.WriteLine($"Found {runtimeDlls.Length} runtime DLLs");
            paths.AddRange(runtimeDlls);

            Console.WriteLine($"Total resolver paths: {paths.Count}");
            Console.WriteLine("Creating MetadataLoadContext...");

            var resolver = new PathAssemblyResolver(paths);
            var mlc = new MetadataLoadContext(resolver);

            Console.WriteLine("Loading assembly...");
            var assembly = mlc.LoadFromAssemblyPath(path);

            Console.WriteLine($"Assembly loaded: {assembly.FullName}");
            Console.WriteLine("Getting exported types...");

            var types = assembly.GetExportedTypes();
            Console.WriteLine($"Found {types.Length} exported types");

            Console.WriteLine("\nFirst 10 types:");
            foreach (var type in types.Take(10))
            {
                Console.WriteLine($"  {type.FullName}");
            }

            Console.WriteLine("\n✓ SUCCESS! MetadataLoadContext can load AutoCAD DLLs");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ ERROR: {ex.Message}");
            Console.WriteLine($"Type: {ex.GetType().Name}");
            Console.WriteLine($"Stack trace:\n{ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Console.WriteLine($"\nInner exception: {ex.InnerException.Message}");
            }
        }
    }
}
