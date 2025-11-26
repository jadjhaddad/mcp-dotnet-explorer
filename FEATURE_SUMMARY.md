# Software Differentiation Feature

## Overview
This feature adds the ability to differentiate DLLs by software product, allowing efficient querying of assemblies from specific applications (e.g., AutoCAD Civil 3D 2024, Revit 2025, Dynamo) without needing to query the entire database.

## Branch Information
- **Main Branch**: `main` - Contains the stable v1 schema
- **Feature Branch**: `feature/software-specific-dlls` - Contains the new software differentiation functionality

## Key Changes

### 1. Database Schema v2
- **New Table**: `Software` - Stores software product information
  - `Id`: Primary key
  - `Name`: Software name (e.g., "AutoCAD Civil 3D 2024")
  - `Version`: Version string
  - `Description`: Optional description
  - `CreatedDate`: Timestamp

- **Updated Table**: `Assemblies`
  - Added `SoftwareId` column (nullable foreign key to Software)
  - Added index `idx_assemblies_software` for performance

- **Updated View**: `vw_TypesDetail`
  - Now includes `Software` and `SoftwareVersion` columns

### 2. Automatic Migration
- Database automatically migrates from v1 to v2 on startup
- Migration is idempotent (safe to run multiple times)
- Backward compatible - existing databases work without changes

### 3. New MCP Tools

#### analyze_dll (Enhanced)
```
Parameters:
  - dllPath: Path to the DLL file
  - forceReAnalyze: Force re-analysis (default: false)
  - softwareName: Optional software name (e.g., "AutoCAD Civil 3D 2024") [NEW]
  - softwareVersion: Optional software version [NEW]
```

#### list_assemblies (Enhanced)
```
Parameters:
  - softwareName: Optional filter by software name [NEW]
```

#### list_software (New)
Lists all software products in the database with assembly counts.

#### associate_assembly_with_software (New)
Associates an existing assembly with a software product.
```
Parameters:
  - assemblyName: Assembly name
  - softwareName: Software name
```

## Usage Examples

### Analyzing DLLs with Software Association
```bash
# Analyze a Civil 3D DLL
analyze_dll(
  dllPath="/path/to/AeccDbMgd.dll",
  softwareName="AutoCAD Civil 3D",
  softwareVersion="2024"
)

# Analyze a Revit DLL
analyze_dll(
  dllPath="/path/to/RevitAPI.dll",
  softwareName="Revit",
  softwareVersion="2025"
)
```

### Querying by Software
```bash
# List only Civil 3D assemblies
list_assemblies(softwareName="AutoCAD Civil 3D")

# View all software products
list_software()
```

### Associating Existing Assemblies
```bash
# Associate an existing assembly with software
associate_assembly_with_software(
  assemblyName="AeccDbMgd",
  softwareName="AutoCAD Civil 3D"
)
```

## Benefits

1. **Targeted Queries**: Query only DLLs from specific software products
2. **Multi-Software Support**: Analyze multiple software products in the same database
3. **Performance**: Indexed queries for faster filtered results
4. **Organization**: Better organization of related assemblies
5. **Flexibility**: Software association is optional - works with or without it

## Migration Path

The migration is automatic, but here's what happens:

1. On first run after upgrade, the database checks for the Software table
2. If not found, it creates the Software table
3. Adds SoftwareId column to Assemblies (nullable)
4. Creates index for performance
5. Recreates vw_TypesDetail view with Software columns

**No data loss** - existing assemblies remain intact, just without software association.

## Files Added/Modified

### New Files
- `DatabaseSchema_v2.sql` - Complete v2 schema
- `MIGRATION_V2.md` - Detailed migration documentation
- `FEATURE_SUMMARY.md` - This file

### Modified Files
- `DatabaseManager.cs` - Added migration logic
- `DatabaseQueryTools.cs` - Added software parameters and new tools
- `DllAnalyzer.cs` - Added software ID parameter

## Testing

Build and run:
```bash
dotnet build DllInspectorMcp.csproj
dotnet run --project DllInspectorMcp.csproj
```

Both commands succeed with only nullable reference warnings (not errors).

## Next Steps

To merge this feature into main:
```bash
git checkout main
git merge feature/software-specific-dlls
```

To continue development:
```bash
# Stay on feature branch
git checkout feature/software-specific-dlls
```
