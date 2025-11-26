# DLL Inspector MCP Server

**Version 2.0.0** - Now with Software Differentiation Support!

A Model Context Protocol (MCP) server that analyzes .NET assembly DLLs and stores metadata in a SQLite database for efficient querying. Originally designed for exploring Autodesk Civil 3D and AutoCAD APIs, now supports multiple software products in a single database.

## Overview

This MCP server provides a comprehensive database-driven solution for exploring .NET APIs. Instead of loading DLLs every time you query them, it analyzes assemblies once and stores all metadata (types, methods, properties, parameters, etc.) in a SQLite database for fast, powerful queries.

## Key Features

- **Software Differentiation** (NEW in v2.0): Organize DLLs by software product (Civil 3D, Revit, Dynamo, etc.)
- **Filtered Queries** (NEW in v2.0): Query only DLLs from specific software without searching the entire database
- **Automatic Migration**: Seamlessly upgrades from v1 to v2 schema
- **One-time analysis**: Analyze DLLs once and query the database repeatedly without reloading
- **Comprehensive metadata**: Captures types, methods, properties, fields, events, parameters, inheritance, interfaces, and more
- **Smart updates**: Hash-based change detection only re-analyzes when DLLs change
- **Powerful queries**: Search by name, namespace, return type, inheritance, interface implementations
- **Full-text search**: Find members by documentation and descriptions
- **Cross-platform**: Automatically handles Windows path to WSL path conversion

## MCP Tools

### Database Management

#### `analyze_dll`
Analyzes a .NET DLL and stores all metadata in the database.

**Parameters:**
- `dllPath` (required): Path to the DLL file (Windows or WSL format)
- `forceReAnalyze` (optional): Force re-analysis even if already in database (default: false)
- `softwareName` (optional, NEW in v2.0): Software name to associate with this DLL (e.g., "AutoCAD Civil 3D 2024")
- `softwareVersion` (optional, NEW in v2.0): Software version

**Example:**
```
dllPath: "C:\Program Files\Autodesk\AutoCAD 2024\C3D\AeccDbMgd.dll"
softwareName: "AutoCAD Civil 3D"
softwareVersion: "2024"
```

#### `list_assemblies`
Lists all assemblies currently in the database with statistics.

**Parameters:**
- `softwareName` (optional, NEW in v2.0): Filter assemblies by software name

**Returns:** Assembly names, versions, software association, analysis dates, namespace counts, and type counts

#### `list_software` (NEW in v2.0)
Lists all software products in the database with assembly counts.

**Returns:** Software names, versions, descriptions, and number of associated assemblies

#### `associate_assembly_with_software` (NEW in v2.0)
Associates an existing assembly with a software product.

**Parameters:**
- `assemblyName` (required): Assembly name
- `softwareName` (required): Software name

**Example:**
```
assemblyName: "AeccDbMgd"
softwareName: "AutoCAD Civil 3D"
```

### Type Discovery

#### `list_namespaces`
Lists all namespaces in the database.

**Parameters:**
- `assemblyName` (optional): Filter by specific assembly name

**Example:**
```
assemblyName: "AeccDbMgd"
```

#### `search_types`
Searches for types by name pattern. Supports SQL wildcards (%).

**Parameters:**
- `searchPattern` (required): Search pattern (e.g., "Alignment", "%Pipe%", "Surface%")
- `namespaceName` (optional): Filter by namespace
- `typeKind` (optional): Filter by type kind (Class, Interface, Enum, Struct)

**Examples:**
```
searchPattern: "%Alignment%"
namespaceName: "Autodesk.Civil.DatabaseServices"
```

```
searchPattern: "Pressure%"
typeKind: "Class"
```

#### `get_type_details`
Gets comprehensive information about a specific type including all members.

**Parameters:**
- `typeName` (required): Full type name

**Example:**
```
typeName: "Autodesk.Civil.DatabaseServices.Alignment"
```

**Returns:** Type information, base class, interfaces, constructors, properties, methods, fields, events with full signatures

### Method Discovery

