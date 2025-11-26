#!/bin/bash

# Test script for the Civil3D DLL Inspector MCP Server

echo "=== Testing Civil3D DLL Inspector MCP Server ==="
echo

# Test 1: Analyze a DLL
echo "Test 1: Analyzing AeccDbMgd.dll (Civil3D 2024)..."
echo '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"analyze_dll","arguments":{"dllPath":"C:\\Program Files\\Autodesk\\AutoCAD 2024\\C3D\\AeccDbMgd.dll"}}}' | \
  dotnet run --project DllInspectorMcp.csproj 2>/dev/null

echo
echo "Test 1 complete!"
echo
echo "=== Press Enter to continue to Test 2 ==="
read

# Test 2: List assemblies
echo "Test 2: Listing all assemblies in database..."
echo '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"list_assemblies","arguments":{}}}' | \
  dotnet run --project DllInspectorMcp.csproj 2>/dev/null

echo
echo "Test 2 complete!"
echo
echo "=== Press Enter to continue to Test 3 ==="
read

# Test 3: List namespaces
echo "Test 3: Listing namespaces for AeccDbMgd..."
echo '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"list_namespaces","arguments":{"assemblyName":"AeccDbMgd"}}}' | \
  dotnet run --project DllInspectorMcp.csproj 2>/dev/null

echo
echo "Test 3 complete!"
echo
echo "=== Press Enter to continue to Test 4 ==="
read

# Test 4: Search for types
echo "Test 4: Searching for Alignment types..."
echo '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"search_types","arguments":{"searchPattern":"%Alignment%"}}}' | \
  dotnet run --project DllInspectorMcp.csproj 2>/dev/null

echo
echo "Test 4 complete!"
echo
echo "=== Press Enter to continue to Test 5 ==="
read

# Test 5: Get type details
echo "Test 5: Getting details for Alignment type..."
echo '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"get_type_details","arguments":{"typeName":"Autodesk.Civil.DatabaseServices.Alignment"}}}' | \
  dotnet run --project DllInspectorMcp.csproj 2>/dev/null

echo
echo "=== All tests complete! ==="
