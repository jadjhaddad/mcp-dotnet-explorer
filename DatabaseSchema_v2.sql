-- Civil3D DLL Inspector Database Schema v2
-- Added support for software/product differentiation
-- This schema stores metadata extracted from .NET assemblies for efficient querying

-- Software/Products: Stores information about different software products
CREATE TABLE IF NOT EXISTS Software (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,             -- e.g., "AutoCAD Civil 3D", "Revit", "Dynamo"
    Version TEXT,                          -- Major version (e.g., "2024", "2025")
    Description TEXT,                      -- Optional description
    CreatedDate TEXT NOT NULL              -- ISO 8601 timestamp
);

-- Assemblies: Stores information about analyzed DLL files
CREATE TABLE IF NOT EXISTS Assemblies (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SoftwareId INTEGER,                    -- Optional: Link to software product
    Name TEXT NOT NULL,                    -- Assembly name (e.g., "AeccDbMgd")
    FullName TEXT NOT NULL UNIQUE,         -- Full assembly name with version
    Version TEXT NOT NULL,                 -- Version (e.g., "13.0.0.0")
    FilePath TEXT NOT NULL,                -- Full path to DLL file
    TargetFramework TEXT,                  -- Target framework (e.g., "net48")
    AnalyzedDate TEXT NOT NULL,            -- ISO 8601 timestamp of analysis
    FileHash TEXT,                         -- SHA256 hash for change detection
    FOREIGN KEY (SoftwareId) REFERENCES Software(Id) ON DELETE SET NULL,
    UNIQUE(Name, Version)
);

-- Create index for faster software-based assembly lookups
CREATE INDEX IF NOT EXISTS idx_assemblies_software ON Assemblies(SoftwareId);

-- Namespaces: Stores namespace information
CREATE TABLE IF NOT EXISTS Namespaces (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    AssemblyId INTEGER NOT NULL,
    Name TEXT NOT NULL,                    -- Full namespace (e.g., "Autodesk.Civil.DatabaseServices")
    FOREIGN KEY (AssemblyId) REFERENCES Assemblies(Id) ON DELETE CASCADE,
    UNIQUE(AssemblyId, Name)
);

-- Types: Stores class, interface, enum, and struct definitions
CREATE TABLE IF NOT EXISTS Types (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NamespaceId INTEGER NOT NULL,
    Name TEXT NOT NULL,                    -- Simple name (e.g., "Alignment")
    FullName TEXT NOT NULL,                -- Fully qualified name
    TypeKind TEXT NOT NULL,                -- 'Class', 'Interface', 'Enum', 'Struct', 'Delegate'
    IsAbstract INTEGER NOT NULL DEFAULT 0, -- Boolean (0 or 1)
    IsSealed INTEGER NOT NULL DEFAULT 0,   -- Boolean
    IsPublic INTEGER NOT NULL DEFAULT 1,   -- Boolean
    IsStatic INTEGER NOT NULL DEFAULT 0,   -- Boolean (for static classes)
    IsGeneric INTEGER NOT NULL DEFAULT 0,  -- Boolean (has generic type parameters)
    GenericParameters TEXT,                -- JSON array of generic parameter names
    BaseTypeId INTEGER,                    -- Reference to base type (NULL for Object)
    Summary TEXT,                          -- XML documentation summary
    Remarks TEXT,                          -- XML documentation remarks
    FOREIGN KEY (NamespaceId) REFERENCES Namespaces(Id) ON DELETE CASCADE,
    FOREIGN KEY (BaseTypeId) REFERENCES Types(Id) ON DELETE SET NULL,
    UNIQUE(NamespaceId, Name, GenericParameters)
);

-- Create index for faster type lookups
CREATE INDEX IF NOT EXISTS idx_types_fullname ON Types(FullName);
CREATE INDEX IF NOT EXISTS idx_types_name ON Types(Name);

-- Interfaces: Stores interface implementations
CREATE TABLE IF NOT EXISTS TypeInterfaces (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TypeId INTEGER NOT NULL,
    InterfaceTypeId INTEGER NOT NULL,      -- Reference to the interface type
    FOREIGN KEY (TypeId) REFERENCES Types(Id) ON DELETE CASCADE,
    FOREIGN KEY (InterfaceTypeId) REFERENCES Types(Id) ON DELETE CASCADE,
    UNIQUE(TypeId, InterfaceTypeId)
);

