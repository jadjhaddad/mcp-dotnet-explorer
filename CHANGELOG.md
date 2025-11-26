# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2024-01-26

### Added
- **Software Differentiation**: New `Software` table to categorize DLLs by product
- Automatic database migration from v1 to v2 schema
- `softwareName` and `softwareVersion` parameters to `analyze_dll` tool
- `softwareName` filter parameter to `list_assemblies` tool
- New MCP tool: `list_software` - Lists all software products in database
- New MCP tool: `associate_assembly_with_software` - Links assemblies to software
- Database index on `Assemblies.SoftwareId` for improved query performance
- Software columns (`Software`, `SoftwareVersion`) to `vw_TypesDetail` view
- Comprehensive documentation: `FEATURE_SUMMARY.md` and `MIGRATION_V2.md`
- Version numbering to project file

### Changed
- Database schema upgraded to v2 with backward compatibility
- `Assemblies` table now includes optional `SoftwareId` foreign key
- `list_assemblies` output now shows associated software information

### Fixed
- Build error caused by multiple files with top-level statements
- Excluded `TestRunner.cs` and `TestDllLoader.cs` from compilation

## [1.0.0] - 2024-01-26

### Added
- Initial release of DLL Inspector MCP Server
- Core database schema with tables: Assemblies, Namespaces, Types, Members, Parameters, etc.
- MCP tools for DLL analysis and querying:
  - `analyze_dll` - Analyzes .NET DLLs and stores metadata
  - `list_assemblies` - Lists all assemblies in database
  - `list_namespaces` - Lists namespaces with optional filtering
  - `search_types` - Searches for types by pattern
  - `get_type_details` - Gets detailed type information including members
  - `search_methods` - Searches for methods by pattern
  - `search_documentation` - Full-text search across documentation
  - `get_inheritance_tree` - Shows inheritance hierarchy
  - `find_implementations` - Finds interface implementations
- Support for XML documentation extraction
- Full-text search support via SQLite FTS5
- Automatic change detection via file hashing
- MetadataLoadContext-based DLL loading for safe inspection
- Support for .NET Framework and .NET Core assemblies

[2.0.0]: https://github.com/yourrepo/dll-inspector-mcp/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/yourrepo/dll-inspector-mcp/releases/tag/v1.0.0
