using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace DllInspectorMcp;

public class DllAnalyzer
{
    private readonly DatabaseManager _db;

    public DllAnalyzer(DatabaseManager db)
    {
        _db = db;
    }

    public void AnalyzeAssembly(string dllPath, bool forceReAnalyze = false)
    {
        dllPath = DllLoader.ConvertToAbsolutePath(dllPath);

        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException($"DLL file not found: {dllPath}");
        }

        var assembly = DllLoader.LoadAssembly(dllPath);
        var assemblyName = assembly.GetName();
        var fullName = assembly.FullName ?? assemblyName.Name ?? "Unknown";
        var fileHash = DatabaseManager.ComputeFileHash(dllPath);

        // Check if already analyzed and hasn't changed
        if (!forceReAnalyze && _db.IsAssemblyAnalyzed(fullName))
        {
            var existingHash = _db.GetAssemblyHash(fullName);
            if (existingHash == fileHash)
            {
                Console.WriteLine($"Assembly {assemblyName.Name} is already up-to-date in database.");
                return;
            }

            Console.WriteLine($"Assembly {assemblyName.Name} has changed. Re-analyzing...");
            _db.DeleteAssemblyData(fullName);
        }

        Console.WriteLine($"Analyzing assembly: {assemblyName.Name} v{assemblyName.Version}");

        using var transaction = _db.BeginTransaction();
        try
        {
            // Insert assembly record
            var assemblyId = InsertAssembly(assembly, dllPath, fileHash);

            // Get all types
            var types = assembly.GetExportedTypes().OrderBy(t => t.FullName).ToArray();
            Console.WriteLine($"Found {types.Length} public types");

            // Create namespace cache
            var namespaceCache = new Dictionary<string, long>();

            // Process all types
            for (int i = 0; i < types.Length; i++)
            {
                if (i % 100 == 0)
                {
                    Console.WriteLine($"Processing type {i + 1}/{types.Length}...");
                }

                var type = types[i];

                // Get or create namespace
                var namespaceName = type.Namespace ?? "(global)";
                if (!namespaceCache.TryGetValue(namespaceName, out var namespaceId))
                {
                    namespaceId = GetOrCreateNamespace(assemblyId, namespaceName);
                    namespaceCache[namespaceName] = namespaceId;
                }

                // Insert type and its members
                AnalyzeType(type, namespaceId);
            }

            transaction.Commit();
            Console.WriteLine($"Successfully analyzed assembly: {assemblyName.Name}");
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private long InsertAssembly(Assembly assembly, string filePath, string fileHash)
    {
        var assemblyName = assembly.GetName();
        var name = assemblyName.Name ?? "Unknown";
        var fullName = assembly.FullName ?? name;
        var version = assemblyName.Version?.ToString() ?? "0.0.0.0";

        // Get target framework using CustomAttributeData (required for MetadataLoadContext)
        string targetFramework = "Unknown";
        try
        {
            var tfAttr = assembly.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType.Name == "TargetFrameworkAttribute");
            if (tfAttr != null && tfAttr.ConstructorArguments.Count > 0)
            {
                targetFramework = tfAttr.ConstructorArguments[0].Value?.ToString() ?? "Unknown";
            }
        }
        catch
        {
            // Ignore if we can't get the framework
        }

        return _db.ExecuteInsert(
            @"INSERT INTO Assemblies (Name, FullName, Version, FilePath, TargetFramework, AnalyzedDate, FileHash)
              VALUES (@name, @fullName, @version, @filePath, @framework, @date, @hash)",
            new SqliteParameter("@name", name),
            new SqliteParameter("@fullName", fullName),
            new SqliteParameter("@version", version),
            new SqliteParameter("@filePath", filePath),
            new SqliteParameter("@framework", targetFramework),
            new SqliteParameter("@date", DateTime.UtcNow.ToString("O")),
            new SqliteParameter("@hash", fileHash)
        );
    }

    private long GetOrCreateNamespace(long assemblyId, string namespaceName)
    {
        var existing = _db.ExecuteScalar(
            "SELECT Id FROM Namespaces WHERE AssemblyId = @assemblyId AND Name = @name",
            new SqliteParameter("@assemblyId", assemblyId),
            new SqliteParameter("@name", namespaceName)
        );

        if (existing != null)
        {
            return Convert.ToInt64(existing);
        }

        return _db.ExecuteInsert(
            "INSERT INTO Namespaces (AssemblyId, Name) VALUES (@assemblyId, @name)",
            new SqliteParameter("@assemblyId", assemblyId),
            new SqliteParameter("@name", namespaceName)
        );
    }