-- Members: Stores methods, properties, fields, events, constructors
CREATE TABLE IF NOT EXISTS Members (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TypeId INTEGER NOT NULL,
    Name TEXT NOT NULL,                    -- Member name
    MemberKind TEXT NOT NULL,              -- 'Method', 'Property', 'Field', 'Constructor', 'Event'
    ReturnType TEXT,                       -- Full type name of return value (NULL for constructors)
    IsStatic INTEGER NOT NULL DEFAULT 0,   -- Boolean
    IsVirtual INTEGER NOT NULL DEFAULT 0,  -- Boolean
    IsAbstract INTEGER NOT NULL DEFAULT 0, -- Boolean
    IsSealed INTEGER NOT NULL DEFAULT 0,   -- Boolean (for sealed overrides)
    IsOverride INTEGER NOT NULL DEFAULT 0, -- Boolean
    IsPublic INTEGER NOT NULL DEFAULT 1,   -- Boolean
    IsProtected INTEGER NOT NULL DEFAULT 0,-- Boolean
    IsInternal INTEGER NOT NULL DEFAULT 0, -- Boolean
    IsPrivate INTEGER NOT NULL DEFAULT 0,  -- Boolean
    IsReadOnly INTEGER NOT NULL DEFAULT 0, -- Boolean (for fields/properties)
    IsGeneric INTEGER NOT NULL DEFAULT 0,  -- Boolean (has generic type parameters)
    GenericParameters TEXT,                -- JSON array of generic parameter names
    Summary TEXT,                          -- XML documentation summary
    Remarks TEXT,                          -- XML documentation remarks
    Returns TEXT,                          -- XML documentation returns section
    Example TEXT,                          -- XML documentation example
    FOREIGN KEY (TypeId) REFERENCES Types(Id) ON DELETE CASCADE
);

-- Create indexes for faster member lookups
CREATE INDEX IF NOT EXISTS idx_members_typeid ON Members(TypeId);
CREATE INDEX IF NOT EXISTS idx_members_name ON Members(Name);
CREATE INDEX IF NOT EXISTS idx_members_kind ON Members(MemberKind);

-- Parameters: Stores method and constructor parameters
CREATE TABLE IF NOT EXISTS Parameters (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MemberId INTEGER NOT NULL,
    Name TEXT NOT NULL,                    -- Parameter name
    ParameterType TEXT NOT NULL,           -- Full type name
    Position INTEGER NOT NULL,             -- Zero-based position
    IsOptional INTEGER NOT NULL DEFAULT 0, -- Boolean
    DefaultValue TEXT,                     -- String representation of default value
    IsParams INTEGER NOT NULL DEFAULT 0,   -- Boolean (params keyword)
    IsOut INTEGER NOT NULL DEFAULT 0,      -- Boolean (out keyword)
    IsRef INTEGER NOT NULL DEFAULT 0,      -- Boolean (ref keyword)
    IsIn INTEGER NOT NULL DEFAULT 0,       -- Boolean (in keyword)
    Summary TEXT,                          -- XML documentation for parameter
    FOREIGN KEY (MemberId) REFERENCES Members(Id) ON DELETE CASCADE
);

-- Create index for faster parameter lookups
CREATE INDEX IF NOT EXISTS idx_parameters_memberid ON Parameters(MemberId);

-- Attributes: Stores attribute/annotation information
CREATE TABLE IF NOT EXISTS Attributes (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TargetKind TEXT NOT NULL,              -- 'Type', 'Member', 'Parameter', 'Assembly'
    TargetId INTEGER NOT NULL,             -- ID of the target (TypeId, MemberId, ParameterId, AssemblyId)
    AttributeType TEXT NOT NULL,           -- Full type name of attribute
    AttributeData TEXT                     -- JSON representation of attribute properties
);

-- Create index for faster attribute lookups
CREATE INDEX IF NOT EXISTS idx_attributes_target ON Attributes(TargetKind, TargetId);

