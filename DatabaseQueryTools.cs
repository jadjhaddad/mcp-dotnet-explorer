using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DllInspectorMcp;

[McpServerToolType]
public static class DatabaseQueryTools
{
    private static DatabaseManager? _db;
    private static readonly object _lock = new();

    private static DatabaseManager GetDatabase()
    {
        lock (_lock)
        {
            if (_db == null)
            {
                _db = new DatabaseManager();
                // Check if database is initialized
                try
                {
                    _db.ExecuteScalar("SELECT COUNT(*) FROM Assemblies");
                }
                catch
                {
                    // Database not initialized, create schema
                    _db.InitializeDatabase();
                }
            }
            return _db;
        }
    }

    [McpServerTool, Description("Analyzes a .NET DLL and stores all metadata in the database. This must be run before querying.")]
    public static string AnalyzeDll(
        [Description("Path to the DLL file")] string dllPath,
        [Description("Force re-analysis even if already in database")] bool forceReAnalyze = false,
        [Description("Optional software name to associate with this DLL (e.g., 'AutoCAD Civil 3D 2024')")] string? softwareName = null,
        [Description("Optional software version")] string? softwareVersion = null)
    {
        if (string.IsNullOrEmpty(dllPath))
            return "Error: dllPath parameter is required";

        try
        {
            var db = GetDatabase();
            var analyzer = new DllAnalyzer(db);

            long? softwareId = null;
            if (!string.IsNullOrEmpty(softwareName))
            {
                // Find or create software entry
                var existing = db.ExecuteQuery(
                    "SELECT Id FROM Software WHERE Name = @name",
                    new Microsoft.Data.Sqlite.SqliteParameter("@name", softwareName));

                if (existing.Count > 0)
                {
                    softwareId = Convert.ToInt64(existing[0]["Id"]);
                }
                else
                {
                    // Create new software entry
                    softwareId = db.ExecuteInsert(
                        "INSERT INTO Software (Name, Version, CreatedDate) VALUES (@name, @version, @date)",
                        new Microsoft.Data.Sqlite.SqliteParameter("@name", softwareName),
                        new Microsoft.Data.Sqlite.SqliteParameter("@version", softwareVersion ?? ""),
                        new Microsoft.Data.Sqlite.SqliteParameter("@date", DateTime.UtcNow.ToString("o")));
                }
            }

            analyzer.AnalyzeAssembly(dllPath, forceReAnalyze, softwareId);

            var msg = $"Successfully analyzed DLL: {dllPath}";
            if (!string.IsNullOrEmpty(softwareName))
                msg += $"\nAssociated with software: {softwareName}";
            return msg;
        }
        catch (Exception ex)
        {
            return $"Error analyzing DLL: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool, Description("Lists all assemblies currently in the database")]
    public static string ListAssemblies([Description("Optional software name to filter by")] string? softwareName = null)
    {
        try
        {
            var db = GetDatabase();
            string sql = @"
                SELECT a.Name, a.Version, a.AnalyzedDate, s.Name as Software, s.Version as SoftwareVersion,
                       (SELECT COUNT(*) FROM Namespaces WHERE AssemblyId = a.Id) as NamespaceCount,
                       (SELECT COUNT(*) FROM Types t JOIN Namespaces n ON t.NamespaceId = n.Id WHERE n.AssemblyId = a.Id) as TypeCount
                FROM Assemblies a
                LEFT JOIN Software s ON a.SoftwareId = s.Id";

            if (!string.IsNullOrEmpty(softwareName))
            {
                sql += " WHERE s.Name = @softwareName";
            }

            sql += " ORDER BY s.Name, a.Name, a.Version";

            var results = string.IsNullOrEmpty(softwareName)
                ? db.ExecuteQuery(sql)
                : db.ExecuteQuery(sql, new Microsoft.Data.Sqlite.SqliteParameter("@softwareName", softwareName));

            if (results.Count == 0)
            {
                if (string.IsNullOrEmpty(softwareName))
                    return "No assemblies found in database. Use analyze_dll to add assemblies.";
                else
                    return $"No assemblies found for software: {softwareName}";
            }

            var sb = new StringBuilder();
            sb.AppendLine("ASSEMBLIES IN DATABASE:");
            sb.AppendLine();

            foreach (var row in results)
            {
                sb.AppendLine($"Name: {row["Name"]}");
                sb.AppendLine($"  Version: {row["Version"]}");
                if (row["Software"] != null)
                    sb.AppendLine($"  Software: {row["Software"]} {row["SoftwareVersion"]}");
                sb.AppendLine($"  Analyzed: {row["AnalyzedDate"]}");
                sb.AppendLine($"  Namespaces: {row["NamespaceCount"]}");
                sb.AppendLine($"  Types: {row["TypeCount"]}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing assemblies: {ex.Message}";
        }
    }

    [McpServerTool, Description("Lists all namespaces in the database, optionally filtered by assembly")]
    public static string ListNamespaces([Description("Optional assembly name to filter by")] string? assemblyName = null)
    {
        try
        {
            var db = GetDatabase();
            string sql;

            if (string.IsNullOrEmpty(assemblyName))
            {
                sql = @"
                    SELECT n.Name, a.Name as Assembly, a.Version,
                           (SELECT COUNT(*) FROM Types WHERE NamespaceId = n.Id) as TypeCount
                    FROM Namespaces n
                    JOIN Assemblies a ON n.AssemblyId = a.Id
                    ORDER BY n.Name";
            }
            else
            {
                sql = @"
                    SELECT n.Name, a.Name as Assembly, a.Version,
                           (SELECT COUNT(*) FROM Types WHERE NamespaceId = n.Id) as TypeCount
                    FROM Namespaces n
                    JOIN Assemblies a ON n.AssemblyId = a.Id
                    WHERE a.Name = @assemblyName
                    ORDER BY n.Name";
            }

            var results = string.IsNullOrEmpty(assemblyName)
                ? db.ExecuteQuery(sql)
                : db.ExecuteQuery(sql, new Microsoft.Data.Sqlite.SqliteParameter("@assemblyName", assemblyName));

            if (results.Count == 0)
                return string.IsNullOrEmpty(assemblyName)
                    ? "No namespaces found in database."
                    : $"No namespaces found for assembly: {assemblyName}";

            var sb = new StringBuilder();
            sb.AppendLine("NAMESPACES:");
            sb.AppendLine();

            foreach (var row in results)
            {
                sb.AppendLine($"{row["Name"]} ({row["TypeCount"]} types) - {row["Assembly"]} v{row["Version"]}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing namespaces: {ex.Message}";
        }
    }

    [McpServerTool, Description("Searches for types by name or pattern. Supports wildcards (%).")]
    public static string SearchTypes(
        [Description("Search pattern for type name (use % as wildcard)")] string searchPattern,
        [Description("Optional namespace to filter by")] string? namespaceName = null,
        [Description("Optional type kind to filter by (Class, Interface, Enum, Struct)")] string? typeKind = null)
    {
        if (string.IsNullOrEmpty(searchPattern))
            return "Error: searchPattern parameter is required";

        try
        {
            var db = GetDatabase();
            var sb = new StringBuilder();
            sb.Append(@"
                SELECT t.Name, t.FullName, t.TypeKind, t.IsAbstract, t.IsSealed, t.IsStatic,
                       n.Name as Namespace, a.Name as Assembly, bt.FullName as BaseType
                FROM Types t
                JOIN Namespaces n ON t.NamespaceId = n.Id
                JOIN Assemblies a ON n.AssemblyId = a.Id
                LEFT JOIN Types bt ON t.BaseTypeId = bt.Id
                WHERE t.Name LIKE @searchPattern");

            var parameters = new List<Microsoft.Data.Sqlite.SqliteParameter>
            {
                new("@searchPattern", searchPattern)
            };

            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.Append(" AND n.Name = @namespace");
                parameters.Add(new("@namespace", namespaceName));
            }

            if (!string.IsNullOrEmpty(typeKind))
            {
                sb.Append(" AND t.TypeKind = @typeKind");
                parameters.Add(new("@typeKind", typeKind));
            }

            sb.Append(" ORDER BY t.FullName");

            var results = db.ExecuteQuery(sb.ToString(), parameters.ToArray());

            if (results.Count == 0)
                return $"No types found matching pattern: {searchPattern}";

            var output = new StringBuilder();
            output.AppendLine($"TYPES MATCHING '{searchPattern}': ({results.Count} found)");
            output.AppendLine();

            foreach (var row in results)
            {
                var modifiers = new List<string>();
                if (Convert.ToInt32(row["IsAbstract"]) == 1) modifiers.Add("abstract");
                if (Convert.ToInt32(row["IsSealed"]) == 1) modifiers.Add("sealed");
                if (Convert.ToInt32(row["IsStatic"]) == 1) modifiers.Add("static");

                var modifier = modifiers.Any() ? string.Join(" ", modifiers) + " " : "";
                output.AppendLine($"{modifier}{row["TypeKind"]}: {row["FullName"]}");

                if (row["BaseType"] != null && row["BaseType"].ToString() != "System.Object")
                    output.AppendLine($"  Base: {row["BaseType"]}");

                output.AppendLine($"  Assembly: {row["Assembly"]}");
                output.AppendLine();
            }

            return output.ToString();
        }
        catch (Exception ex)
        {
            return $"Error searching types: {ex.Message}";
        }
    }

    [McpServerTool, Description("Gets detailed information about a specific type including all its members")]
    public static string GetTypeDetails(
        [Description("Full type name (e.g., 'Autodesk.Civil.DatabaseServices.Alignment')")] string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "Error: typeName parameter is required";

        try
        {
            var db = GetDatabase();

            // Get type info
            var typeResults = db.ExecuteQuery(@"
                SELECT t.*, n.Name as Namespace, a.Name as Assembly, a.Version, bt.FullName as BaseType
                FROM Types t
                JOIN Namespaces n ON t.NamespaceId = n.Id
                JOIN Assemblies a ON n.AssemblyId = a.Id
                LEFT JOIN Types bt ON t.BaseTypeId = bt.Id
                WHERE t.FullName = @typeName",
                new Microsoft.Data.Sqlite.SqliteParameter("@typeName", typeName));

            if (typeResults.Count == 0)
                return $"Type not found: {typeName}";

            var type = typeResults[0];
            var sb = new StringBuilder();

            // Type header
            sb.AppendLine($"TYPE: {type["FullName"]}");
            sb.AppendLine($"Assembly: {type["Assembly"]} v{type["Version"]}");
            sb.AppendLine($"Namespace: {type["Namespace"]}");
            sb.AppendLine($"Kind: {type["TypeKind"]}");

            var modifiers = new List<string>();
            if (Convert.ToInt32(type["IsAbstract"]) == 1) modifiers.Add("abstract");
            if (Convert.ToInt32(type["IsSealed"]) == 1) modifiers.Add("sealed");
            if (Convert.ToInt32(type["IsStatic"]) == 1) modifiers.Add("static");
            if (modifiers.Any())
                sb.AppendLine($"Modifiers: {string.Join(", ", modifiers)}");

            if (type["BaseType"] != null)
                sb.AppendLine($"Base Type: {type["BaseType"]}");

            sb.AppendLine();

            // Get interfaces
            var interfaces = db.ExecuteQuery(@"
                SELECT t.FullName
                FROM TypeInterfaces ti
                JOIN Types t ON ti.InterfaceTypeId = t.Id
                WHERE ti.TypeId = (SELECT Id FROM Types WHERE FullName = @typeName)",
                new Microsoft.Data.Sqlite.SqliteParameter("@typeName", typeName));

            if (interfaces.Count > 0)
            {
                sb.AppendLine("INTERFACES:");
                foreach (var iface in interfaces)
                {
                    sb.AppendLine($"  {iface["FullName"]}");
                }
                sb.AppendLine();
            }

            // Get members grouped by kind
            var members = db.ExecuteQuery(@"
                SELECT *
                FROM Members
                WHERE TypeId = (SELECT Id FROM Types WHERE FullName = @typeName)
                ORDER BY MemberKind, Name",
                new Microsoft.Data.Sqlite.SqliteParameter("@typeName", typeName));

            var membersByKind = members.GroupBy(m => m["MemberKind"]?.ToString() ?? "Unknown");

            foreach (var group in membersByKind.OrderBy(g => g.Key))
            {
                sb.AppendLine($"{group.Key.ToUpper()}S:");

                foreach (var member in group)
                {
                    var memberModifiers = new List<string>();
                    if (Convert.ToInt32(member["IsStatic"]) == 1) memberModifiers.Add("static");
                    if (Convert.ToInt32(member["IsVirtual"]) == 1) memberModifiers.Add("virtual");
                    if (Convert.ToInt32(member["IsAbstract"]) == 1) memberModifiers.Add("abstract");
                    if (Convert.ToInt32(member["IsOverride"]) == 1) memberModifiers.Add("override");

                    var memberMod = memberModifiers.Any() ? string.Join(" ", memberModifiers) + " " : "";

                    if (group.Key == "Method" || group.Key == "Constructor")
                    {
                        // Get parameters
                        var memberId = member["Id"];
                        var parameters = db.ExecuteQuery(@"
                            SELECT Name, ParameterType, IsOptional, DefaultValue, IsOut, IsRef, IsIn
                            FROM Parameters
                            WHERE MemberId = @memberId
                            ORDER BY Position",
                            new Microsoft.Data.Sqlite.SqliteParameter("@memberId", memberId));

                        var paramStr = string.Join(", ", parameters.Select(p =>
                        {
                            var prefix = "";
                            if (Convert.ToInt32(p["IsOut"]) == 1) prefix = "out ";
                            else if (Convert.ToInt32(p["IsRef"]) == 1) prefix = "ref ";
                            else if (Convert.ToInt32(p["IsIn"]) == 1) prefix = "in ";

                            var suffix = "";
                            if (Convert.ToInt32(p["IsOptional"]) == 1)
                                suffix = $" = {p["DefaultValue"]}";

                            return $"{prefix}{p["ParameterType"]} {p["Name"]}{suffix}";
                        }));

                        if (group.Key == "Constructor")
                            sb.AppendLine($"  {memberMod}{member["Name"]}({paramStr})");
                        else
                            sb.AppendLine($"  {memberMod}{member["ReturnType"]} {member["Name"]}({paramStr})");
                    }
                    else if (group.Key == "Property")
                    {
                        var memberId = member["Id"];
                        var accessor = db.ExecuteQuery(@"
                            SELECT HasGetter, HasSetter, GetterVisibility, SetterVisibility
                            FROM PropertyAccessors
                            WHERE MemberId = @memberId",
                            new Microsoft.Data.Sqlite.SqliteParameter("@memberId", memberId)).FirstOrDefault();

                        var accessors = new List<string>();
                        if (accessor != null)
                        {
                            if (Convert.ToInt32(accessor["HasGetter"]) == 1) accessors.Add("get");
                            if (Convert.ToInt32(accessor["HasSetter"]) == 1) accessors.Add("set");
                        }

                        sb.AppendLine($"  {memberMod}{member["ReturnType"]} {member["Name"]} {{ {string.Join("; ", accessors)}; }}");
                    }
                    else
                    {
                        sb.AppendLine($"  {memberMod}{member["ReturnType"]} {member["Name"]}");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting type details: {ex.Message}\n{ex.StackTrace}";
        }
    }

    [McpServerTool, Description("Searches for methods by name pattern across all types")]
    public static string SearchMethods(
        [Description("Search pattern for method name (use % as wildcard)")] string searchPattern,
        [Description("Optional return type to filter by")] string? returnType = null)
    {
        if (string.IsNullOrEmpty(searchPattern))
            return "Error: searchPattern parameter is required";

        try
        {
            var db = GetDatabase();
            var sb = new StringBuilder();
            sb.Append(@"
                SELECT m.Name, m.ReturnType, m.IsStatic, t.FullName as TypeName
                FROM Members m
                JOIN Types t ON m.TypeId = t.Id
                WHERE m.MemberKind = 'Method' AND m.Name LIKE @searchPattern");

            var parameters = new List<Microsoft.Data.Sqlite.SqliteParameter>
            {
                new("@searchPattern", searchPattern)
            };

            if (!string.IsNullOrEmpty(returnType))
            {
                sb.Append(" AND m.ReturnType LIKE @returnType");
                parameters.Add(new("@returnType", returnType));
            }

            sb.Append(" ORDER BY t.FullName, m.Name");

            var results = db.ExecuteQuery(sb.ToString(), parameters.ToArray());

            if (results.Count == 0)
                return $"No methods found matching pattern: {searchPattern}";

            var output = new StringBuilder();
            output.AppendLine($"METHODS MATCHING '{searchPattern}': ({results.Count} found)");
            output.AppendLine();

            foreach (var row in results)
            {
                var modifier = Convert.ToInt32(row["IsStatic"]) == 1 ? "static " : "";
                output.AppendLine($"{row["TypeName"]}");
                output.AppendLine($"  {modifier}{row["ReturnType"]} {row["Name"]}()");
                output.AppendLine();
            }

            return output.ToString();
        }
        catch (Exception ex)
        {
            return $"Error searching methods: {ex.Message}";
        }
    }

    [McpServerTool, Description("Full-text search across all type and member documentation")]
    public static string SearchDocumentation([Description("Search terms to find in documentation")] string searchTerms)
    {
        if (string.IsNullOrEmpty(searchTerms))
            return "Error: searchTerms parameter is required";

        try
        {
            var db = GetDatabase();
            var results = db.ExecuteQuery(@"
                SELECT MemberId, TypeName, MemberName, Summary
                FROM MembersSearch
                WHERE MembersSearch MATCH @searchTerms
                LIMIT 50",
                new Microsoft.Data.Sqlite.SqliteParameter("@searchTerms", searchTerms));

            if (results.Count == 0)
                return $"No documentation found matching: {searchTerms}";

            var sb = new StringBuilder();
            sb.AppendLine($"DOCUMENTATION SEARCH RESULTS FOR '{searchTerms}': ({results.Count} found)");
            sb.AppendLine();

            foreach (var row in results)
            {
                sb.AppendLine($"{row["TypeName"]}.{row["MemberName"]}");
                if (row["Summary"] != null)
                    sb.AppendLine($"  {row["Summary"]}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error searching documentation: {ex.Message}";
        }
    }

    [McpServerTool, Description("Gets the inheritance hierarchy for a type (base classes and derived classes)")]
    public static string GetInheritanceTree([Description("Full type name")] string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return "Error: typeName parameter is required";

        try
        {
            var db = GetDatabase();
            var sb = new StringBuilder();
            sb.AppendLine($"INHERITANCE TREE FOR: {typeName}");
            sb.AppendLine();

            // Get base classes
            sb.AppendLine("BASE CLASSES:");
            var currentType = typeName;
            int indent = 0;

            while (currentType != null)
            {
                var result = db.ExecuteQuery(@"
                    SELECT t.FullName, bt.FullName as BaseType
                    FROM Types t
                    LEFT JOIN Types bt ON t.BaseTypeId = bt.Id
                    WHERE t.FullName = @typeName",
                    new Microsoft.Data.Sqlite.SqliteParameter("@typeName", currentType));

                if (result.Count == 0) break;

                var row = result[0];
                sb.AppendLine($"{new string(' ', indent * 2)}{row["FullName"]}");

                currentType = row["BaseType"]?.ToString();
                if (currentType == "System.Object" || currentType == null)
                {
                    if (currentType == "System.Object")
                        sb.AppendLine($"{new string(' ', (indent + 1) * 2)}System.Object");
                    break;
                }
                indent++;
            }

            sb.AppendLine();

            // Get derived classes
            var derived = db.ExecuteQuery(@"
                SELECT t.FullName
                FROM Types t
                JOIN Types bt ON t.BaseTypeId = bt.Id
                WHERE bt.FullName = @typeName
                ORDER BY t.FullName",
                new Microsoft.Data.Sqlite.SqliteParameter("@typeName", typeName));

            if (derived.Count > 0)
            {
                sb.AppendLine("DERIVED CLASSES:");
                foreach (var row in derived)
                {
                    sb.AppendLine($"  {row["FullName"]}");
                }
            }
            else
            {
                sb.AppendLine("DERIVED CLASSES: None");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error getting inheritance tree: {ex.Message}";
        }
    }

    [McpServerTool, Description("Finds all types that implement a specific interface")]
    public static string FindImplementations([Description("Full interface name")] string interfaceName)
    {
        if (string.IsNullOrEmpty(interfaceName))
            return "Error: interfaceName parameter is required";

        try
        {
            var db = GetDatabase();
            var results = db.ExecuteQuery(@"
                SELECT t.FullName, t.TypeKind, n.Name as Namespace
                FROM Types t
                JOIN TypeInterfaces ti ON t.Id = ti.TypeId
                JOIN Types it ON ti.InterfaceTypeId = it.Id
                JOIN Namespaces n ON t.NamespaceId = n.Id
                WHERE it.FullName = @interfaceName
                ORDER BY t.FullName",
                new Microsoft.Data.Sqlite.SqliteParameter("@interfaceName", interfaceName));

            if (results.Count == 0)
                return $"No implementations found for interface: {interfaceName}";

            var sb = new StringBuilder();
            sb.AppendLine($"IMPLEMENTATIONS OF '{interfaceName}': ({results.Count} found)");
            sb.AppendLine();

            foreach (var row in results)
            {
                sb.AppendLine($"{row["TypeKind"]}: {row["FullName"]}");
                sb.AppendLine($"  Namespace: {row["Namespace"]}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error finding implementations: {ex.Message}";
        }
    }

    [McpServerTool, Description("Lists all software products in the database")]
    public static string ListSoftware()
    {
        try
        {
            var db = GetDatabase();
            var results = db.ExecuteQuery(@"
                SELECT s.Name, s.Version, s.Description,
                       (SELECT COUNT(*) FROM Assemblies WHERE SoftwareId = s.Id) as AssemblyCount
                FROM Software s
                ORDER BY s.Name, s.Version");

            if (results.Count == 0)
                return "No software products found in database.";

            var sb = new StringBuilder();
            sb.AppendLine("SOFTWARE PRODUCTS:");
            sb.AppendLine();

            foreach (var row in results)
            {
                sb.AppendLine($"Name: {row["Name"]}");
                if (!string.IsNullOrEmpty(row["Version"]?.ToString()))
                    sb.AppendLine($"  Version: {row["Version"]}");
                if (!string.IsNullOrEmpty(row["Description"]?.ToString()))
                    sb.AppendLine($"  Description: {row["Description"]}");
                sb.AppendLine($"  Assemblies: {row["AssemblyCount"]}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error listing software: {ex.Message}";
        }
    }

    [McpServerTool, Description("Associates an existing assembly with a software product")]
    public static string AssociateAssemblyWithSoftware(
        [Description("Assembly name")] string assemblyName,
        [Description("Software name")] string softwareName)
    {
        if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(softwareName))
            return "Error: Both assemblyName and softwareName are required";

        try
        {
            var db = GetDatabase();

            // Find software
            var software = db.ExecuteQuery(
                "SELECT Id FROM Software WHERE Name = @name",
                new Microsoft.Data.Sqlite.SqliteParameter("@name", softwareName));

            if (software.Count == 0)
                return $"Error: Software not found: {softwareName}. Create it first using analyze_dll with softwareName parameter.";

            var softwareId = Convert.ToInt64(software[0]["Id"]);

            // Update assembly
            var updated = db.ExecuteScalar(
                "UPDATE Assemblies SET SoftwareId = @softwareId WHERE Name = @assemblyName; SELECT changes()",
                new Microsoft.Data.Sqlite.SqliteParameter("@softwareId", softwareId),
                new Microsoft.Data.Sqlite.SqliteParameter("@assemblyName", assemblyName));

            if (Convert.ToInt64(updated) == 0)
                return $"Error: Assembly not found: {assemblyName}";

            return $"Successfully associated {assemblyName} with {softwareName}";
        }
        catch (Exception ex)
        {
            return $"Error associating assembly: {ex.Message}";
        }
    }
}