    private void AnalyzeType(Type type, long namespaceId)
    {
        // Determine type kind
        string typeKind;
        if (type.IsEnum) typeKind = "Enum";
        else if (type.IsInterface) typeKind = "Interface";
        else if (type.IsValueType) typeKind = "Struct";
        else if (typeof(Delegate).IsAssignableFrom(type)) typeKind = "Delegate";
        else typeKind = "Class";

        // Handle generic parameters
        var genericParams = type.IsGenericType ? JsonSerializer.Serialize(type.GetGenericArguments().Select(t => t.Name).ToArray()) : null;

        // Get base type ID (will be set later in a second pass if needed, for now just store the name)
        var baseTypeName = type.BaseType?.FullName;

        // Insert type
        var typeId = _db.ExecuteInsert(
            @"INSERT INTO Types (NamespaceId, Name, FullName, TypeKind, IsAbstract, IsSealed, IsPublic, IsStatic, IsGeneric, GenericParameters, Summary)
              VALUES (@namespaceId, @name, @fullName, @typeKind, @isAbstract, @isSealed, @isPublic, @isStatic, @isGeneric, @genericParams, @summary)",
            new SqliteParameter("@namespaceId", namespaceId),
            new SqliteParameter("@name", type.Name),
            new SqliteParameter("@fullName", type.FullName ?? type.Name),
            new SqliteParameter("@typeKind", typeKind),
            new SqliteParameter("@isAbstract", type.IsAbstract ? 1 : 0),
            new SqliteParameter("@isSealed", type.IsSealed ? 1 : 0),
            new SqliteParameter("@isPublic", type.IsPublic || type.IsNestedPublic ? 1 : 0),
            new SqliteParameter("@isStatic", type.IsAbstract && type.IsSealed ? 1 : 0),
            new SqliteParameter("@isGeneric", type.IsGenericType ? 1 : 0),
            new SqliteParameter("@genericParams", (object?)genericParams ?? DBNull.Value),
            new SqliteParameter("@summary", (object?)ExtractDocComment(type) ?? DBNull.Value)
        );

        // Insert interfaces
        foreach (var iface in type.GetInterfaces())
        {
            // Store interface relationship (will need to resolve IDs in a second pass)
            InsertTypeInterface(typeId, iface.FullName ?? iface.Name);
        }

        // Process enum values
        if (type.IsEnum)
        {
            AnalyzeEnumValues(type, typeId);
        }
        else
        {
            // Process constructors
            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            foreach (var ctor in constructors)
            {
                AnalyzeConstructor(ctor, typeId);
            }

            // Process properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var prop in properties)
            {
                AnalyzeProperty(prop, typeId);
            }

            // Process methods
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName); // Exclude property getters/setters
            foreach (var method in methods)
            {
                AnalyzeMethod(method, typeId);
            }

            // Process fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                AnalyzeField(field, typeId);
            }