-- EnumValues: Stores enum member values
CREATE TABLE IF NOT EXISTS EnumValues (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    TypeId INTEGER NOT NULL,               -- Reference to the enum type
    Name TEXT NOT NULL,                    -- Enum member name
    Value TEXT NOT NULL,                   -- String representation of the value
    Summary TEXT,                          -- XML documentation
    FOREIGN KEY (TypeId) REFERENCES Types(Id) ON DELETE CASCADE,
    UNIQUE(TypeId, Name)
);

-- PropertyAccessors: Stores get/set accessor information for properties
CREATE TABLE IF NOT EXISTS PropertyAccessors (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    MemberId INTEGER NOT NULL,             -- Reference to the property member
    HasGetter INTEGER NOT NULL DEFAULT 0,  -- Boolean
    HasSetter INTEGER NOT NULL DEFAULT 0,  -- Boolean
    GetterVisibility TEXT,                 -- 'Public', 'Protected', 'Internal', 'Private'
    SetterVisibility TEXT,                 -- 'Public', 'Protected', 'Internal', 'Private'
    FOREIGN KEY (MemberId) REFERENCES Members(Id) ON DELETE CASCADE,
    UNIQUE(MemberId)
);

-- Full-text search support for finding members by name/description
CREATE VIRTUAL TABLE IF NOT EXISTS MembersSearch USING fts5(
    MemberId UNINDEXED,
    TypeName,
    MemberName,
    Summary,
    content='Members',
    content_rowid='Id'
);

-- Trigger to keep FTS index updated
CREATE TRIGGER IF NOT EXISTS members_ai AFTER INSERT ON Members BEGIN
    INSERT INTO MembersSearch(MemberId, TypeName, MemberName, Summary)
    SELECT NEW.Id, t.Name, NEW.Name, NEW.Summary
    FROM Types t WHERE t.Id = NEW.TypeId;
END;

CREATE TRIGGER IF NOT EXISTS members_ad AFTER DELETE ON Members BEGIN
    DELETE FROM MembersSearch WHERE MemberId = OLD.Id;
END;

CREATE TRIGGER IF NOT EXISTS members_au AFTER UPDATE ON Members BEGIN
    DELETE FROM MembersSearch WHERE MemberId = OLD.Id;
    INSERT INTO MembersSearch(MemberId, TypeName, MemberName, Summary)
    SELECT NEW.Id, t.Name, NEW.Name, NEW.Summary
    FROM Types t WHERE t.Id = NEW.TypeId;
END;

-- Views for common queries

-- View: All types with their namespace, assembly, and software information
CREATE VIEW IF NOT EXISTS vw_TypesDetail AS
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

-- View: All members with their type and return type information
CREATE VIEW IF NOT EXISTS vw_MembersDetail AS
SELECT
    m.Id,
    m.Name AS MemberName,
    m.MemberKind,
    m.ReturnType,
    m.IsStatic,
    m.IsVirtual,
    m.IsAbstract,
    t.Name AS TypeName,
    t.FullName AS TypeFullName,
    n.Name AS Namespace,
    m.Summary
FROM Members m
JOIN Types t ON m.TypeId = t.Id
JOIN Namespaces n ON t.NamespaceId = n.Id;

-- View: Methods with their parameters
CREATE VIEW IF NOT EXISTS vw_MethodSignatures AS
SELECT
    m.Id AS MemberId,
    t.FullName AS TypeFullName,
    m.Name AS MethodName,
    m.ReturnType,
    m.IsStatic,
    GROUP_CONCAT(
        CASE
            WHEN p.IsOut = 1 THEN 'out ' || p.ParameterType || ' ' || p.Name
            WHEN p.IsRef = 1 THEN 'ref ' || p.ParameterType || ' ' || p.Name
            WHEN p.IsIn = 1 THEN 'in ' || p.ParameterType || ' ' || p.Name
            ELSE p.ParameterType || ' ' || p.Name
        END ||
        CASE
            WHEN p.IsOptional = 1 THEN ' = ' || COALESCE(p.DefaultValue, 'null')
            ELSE ''
        END,
        ', '
        ORDER BY p.Position
    ) AS Parameters
FROM Members m
JOIN Types t ON m.TypeId = t.Id
LEFT JOIN Parameters p ON m.Id = p.MemberId
WHERE m.MemberKind IN ('Method', 'Constructor')
GROUP BY m.Id, t.FullName, m.Name, m.ReturnType, m.IsStatic;