#### `search_methods`
Searches for methods by name pattern across all types.

**Parameters:**
- `searchPattern` (required): Method name pattern (e.g., "%Station%", "Get%")
- `returnType` (optional): Filter by return type

**Examples:**
```
searchPattern: "%Station%"
```

```
searchPattern: "Get%"
returnType: "%Surface%"
```

### Advanced Queries

#### `search_documentation`
Full-text search across all type and member documentation.

**Parameters:**
- `searchTerms` (required): Search terms to find in documentation

**Example:**
```
searchTerms: "corridor surface"
```

#### `get_inheritance_tree`
Shows the inheritance hierarchy for a type (base classes and derived classes).

**Parameters:**
- `typeName` (required): Full type name

**Example:**
```
typeName: "Autodesk.Civil.DatabaseServices.Alignment"
```

#### `find_implementations`
Finds all types that implement a specific interface.

**Parameters:**
- `interfaceName` (required): Full interface name

**Example:**
```
interfaceName: "System.IDisposable"
```

## Installation

### Prerequisites

- .NET 9.0 SDK
- WSL2 (if running on Windows)

### Build

```bash
cd /mnt/c/Users/jjhaddad/Documents/Work/zeroTouch/dll-inspector-mcp
dotnet build
```

The built executable will be located at:
```
bin/Debug/net9.0/DllInspectorMcp.dll
```

### Configuration for Claude Code

Add this server to your Claude Code MCP settings file.

**Location of settings file:**
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`
- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
- Linux: `~/.config/Claude/claude_desktop_config.json`

**Configuration example:**

```json
{
  "mcpServers": {
    "civil3d-inspector": {
      "command": "dotnet",
      "args": [
        "exec",
        "/mnt/c/Users/jjhaddad/Documents/Work/zeroTouch/dll-inspector-mcp/bin/Debug/net9.0/DllInspectorMcp.dll"
      ]
    }
  }
}
```

**Note:** Adjust the path to match your installation location.

## Usage Workflow

### 1. Analyze DLLs (with Software Association)

First, analyze the assemblies you want to explore. **New in v2.0**: You can now associate DLLs with software products:

```
analyze_dll
  dllPath: "C:\Program Files\Autodesk\AutoCAD 2024\C3D\AeccDbMgd.dll"
  softwareName: "AutoCAD Civil 3D"
  softwareVersion: "2024"
```

You can analyze multiple software products in the same database:

```
analyze_dll
  dllPath: "C:\Program Files\Autodesk\Revit 2024\RevitAPI.dll"
  softwareName: "Revit"
  softwareVersion: "2024"
```

```
analyze_dll
  dllPath: "C:\Program Files\Dynamo\DynamoCore.dll"
  softwareName: "Dynamo"
  softwareVersion: "2.x"
```

The first analysis will take time, but subsequent queries will be instant!

**Tip:** Software association is optional. If you don't specify it, the DLL will still be analyzed and available for querying.

### 2. Explore the API

**New in v2.0**: View all software products and filter by software:
```
list_software
```

List assemblies from a specific software:
```
list_assemblies
  softwareName: "AutoCAD Civil 3D"
```

List available namespaces:
```
list_namespaces
  assemblyName: "AeccDbMgd"
```

Find types related to alignments:
```
search_types
  searchPattern: "%Alignment%"
  namespaceName: "Autodesk.Civil.DatabaseServices"
```

Get full details on a specific type:
```
get_type_details
  typeName: "Autodesk.Civil.DatabaseServices.Alignment"
```

Search for methods:
```
search_methods
  searchPattern: "%Station%"
```

Find inheritance relationships:
```
get_inheritance_tree
  typeName: "Autodesk.Civil.DatabaseServices.Corridor"
```

### 3. Updates

If you update Civil3D or the DLLs change, simply re-analyze:

```
analyze_dll
  dllPath: "C:\Program Files\Autodesk\AutoCAD 2024\C3D\AeccDbMgd.dll"
  forceReAnalyze: true