            // Process events
            var events = type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var evt in events)
            {
                AnalyzeEvent(evt, typeId);
            }
        }
    }

    private void InsertTypeInterface(long typeId, string interfaceName)
    {
        // For now, just store in a temporary way. We'll need a second pass to resolve all type IDs
        // This is a simplified version - in production, you'd want to handle this more carefully
        _db.ExecuteNonQuery(
            @"INSERT OR IGNORE INTO TypeInterfaces (TypeId, InterfaceTypeId)
              SELECT @typeId, Id FROM Types WHERE FullName = @interfaceName",
            new SqliteParameter("@typeId", typeId),
            new SqliteParameter("@interfaceName", interfaceName)
        );
    }

    private void AnalyzeEnumValues(Type enumType, long typeId)
    {
        // GetEnumValues() doesn't work with MetadataLoadContext - use GetFields instead
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);

        foreach (var field in fields)
        {
            var name = field.Name;
            var value = field.GetRawConstantValue();

            _db.ExecuteInsert(
                "INSERT INTO EnumValues (TypeId, Name, Value, Summary) VALUES (@typeId, @name, @value, @summary)",
                new SqliteParameter("@typeId", typeId),
                new SqliteParameter("@name", name),
                new SqliteParameter("@value", value?.ToString() ?? ""),
                new SqliteParameter("@summary", (object?)ExtractDocComment(field) ?? DBNull.Value)
            );
        }
    }

    private void AnalyzeConstructor(ConstructorInfo ctor, long typeId)
    {
        var memberId = _db.ExecuteInsert(
            @"INSERT INTO Members (TypeId, Name, MemberKind, ReturnType, IsStatic, IsPublic, Summary)
              VALUES (@typeId, @name, @kind, @returnType, @isStatic, @isPublic, @summary)",
            new SqliteParameter("@typeId", typeId),
            new SqliteParameter("@name", ctor.DeclaringType?.Name ?? "Constructor"),
            new SqliteParameter("@kind", "Constructor"),
            new SqliteParameter("@returnType", DBNull.Value),
            new SqliteParameter("@isStatic", ctor.IsStatic ? 1 : 0),
            new SqliteParameter("@isPublic", ctor.IsPublic ? 1 : 0),
            new SqliteParameter("@summary", (object?)ExtractDocComment(ctor) ?? DBNull.Value)
        );

        AnalyzeParameters(ctor.GetParameters(), memberId);
    }

    private void AnalyzeMethod(MethodInfo method, long typeId)
    {
        var genericParams = method.IsGenericMethod ? JsonSerializer.Serialize(method.GetGenericArguments().Select(t => t.Name).ToArray()) : null;

        // GetBaseDefinition() doesn't work with MetadataLoadContext
        // Use a simpler heuristic: virtual + not new slot = likely override
        bool isOverride = method.IsVirtual && !method.IsHideBySig;

        var memberId = _db.ExecuteInsert(
            @"INSERT INTO Members (TypeId, Name, MemberKind, ReturnType, IsStatic, IsVirtual, IsAbstract, IsSealed, IsOverride,
                                    IsPublic, IsProtected, IsInternal, IsPrivate, IsGeneric, GenericParameters, Summary)
              VALUES (@typeId, @name, @kind, @returnType, @isStatic, @isVirtual, @isAbstract, @isSealed, @isOverride,
                      @isPublic, @isProtected, @isInternal, @isPrivate, @isGeneric, @genericParams, @summary)",
            new SqliteParameter("@typeId", typeId),
            new SqliteParameter("@name", method.Name),
            new SqliteParameter("@kind", "Method"),
            new SqliteParameter("@returnType", method.ReturnType.FullName ?? method.ReturnType.Name),
            new SqliteParameter("@isStatic", method.IsStatic ? 1 : 0),
            new SqliteParameter("@isVirtual", method.IsVirtual && !method.IsFinal ? 1 : 0),
            new SqliteParameter("@isAbstract", method.IsAbstract ? 1 : 0),
            new SqliteParameter("@isSealed", method.IsFinal && method.IsVirtual ? 1 : 0),
            new SqliteParameter("@isOverride", isOverride ? 1 : 0),
            new SqliteParameter("@isPublic", method.IsPublic ? 1 : 0),
            new SqliteParameter("@isProtected", method.IsFamily || method.IsFamilyOrAssembly ? 1 : 0),
            new SqliteParameter("@isInternal", method.IsAssembly || method.IsFamilyOrAssembly ? 1 : 0),
            new SqliteParameter("@isPrivate", method.IsPrivate ? 1 : 0),
            new SqliteParameter("@isGeneric", method.IsGenericMethod ? 1 : 0),
            new SqliteParameter("@genericParams", (object?)genericParams ?? DBNull.Value),
            new SqliteParameter("@summary", (object?)ExtractDocComment(method) ?? DBNull.Value)
        );

        AnalyzeParameters(method.GetParameters(), memberId);
    }

    private void AnalyzeProperty(PropertyInfo property, long typeId)
    {
        var memberId = _db.ExecuteInsert(
            @"INSERT INTO Members (TypeId, Name, MemberKind, ReturnType, IsStatic, IsPublic, IsReadOnly, Summary)
              VALUES (@typeId, @name, @kind, @returnType, @isStatic, @isPublic, @isReadOnly, @summary)",
            new SqliteParameter("@typeId", typeId),
            new SqliteParameter("@name", property.Name),
            new SqliteParameter("@kind", "Property"),
            new SqliteParameter("@returnType", property.PropertyType.FullName ?? property.PropertyType.Name),
            new SqliteParameter("@isStatic", (property.GetMethod?.IsStatic ?? property.SetMethod?.IsStatic) ?? false ? 1 : 0),
            new SqliteParameter("@isPublic", (property.GetMethod?.IsPublic ?? property.SetMethod?.IsPublic) ?? false ? 1 : 0),
            new SqliteParameter("@isReadOnly", !property.CanWrite ? 1 : 0),
            new SqliteParameter("@summary", (object?)ExtractDocComment(property) ?? DBNull.Value)
        );

        // Insert property accessor info
        _db.ExecuteInsert(
            @"INSERT INTO PropertyAccessors (MemberId, HasGetter, HasSetter, GetterVisibility, SetterVisibility)
              VALUES (@memberId, @hasGetter, @hasSetter, @getterVis, @setterVis)",
            new SqliteParameter("@memberId", memberId),
            new SqliteParameter("@hasGetter", property.CanRead ? 1 : 0),
            new SqliteParameter("@hasSetter", property.CanWrite ? 1 : 0),
            new SqliteParameter("@getterVis", property.GetMethod != null ? GetVisibility(property.GetMethod) : DBNull.Value),
            new SqliteParameter("@setterVis", property.SetMethod != null ? GetVisibility(property.SetMethod) : DBNull.Value)
        );
    }

    private void AnalyzeField(FieldInfo field, long typeId)
    {
        _db.ExecuteInsert(
            @"INSERT INTO Members (TypeId, Name, MemberKind, ReturnType, IsStatic, IsPublic, IsReadOnly, Summary)
              VALUES (@typeId, @name, @kind, @returnType, @isStatic, @isPublic, @isReadOnly, @summary)",
            new SqliteParameter("@typeId", typeId),
            new SqliteParameter("@name", field.Name),
            new SqliteParameter("@kind", "Field"),
            new SqliteParameter("@returnType", field.FieldType.FullName ?? field.FieldType.Name),
            new SqliteParameter("@isStatic", field.IsStatic ? 1 : 0),
            new SqliteParameter("@isPublic", field.IsPublic ? 1 : 0),
            new SqliteParameter("@isReadOnly", field.IsInitOnly || field.IsLiteral ? 1 : 0),
            new SqliteParameter("@summary", (object?)ExtractDocComment(field) ?? DBNull.Value)
        );
    }

    private void AnalyzeEvent(EventInfo evt, long typeId)
    {
        _db.ExecuteInsert(
            @"INSERT INTO Members (TypeId, Name, MemberKind, ReturnType, IsStatic, IsPublic, Summary)
              VALUES (@typeId, @name, @kind, @returnType, @isStatic, @isPublic, @summary)",
            new SqliteParameter("@typeId", typeId),
            new SqliteParameter("@name", evt.Name),
            new SqliteParameter("@kind", "Event"),
            new SqliteParameter("@returnType", evt.EventHandlerType?.FullName ?? evt.EventHandlerType?.Name ?? "EventHandler"),
            new SqliteParameter("@isStatic", (evt.AddMethod?.IsStatic ?? evt.RemoveMethod?.IsStatic) ?? false ? 1 : 0),
            new SqliteParameter("@isPublic", (evt.AddMethod?.IsPublic ?? evt.RemoveMethod?.IsPublic) ?? false ? 1 : 0),
            new SqliteParameter("@summary", (object?)ExtractDocComment(evt) ?? DBNull.Value)
        );
    }

    private void AnalyzeParameters(ParameterInfo[] parameters, long memberId)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            var param = parameters[i];

            // Check for params keyword using CustomAttributeData (MetadataLoadContext compatible)
            bool isParams = param.GetCustomAttributesData()
                .Any(a => a.AttributeType.Name == "ParamArrayAttribute");

            _db.ExecuteInsert(
                @"INSERT INTO Parameters (MemberId, Name, ParameterType, Position, IsOptional, DefaultValue, IsParams, IsOut, IsRef, IsIn)
                  VALUES (@memberId, @name, @type, @position, @isOptional, @defaultValue, @isParams, @isOut, @isRef, @isIn)",
                new SqliteParameter("@memberId", memberId),
                new SqliteParameter("@name", param.Name ?? $"param{i}"),
                new SqliteParameter("@type", param.ParameterType.FullName ?? param.ParameterType.Name),
                new SqliteParameter("@position", i),
                new SqliteParameter("@isOptional", param.IsOptional ? 1 : 0),
                new SqliteParameter("@defaultValue", param.HasDefaultValue ? param.DefaultValue?.ToString() ?? "null" : DBNull.Value),
                new SqliteParameter("@isParams", isParams ? 1 : 0),
                new SqliteParameter("@isOut", param.IsOut ? 1 : 0),
                new SqliteParameter("@isRef", param.ParameterType.IsByRef && !param.IsOut ? 1 : 0),
                new SqliteParameter("@isIn", param.IsIn ? 1 : 0)
            );
        }
    }

    private string GetVisibility(MethodInfo method)
    {
        if (method.IsPublic) return "Public";
        if (method.IsFamily) return "Protected";
        if (method.IsAssembly) return "Internal";
        if (method.IsFamilyOrAssembly) return "ProtectedInternal";
        if (method.IsPrivate) return "Private";
        return "Unknown";
    }

    private string? ExtractDocComment(MemberInfo member)
    {
        // XML documentation is not available through MetadataLoadContext
        // This would require parsing the XML documentation file separately
        // For now, return null - we can enhance this later
        return null;
    }
}
