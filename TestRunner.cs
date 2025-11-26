using DllInspectorMcp;

Console.WriteLine("=== Civil3D DLL Inspector Test ===");
Console.WriteLine();

// Test 1: Database initialization
Console.WriteLine("Test 1: Initializing database...");
using var db = new DatabaseManager();
db.InitializeDatabase();
Console.WriteLine("✓ Database initialized");
Console.WriteLine();

// Test 2: Analyze a DLL
Console.WriteLine("Test 2: Analyzing AeccDbMgd.dll...");
var analyzer = new DllAnalyzer(db);
var dllPath = "/mnt/c/Program Files/Autodesk/AutoCAD 2024/C3D/AeccDbMgd.dll";

if (!File.Exists(dllPath))
{
    Console.WriteLine($"✗ DLL not found at: {dllPath}");
    Console.WriteLine("Please update the path to match your installation.");
    return;
}

try
{
    var startTime = DateTime.Now;
    analyzer.AnalyzeAssembly(dllPath, forceReAnalyze: false);
    var elapsed = DateTime.Now - startTime;
    Console.WriteLine($"✓ Analysis complete in {elapsed.TotalSeconds:F2} seconds");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return;
}
Console.WriteLine();

// Test 3: Query assemblies
Console.WriteLine("Test 3: Querying assemblies...");
var assemblies = db.ExecuteQuery("SELECT Name, Version, (SELECT COUNT(*) FROM Types t JOIN Namespaces n ON t.NamespaceId = n.Id WHERE n.AssemblyId = a.Id) AS TypeCount FROM Assemblies a");
foreach (var asm in assemblies)
{
    Console.WriteLine($"  - {asm["Name"]} v{asm["Version"]} ({asm["TypeCount"]} types)");
}
Console.WriteLine();

// Test 4: Query namespaces
Console.WriteLine("Test 4: Querying namespaces...");
var namespaces = db.ExecuteQuery(@"
    SELECT n.Name, COUNT(t.Id) as TypeCount
    FROM Namespaces n
    LEFT JOIN Types t ON t.NamespaceId = n.Id
    GROUP BY n.Name
    ORDER BY TypeCount DESC
    LIMIT 10");
Console.WriteLine("Top 10 namespaces by type count:");
foreach (var ns in namespaces)
{
    Console.WriteLine($"  - {ns["Name"]}: {ns["TypeCount"]} types");
}
Console.WriteLine();

// Test 5: Search for types
Console.WriteLine("Test 5: Searching for 'Alignment' types...");
var types = db.ExecuteQuery(@"
    SELECT t.Name, t.TypeKind, n.Name as Namespace
    FROM Types t
    JOIN Namespaces n ON t.NamespaceId = n.Id
    WHERE t.Name LIKE '%Alignment%'
    ORDER BY t.Name
    LIMIT 10");
foreach (var type in types)
{
    Console.WriteLine($"  - {type["TypeKind"]}: {type["Namespace"]}.{type["Name"]}");
}
Console.WriteLine();

// Test 6: Get type details
Console.WriteLine("Test 6: Getting details for 'Alignment' type...");
var typeInfo = db.ExecuteQuery(@"
    SELECT t.*, n.Name as Namespace
    FROM Types t
    JOIN Namespaces n ON t.NamespaceId = n.Id
    WHERE t.Name = 'Alignment'
    LIMIT 1");

if (typeInfo.Count > 0)
{
    var type = typeInfo[0];
    Console.WriteLine($"Type: {type["Namespace"]}.{type["Name"]}");
    Console.WriteLine($"Kind: {type["TypeKind"]}");
    Console.WriteLine($"Is Abstract: {type["IsAbstract"]}");
    Console.WriteLine($"Is Sealed: {type["IsSealed"]}");

    // Count members
    var memberCount = db.ExecuteQuery(@"
        SELECT MemberKind, COUNT(*) as Count
        FROM Members
        WHERE TypeId = @typeId
        GROUP BY MemberKind",
        new Microsoft.Data.Sqlite.SqliteParameter("@typeId", type["Id"]));

    Console.WriteLine("Members:");
    foreach (var member in memberCount)
    {
        Console.WriteLine($"  - {member["MemberKind"]}: {member["Count"]}");
    }
}
Console.WriteLine();

// Test 7: Get method signatures with full details
Console.WriteLine("Test 7: Getting method signatures for 'Alignment' type...");
var methodsWithParams = db.ExecuteQuery(@"
    SELECT m.Id, m.Name, m.ReturnType, m.IsStatic
    FROM Members m
    WHERE m.TypeId = (SELECT Id FROM Types WHERE Name = 'Alignment' LIMIT 1)
    AND m.MemberKind = 'Method'
    ORDER BY m.Name
    LIMIT 5");

Console.WriteLine($"Sample methods from Alignment class ({methodsWithParams.Count} shown):");
foreach (var method in methodsWithParams)
{
    var methodId = method["Id"];
    var methodName = method["Name"];
    var returnType = method["ReturnType"];
    var isStatic = Convert.ToInt32(method["IsStatic"]) == 1 ? "static " : "";

    // Get parameters for this method
    var parameters = db.ExecuteQuery(@"
        SELECT Name, ParameterType, Position, IsOptional, DefaultValue, IsOut, IsRef, IsIn
        FROM Parameters
        WHERE MemberId = @memberId
        ORDER BY Position",
        new Microsoft.Data.Sqlite.SqliteParameter("@memberId", methodId));

    // Build parameter string
    var paramList = string.Join(", ", parameters.Select(p =>
    {
        var prefix = "";
        if (Convert.ToInt32(p["IsOut"]) == 1) prefix = "out ";
        else if (Convert.ToInt32(p["IsRef"]) == 1) prefix = "ref ";
        else if (Convert.ToInt32(p["IsIn"]) == 1) prefix = "in ";

        var suffix = "";
        if (Convert.ToInt32(p["IsOptional"]) == 1 && p["DefaultValue"] != DBNull.Value)
            suffix = $" = {p["DefaultValue"]}";

        // Shorten type names for readability
        var typeName = p["ParameterType"]?.ToString() ?? "object";
        if (typeName.StartsWith("Autodesk."))
        {
            var parts = typeName.Split('.');
            typeName = parts[^1]; // Just the last part
        }

        return $"{prefix}{typeName} {p["Name"]}{suffix}";
    }));

    // Shorten return type
    var shortReturnType = returnType?.ToString() ?? "void";
    if (shortReturnType.StartsWith("Autodesk."))
    {
        var parts = shortReturnType.Split('.');
        shortReturnType = parts[^1];
    }

    Console.WriteLine($"  {isStatic}{shortReturnType} {methodName}({paramList})");
}
Console.WriteLine();

Console.WriteLine("=== All tests passed! ===");