```

## Database Location

The SQLite database is created at:
```
bin/Debug/net9.0/civil3d_api.db
```

You can inspect or query it directly using any SQLite client.

## Database Schema

The database includes the following tables:

- **Software** (NEW in v2.0): Software product information
- **Assemblies**: DLL metadata and versions (now with optional SoftwareId)
- **Namespaces**: Namespace organization
- **Types**: Classes, interfaces, enums, structs
- **Members**: Methods, properties, fields, events, constructors
- **Parameters**: Method and constructor parameters
- **TypeInterfaces**: Interface implementations
- **EnumValues**: Enumeration values
- **PropertyAccessors**: Property getter/setter information
- **MembersSearch**: Full-text search index

See `DatabaseSchema.sql` for the complete v1 schema, or `DatabaseSchema_v2.sql` for v2 schema.
See `MIGRATION_V2.md` for migration details.

## Architecture

### Components

- **DatabaseManager.cs**: SQLite database operations and management
- **DllAnalyzer.cs**: Extracts metadata from .NET assemblies using MetadataLoadContext
- **DatabaseQueryTools.cs**: MCP tools for querying the database
- **DllLoader.cs**: Assembly loading with dependency resolution and path conversion
- **DatabaseSchema.sql**: Complete database schema with indexes and views

### Key Technologies

- **SQLite**: Lightweight, file-based database
- **MetadataLoadContext**: Reflection-only assembly loading
- **FTS5**: SQLite full-text search for documentation
- **Model Context Protocol**: Integration with Claude Code

## Path Handling

The server automatically converts Windows paths to WSL paths when running in WSL:
- `C:\path\to\file.dll` → `/mnt/c/path/to/file.dll`
- Works with all drive letters (C:, D:, etc.)

You can use either format when calling the tools.

## Performance

- **First analysis**: May take 30-60 seconds for large DLLs like AeccDbMgd.dll
- **Subsequent queries**: Milliseconds (database lookups)
- **Re-analysis**: Only when DLL file hash changes
- **Database size**: ~10-50 MB per analyzed assembly

## Troubleshooting

**Error: "Path is not an absolute path"**
- Ensure the DLL path is correct
- Try using the full WSL path format: `/mnt/c/...`

**Error: "Could not load file or assembly"**
- The DLL may have dependencies in the same directory
- The server attempts to load dependencies automatically from the DLL's directory

**Database not found or empty**
- The database is created automatically on first use
- Use `analyze_dll` to populate it with your DLLs

**Slow queries**
- The first query after starting the server may initialize the database
- Subsequent queries should be very fast
- Consider re-analyzing if you suspect data corruption

## Documentation

- **CHANGELOG.md**: Version history and changes
- **FEATURE_SUMMARY.md**: Summary of v2.0 software differentiation feature
- **MIGRATION_V2.md**: Database migration guide from v1 to v2
- **CIVIL3D_API_STRUCTURE.md**: Comprehensive documentation of the Civil3D API structure
- **PROJECT_STATUS.md**: Project development status and roadmap
- **DatabaseSchema.sql**: Complete v1 database schema with comments
- **DatabaseSchema_v2.sql**: Complete v2 database schema with Software table

## Dependencies

- .NET 9.0
- Microsoft.Data.Sqlite (9.0.0)
- Microsoft.Extensions.Hosting (10.0.0)
- ModelContextProtocol (0.4.0-preview.3)
- System.Reflection.MetadataLoadContext (9.0.0)

## Development

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run
```

### Clean Database

To start fresh, delete the database file:

```bash
rm bin/Debug/net9.0/civil3d_api.db
```

## References

- [Model Context Protocol Documentation](https://modelcontextprotocol.io/)
- [Build a Model Context Protocol (MCP) server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
- [MCP C# SDK GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [Civil 3D .NET API Reference](http://docs.autodesk.com/CIV3D/2019/ENU/API_Reference_Guide/index.html)
- [Autodesk Developer Documentation](https://help.autodesk.com/)

## License

This is a utility tool for inspecting .NET assemblies. Use at your own discretion.
