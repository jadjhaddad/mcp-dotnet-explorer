# Civil3D and AutoCAD .NET API Structure

This document provides a comprehensive overview of the Autodesk Civil 3D and AutoCAD .NET API structure based on research conducted in November 2025.

## Table of Contents
- [Core Assembly Structure](#core-assembly-structure)
- [Namespace Organization](#namespace-organization)
- [Object Model Hierarchy](#object-model-hierarchy)
- [Key Object Categories](#key-object-categories)
- [Database Schema Implications](#database-schema-implications)
- [References](#references)

---

## Core Assembly Structure

The Civil3D and AutoCAD .NET API is organized into several key managed assemblies (DLLs):

### Base Assemblies

| Assembly | Description | Purpose |
|----------|-------------|---------|
| **acdbmgd.dll** | AutoCAD Database Managed | Core AutoCAD database operations, entity management |
| **acmgd.dll** | AutoCAD Managed | Main AutoCAD application functionality |
| **accoremgd.dll** | AutoCAD Core Managed | Low-level AutoCAD core services |
| **AecBaseMgd.dll** | AEC Base Managed | Base classes for AEC (Architecture, Engineering, Construction) products |
| **AeccDbMgd.dll** | Civil 3D Database Managed | **Primary Civil 3D API** - Contains all Civil 3D-specific classes |

### Assembly Location
These assemblies are typically located in:
```
C:\Program Files\Autodesk\AutoCAD 2024\C3D\
```

### Project Setup Notes
- Set **Copy Local** to `False` in Visual Studio for these assemblies
- This reduces project size and allows debugging against the installed version
- Assemblies are loaded from the Civil 3D installation directory at runtime

---

## Namespace Organization

### Historical Context: 2013 Namespace Simplification

Prior to Civil 3D 2013, the API had complex nested namespaces organized by feature domains (e.g., `Autodesk.Civil.Land`, `Autodesk.Civil.PipeNetwork`, `Autodesk.Civil.Roadway`).

**Civil 3D 2013** introduced a major namespace restructuring that:
- Removed feature domain levels from namespaces
- Simplified object references
- Grouped related classes together more logically
- Reduced the total number of namespaces

### Current Namespace Structure (2013+)

#### Primary Namespaces in AeccDbMgd.dll

```
Autodesk.Civil
├── Autodesk.Civil.ApplicationServices
│   ├── CivilApplication
│   ├── CivilDocument
│   └── Application-level services
│
├── Autodesk.Civil.DatabaseServices
│   ├── Surface
│   ├── Alignment
│   ├── Profile
│   ├── Corridor
│   ├── PipeNetwork
│   ├── PressurePipeNetwork
│   ├── PointGroup
│   ├── Site
│   ├── Parcel
│   ├── SampleLine
│   ├── Section
│   ├── Bridge (2022+)
│   └── [All Civil 3D database objects]
│
├── Autodesk.Civil.DatabaseServices.Styles
│   ├── AlignmentStyle
│   ├── SurfaceStyle
│   ├── ProfileStyle
│   └── [All style objects]
│
└── Autodesk.Civil.Settings
    ├── SettingsAlignment
    ├── SettingsSurface
    └── [Feature and command settings]
```

#### AutoCAD Base Namespaces (from acdbmgd.dll, acmgd.dll, accoremgd.dll)

```
Autodesk.AutoCAD.ApplicationServices
Autodesk.AutoCAD.DatabaseServices
Autodesk.AutoCAD.Geometry
Autodesk.AutoCAD.Runtime
Autodesk.AutoCAD.EditorInput
Autodesk.AutoCAD.Windows
```

---

## Object Model Hierarchy

### Root Application Objects

```
AeccApplication (Root)
│   ├── Inherits from: AcadApplication (AutoCAD)
│   ├── Purpose: Main application window, base AutoCAD objects
│   └── Contains: Collection of all open documents
│
└── CivilDocument (per open drawing)
    ├── Location: Autodesk.Civil.ApplicationServices
    ├── Purpose: Represents an open Civil 3D drawing
    └── Provides access to feature collections
```

### CivilDocument Feature Collections

The `CivilDocument` object provides access to collections of Civil 3D objects:

```csharp
CivilDocument
├── Surfaces
│   └── SurfaceCollection
├── Alignments
│   └── AlignmentCollection
├── Profiles
│   └── ProfileCollection
├── Corridors
│   └── CorridorCollection
├── PipeNetworks
│   └── PipeNetworkCollection
├── PressureNetworks
│   └── PressureNetworkCollection
├── Sites
│   └── SiteCollection
├── Parcels (via Sites)
│   └── ParcelCollection
├── PointGroups
│   └── PointGroupCollection
├── SampleLineGroups
│   └── SampleLineGroupCollection
├── ViewFrameGroups
│   └── ViewFrameGroupCollection
└── [Other feature collections]
```

### Typical Object Pattern

Most Civil 3D features follow this pattern:

```
[Feature]Collection
├── Contains: Multiple [Feature] objects
├── Methods: Add(), Remove(), Contains()
└── Indexers: By name, ObjectId, index

[Feature] (e.g., Alignment, Surface, Profile)
├── Inherits from: Entity or Object
├── Properties: Name, ObjectId, StyleId, etc.
├── Methods: Feature-specific operations
├── Styles: Reference to [Feature]Style
└── Settings: Reference to Settings[Feature]

[Feature]Style
├── Controls: Display and behavior
├── Properties: Colors, layers, display components
└── Tabs: Display, Design, Information

Settings[Feature]
├── Controls: Default values and behaviors
├── Organized by: Command and object settings
└── Ambient settings: Drawing-wide defaults
```

---

## Key Object Categories

### 1. Surfaces
**Namespace:** `Autodesk.Civil.DatabaseServices`

Terrain modeling and analysis objects.

**Key Classes:**
- `Surface` - TIN surface, grid surface, volume surface
- `SurfaceDefinition` - Definition data (breaklines, boundaries, DEM files)
- `SurfaceAnalysis` - Analysis data (elevations, slopes, watersheds)
- `SurfaceStyle` - Display styling
- `GridSurface`, `TinSurface`, `VolumeSurface` - Surface types

**Common Operations:**
- Create from points, DEM files, or other surfaces
- Add breaklines and boundaries
- Perform analysis (elevation, slope, watershed)
- Extract contours
- Volume calculations

### 2. Alignments
**Namespace:** `Autodesk.Civil.DatabaseServices`

Horizontal geometry for linear design.

**Key Classes:**
- `Alignment` - Horizontal alignment entity
- `AlignmentEntity` - Base class for alignment sub-entities
- `AlignmentLine`, `AlignmentArc`, `AlignmentSpiral` - Geometry elements
- `AlignmentStyle` - Display styling
- `StationEquation` - Station equations

**Common Operations:**
- Create from polylines or entities
- Add/edit geometry (lines, curves, spirals)
- Station/offset calculations
- Station equations
- Superelevation

### 3. Profiles
**Namespace:** `Autodesk.Civil.DatabaseServices`

Vertical geometry for linear design.

**Key Classes:**
- `Profile` - Vertical alignment
- `ProfileView` - Visual representation
- `ProfileEntity` - Base for profile sub-entities
- `ProfileTangent`, `ProfileCircular`, `ProfileParabola` - Vertical curves
- `ProfileStyle` - Display styling

**Common Operations:**
- Create surface profiles
- Create design profiles
- Sample surfaces
- Grade calculations
- Vertical curve design

### 4. Corridors
**Namespace:** `Autodesk.Civil.DatabaseServices`

3D roadway and linear feature modeling.

**Key Classes:**
- `Corridor` - Main corridor object
- `CorridorSurface` - Extracted corridor surface
- `Assembly` - Cross-section template
- `Subassembly` - Assembly component
- `AppliedAssembly`, `AppliedSubassembly` - Instantiated assemblies
- `FeatureLine` - Linear features extracted from corridor
- `CalculatedLink`, `CalculatedPoint` - Corridor calculation results

**Common Operations:**
- Create from alignment, profile, and assembly
- Define regions and frequencies
- Extract surfaces
- Create feature lines
- Corridor sectioning

### 5. Pipe Networks
**Namespace:** `Autodesk.Civil.DatabaseServices`

Gravity-flow utility design (storm, sanitary).

**Key Classes:**
- `Network` - Pipe network container
- `Pipe` - Gravity pipe
- `Structure` - Manhole, inlet, outlet
- `PipeNetworkStyle` - Display styling
- `PartFamily`, `PartSize` - Catalog parts

**Common Operations:**
- Create networks from layout
- Add pipes and structures
- Interference checking
- Hydraulic analysis integration
- Profile views of networks

### 6. Pressure Networks
**Namespace:** `Autodesk.Civil.DatabaseServices`

Pressure utility design (water, gas).

**Key Classes:**
- `PressureNetwork` - Pressure network container
- `PressurePipe` - Pressure pipe
- `PressureFitting` - Fittings (bends, tees, etc.)
- `PressureAppurtenance` - Valves, hydrants, etc.
- `PressurePart` - Base class for pressure parts

**Common Operations:**
- Create pressure networks
- Add pipes, fittings, appurtenances
- Network analysis
- Profile generation

### 7. Points
**Namespace:** `Autodesk.Civil.DatabaseServices`

Survey and COGO points.

**Key Classes:**
- `CogoPoint` - Survey point with description and attributes
- `PointGroup` - Collection of points with query filters
- `PointGroupCollection` - All point groups in drawing
- `PointStyle` - Display styling

**Common Operations:**
- Import point files
- Create point groups with queries
- Point transformations
- Point tables and reports

### 8. Sites and Parcels
**Namespace:** `Autodesk.Civil.DatabaseServices`

Land development and parcel management.

**Key Classes:**
- `Site` - Container for parcels and related objects
- `Parcel` - Land parcel
- `ParcelSegment` - Parcel boundary segment
- `ParcelStyle` - Display styling

**Common Operations:**
- Create parcels from objects
- Parcel subdivision
- Area calculations
- Legal descriptions

### 9. Sample Lines and Sections
**Namespace:** `Autodesk.Civil.DatabaseServices`

Cross-sectional analysis and visualization.

**Key Classes:**
- `SampleLineGroup` - Collection of sample lines
- `SampleLine` - Individual cross-section location
- `SectionView` - Visual representation of section
- `Section` - Cross-section data

**Common Operations:**
- Create sample lines along alignment
- Generate section views
- Volume calculations
- Material tables

### 10. Bridges (2022+)
**Namespace:** `Autodesk.Civil.DatabaseServices`

Bridge design and documentation (added in recent versions).

**Key Classes:**
- `Bridge` - Main bridge object
- `BridgePier` - Bridge pier
- `BridgeStructure` - Base class for bridge structures
- `BridgeAbutment` - Abutment structure
- `Deck` - Bridge deck

**Common Operations:**
- Create bridge geometry
- Place piers and abutments
- Generate bridge cross-sections

---

## Database Schema Implications

Based on this API structure, an optimal SQLite database schema should capture:

### 1. Assemblies Table
- Assembly name (e.g., "AeccDbMgd")
- Version
- File path
- Target framework

### 2. Namespaces Table
- Namespace name (e.g., "Autodesk.Civil.DatabaseServices")
- Assembly reference
- Description

### 3. Types Table
- Full type name
- Namespace reference
- Type kind (class, interface, enum, struct)
- Is abstract
- Is sealed
- Base type reference
- Summary documentation

### 4. Members Table
- Member name
- Type reference (parent class)
- Member kind (method, property, field, constructor, event)
- Return type (for methods/properties)
- Is static
- Is virtual
- Is abstract
- Visibility (public, protected, internal)
- Summary documentation

### 5. Parameters Table
- Parameter name
- Member reference (parent method/constructor)
- Parameter type
- Position
- Is optional
- Default value
- Documentation

### 6. Inheritance Table
- Derived type reference
- Base type reference
- Relationship type (inherits, implements)

### 7. Attributes Table
- Member/Type reference
- Attribute type
- Attribute data

### Query Capabilities

This schema enables queries like:
- "Show all classes in Autodesk.Civil.DatabaseServices namespace"
- "Find all methods that return Surface objects"
- "What classes inherit from Entity?"
- "List all properties on the Alignment class"
- "Find all methods with 'Station' in the name"
- "Show constructor signatures for PipeNetwork"
- "What are the style classes for corridors?"

---

## References

### Official Documentation
- [Setting up a .NET Project for Autodesk Civil 3D](https://help.autodesk.com/cloudhelp/2024/ENU/Civil3D-DevGuide/files/GUID-DE3A46DA-508E-43A0-8538-C77D978D06B2.htm)
- [Autodesk.Civil.DatabaseServices Namespace](https://help.autodesk.com/view/CIV3D/2025/ENU/?guid=73fd1950-ee31-00b8-4872-c3f328ea1331)
- [Civil 3D .NET API Reference Guide](http://docs.autodesk.com/CIV3D/2019/ENU/API_Reference_Guide/index.html)
- [AutoCAD .NET API Overview](https://help.autodesk.com/view/OARX/2024/ENU/?guid=GUID-390A47DB-77AF-433A-994C-2AFBBE9996AE)

### Developer Blogs and Resources
- [.NET API namespace simplification in AutoCAD Civil 3D 2013](https://adndevblog.typepad.com/infrastructure/2012/04/net-api-namespace-simplification-in-autocad-civil-3d-2013.html)
- [Through the Interface: Civil 3D](https://www.keanw.com/civil_3d/)
- [AutoCAD DevBlog](https://adndevblog.typepad.com/autocad/)

### Community Resources
- [Civil 3D Customization Forum](https://forums.autodesk.com/t5/civil-3d-customization-forum/bd-p/160)
- [Civilized Development Blog](https://civilizeddevelopment.typepad.com/)

---

## Document History

- **2025-11-25**: Initial documentation created based on API research for Civil 3D 2024/2025
- **Research Date**: November 25, 2025
- **API Versions Covered**: Civil 3D 2013-2025, AutoCAD 2024+
