# Database Schema Migration v1 to v2

## Overview
This migration adds software/product differentiation to allow querying DLLs by software (e.g., Civil 3D, Revit, Dynamo).

## Changes

### New Table: Software
- Stores information about different software products
- Columns:
  - `Id`: Primary key
  - `Name`: Unique software name (e.g., "AutoCAD Civil 3D 2024")
  - `Version`: Major version
  - `Description`: Optional description
  - `CreatedDate`: Timestamp

### Modified Table: Assemblies
- Added `SoftwareId` column (nullable foreign key to Software table)
- Added index `idx_assemblies_software` for performance

### Modified View: vw_TypesDetail
- Added `Software` and `SoftwareVersion` columns from joined Software table

## Benefits
1. **Filtered Queries**: Query only DLLs from specific software
2. **Multi-Software Support**: Analyze multiple products in same database
3. **Performance**: Index on SoftwareId speeds up filtered queries
4. **Organization**: Group related assemblies by product

## Migration Steps
1. Backup existing database
2. Add Software table
3. Add SoftwareId column to Assemblies (nullable, defaults to NULL)
4. Add index on Assemblies.SoftwareId
5. Recreate vw_TypesDetail view with Software columns
6. Optionally: Populate Software table and update existing assemblies

## SQL Migration Script
```sql
-- Step 1: Create Software table
CREATE TABLE IF NOT EXISTS Software (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Version TEXT,
    Description TEXT,
    CreatedDate TEXT NOT NULL
);

-- Step 2: Add SoftwareId to Assemblies
ALTER TABLE Assemblies ADD COLUMN SoftwareId INTEGER 
    REFERENCES Software(Id) ON DELETE SET NULL;

-- Step 3: Create index
CREATE INDEX IF NOT EXISTS idx_assemblies_software ON Assemblies(SoftwareId);

-- Step 4: Drop and recreate view
DROP VIEW IF EXISTS vw_TypesDetail;
CREATE VIEW vw_TypesDetail AS
SELECT
    t.Id,
    t.Name,
    t.FullName,
    t.TypeKind,
    t.IsAbstract,
    t.IsSealed,
    t.IsStatic,
    n.Name AS Namespace,
    a.Name AS Assembly,
    a.Version AS AssemblyVersion,
    s.Name AS Software,
    s.Version AS SoftwareVersion,
    bt.FullName AS BaseType,
    t.Summary
FROM Types t
JOIN Namespaces n ON t.NamespaceId = n.Id
JOIN Assemblies a ON n.AssemblyId = a.Id
LEFT JOIN Software s ON a.SoftwareId = s.Id
LEFT JOIN Types bt ON t.BaseTypeId = bt.Id;
```

## Usage Examples

### Create software entries
```sql
INSERT INTO Software (Name, Version, Description, CreatedDate)
VALUES ('AutoCAD Civil 3D', '2024', 'Civil engineering design software', datetime('now'));

INSERT INTO Software (Name, Version, Description, CreatedDate)
VALUES ('Revit', '2024', 'BIM software for architecture and MEP', datetime('now'));
```

### Update existing assemblies
```sql
UPDATE Assemblies 
SET SoftwareId = (SELECT Id FROM Software WHERE Name = 'AutoCAD Civil 3D' AND Version = '2024')
WHERE Name LIKE 'Aecc%' OR Name LIKE 'AcDb%' OR FilePath LIKE '%C3D%';
```

### Query types by software
```sql
SELECT * FROM vw_TypesDetail 
WHERE Software = 'AutoCAD Civil 3D' 
AND SoftwareVersion = '2024';
```
