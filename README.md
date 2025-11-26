# Civil3D DLL Inspector MCP Server

A Model Context Protocol (MCP) server that analyzes .NET assembly DLLs and stores metadata in a SQLite database for efficient querying. Designed specifically for exploring Autodesk Civil 3D and AutoCAD APIs.

## Overview

This MCP server provides a comprehensive database-driven solution for exploring .NET APIs. Instead of loading DLLs every time you query them, it analyzes assemblies once and stores all metadata (types, methods, properties, parameters, etc.) in a SQLite database for fast, powerful queries.

## Key Features

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

**Example:**
```
dllPath: "C:\Program Files\Autodesk\AutoCAD 2024\C3D\AeccDbMgd.dll"
```

#### `list_assemblies`
Lists all assemblies currently in the database with statistics.

**Returns:** Assembly names, versions, analysis dates, namespace counts, and type counts

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

### 1. Analyze Civil3D DLLs

First, analyze the Civil3D assemblies you want to explore:

```
analyze_dll
  dllPath: "C:\Program Files\Autodesk\AutoCAD 2024\C3D\AeccDbMgd.dll"
```

You can analyze multiple assemblies:

```
analyze_dll
  dllPath: "C:\Program Files\Autodesk\AutoCAD 2024\acdbmgd.dll"
```

```
analyze_dll
  dllPath: "C:\Program Files\Autodesk\AutoCAD 2024\acmgd.dll"
```

The first analysis will take time, but subsequent queries will be instant!

### 2. Explore the API

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

- **Assemblies**: DLL metadata and versions
- **Namespaces**: Namespace organization
- **Types**: Classes, interfaces, enums, structs
- **Members**: Methods, properties, fields, events, constructors
- **Parameters**: Method and constructor parameters
- **TypeInterfaces**: Interface implementations
- **EnumValues**: Enumeration values
- **PropertyAccessors**: Property getter/setter information
- **MembersSearch**: Full-text search index

See `DatabaseSchema.sql` for the complete schema definition.

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

- **CIVIL3D_API_STRUCTURE.md**: Comprehensive documentation of the Civil3D API structure
- **PROJECT_STATUS.md**: Project development status and roadmap
- **DatabaseSchema.sql**: Complete database schema with comments

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
