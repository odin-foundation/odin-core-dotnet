using System;
using System.Collections.Generic;
using System.Linq;
using Odin.Core.Types;

namespace Odin.Core.Resolver
{
    /// <summary>How to handle type name conflicts when merging imports.</summary>
    public enum ConflictResolution
    {
        /// <summary>Prefix imported types with their namespace (default).</summary>
        Namespace,

        /// <summary>Later definitions overwrite earlier ones.</summary>
        Overwrite,

        /// <summary>Return an error on conflict.</summary>
        Error,
    }

    /// <summary>Options for flattening a schema.</summary>
    public sealed class FlattenerOptions
    {
        /// <summary>How to handle type name conflicts (default: Namespace).</summary>
        public ConflictResolution ConflictResolution { get; set; } = ConflictResolution.Namespace;

        /// <summary>Whether to tree-shake unused types (default: true).</summary>
        public bool TreeShake { get; set; } = true;

        /// <summary>Whether to inline type references (default: false).</summary>
        public bool InlineTypeReferences { get; set; }

        /// <summary>Custom metadata to override the primary schema's metadata.</summary>
        public SchemaMetadata? Metadata { get; set; }

        /// <summary>Creates default flattener options.</summary>
        public static FlattenerOptions Default() => new FlattenerOptions();
    }

    /// <summary>Result of flattening a schema.</summary>
    public sealed class FlattenedResult
    {
        /// <summary>The flattened schema with all imports merged and no import directives.</summary>
        public OdinSchemaDefinition Schema { get; }

        /// <summary>All source files that were merged.</summary>
        public List<string> SourceFiles { get; }

        /// <summary>Warnings generated during flattening.</summary>
        public List<string> Warnings { get; }

        /// <summary>Creates a flattened result.</summary>
        public FlattenedResult(OdinSchemaDefinition schema, List<string> sourceFiles, List<string> warnings)
        {
            Schema = schema;
            SourceFiles = sourceFiles;
            Warnings = warnings;
        }
    }

    /// <summary>
    /// Flattens ODIN schemas by resolving and merging all imports into a single
    /// schema with no dependencies. Supports namespace conflict resolution,
    /// type inheritance expansion, and tree-shaking of unused types.
    /// </summary>
    public sealed class SchemaFlattener
    {
        private readonly FlattenerOptions _options;
        private readonly List<string> _warnings = new List<string>();

        /// <summary>Maps type names to their source namespace (for reference rewriting). Null value means local.</summary>
        private readonly Dictionary<string, string?> _typeSourceMap = new Dictionary<string, string?>();

        /// <summary>Set of referenced type names (qualified) for tree shaking.</summary>
        private readonly HashSet<string> _referencedTypes = new HashSet<string>();

        /// <summary>Creates a new schema flattener with the given options.</summary>
        /// <param name="options">Flattener options, or null for defaults.</param>
        public SchemaFlattener(FlattenerOptions? options = null)
        {
            _options = options ?? FlattenerOptions.Default();
        }

        /// <summary>
        /// Flatten an already-resolved schema, merging all imports into a single
        /// schema definition with no import directives.
        /// </summary>
        /// <param name="resolved">The resolved schema from the import resolver.</param>
        /// <returns>The flattened result containing the merged schema, source files, and warnings.</returns>
        public FlattenedResult Flatten(ResolvedSchema resolved)
        {
            _warnings.Clear();
            _typeSourceMap.Clear();
            _referencedTypes.Clear();

            // 1. Build type source map
            BuildTypeSourceMap(resolved);

            // 2. Merge all types from imports
            var mergedTypes = MergeTypes(resolved);

            // 3. Expand type inheritance (composition / parents)
            mergedTypes = ExpandTypeInheritance(mergedTypes);

            // 4. Merge fields, arrays, constraints
            var mergedFields = MergeFields(resolved);
            var mergedArrays = MergeArrays(resolved);
            var mergedConstraints = MergeConstraints(resolved);

            // 5. Tree shake if enabled
            if (_options.TreeShake)
            {
                CollectReferencedTypes(resolved, mergedTypes, mergedFields, mergedArrays);

                int originalCount = mergedTypes.Count;
                mergedTypes = FilterReferencedTypes(mergedTypes);
                int removed = originalCount - mergedTypes.Count;
                if (removed > 0)
                {
                    _warnings.Add($"Tree shaking removed {removed} unused types");
                }

                mergedFields = FilterReferencedFields(mergedFields);
                mergedArrays = FilterReferencedArrays(mergedArrays);
                mergedConstraints = FilterReferencedConstraints(mergedConstraints);
            }

            // 6. Build flattened schema
            var metadata = _options.Metadata ?? resolved.Schema.Metadata;

            var schema = new OdinSchemaDefinition
            {
                Metadata = metadata,
                Imports = new List<SchemaImport>(), // No imports in flattened schema
                Types = mergedTypes,
                Fields = mergedFields,
                Arrays = mergedArrays,
                Constraints = mergedConstraints,
            };

            var warnings = new List<string>(_warnings);
            return new FlattenedResult(schema, new List<string>(resolved.ResolvedPaths), warnings);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Qualified Naming
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Build a qualified name for a type, avoiding duplication when namespace
        /// overlaps with the type name.
        /// </summary>
        internal static string BuildQualifiedName(string typeName, string? ns)
        {
            if (ns == null) return typeName;

            // Avoid duplication: namespace == type name
            if (ns == typeName) return typeName;

            // Avoid duplication: type name starts with "namespace."
            if (typeName.StartsWith(ns + ".", StringComparison.Ordinal)) return typeName;

            // Avoid duplication: type name starts with "namespace_"
            if (typeName.StartsWith(ns + "_", StringComparison.Ordinal)) return typeName;

            return $"{ns}_{typeName}";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Type Source Map
        // ─────────────────────────────────────────────────────────────────────

        private void BuildTypeSourceMap(ResolvedSchema resolved)
        {
            // Add types from imports with their namespace
            foreach (var imp in resolved.Imports)
            {
                if (imp.Schema != null)
                {
                    foreach (string typeName in imp.Schema.Types.Keys)
                    {
                        _typeSourceMap[typeName] = imp.Alias;
                    }
                }
            }

            // Add types from primary schema (no namespace) -- overrides imports
            foreach (string typeName in resolved.Schema.Types.Keys)
            {
                _typeSourceMap[typeName] = null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Merge Types
        // ─────────────────────────────────────────────────────────────────────

        private Dictionary<string, SchemaType> MergeTypes(ResolvedSchema resolved)
        {
            var merged = new Dictionary<string, SchemaType>();

            // First, add all types from imports
            foreach (var imp in resolved.Imports)
            {
                if (imp.Schema != null)
                {
                    foreach (var kvp in imp.Schema.Types)
                    {
                        AddType(merged, kvp.Key, kvp.Value, imp.Alias);
                    }
                }
            }

            // Then add types from primary schema (may override imports)
            foreach (var kvp in resolved.Schema.Types)
            {
                AddType(merged, kvp.Key, kvp.Value, null);
            }

            return merged;
        }

        private void AddType(
            Dictionary<string, SchemaType> merged,
            string typeName,
            SchemaType schemaType,
            string? ns)
        {
            string qualifiedName = BuildQualifiedName(typeName, ns);

            // Conflict handling
            if (merged.ContainsKey(qualifiedName))
            {
                switch (_options.ConflictResolution)
                {
                    case ConflictResolution.Error:
                        _warnings.Add($"Type name conflict: {qualifiedName}");
                        return;
                    case ConflictResolution.Overwrite:
                        _warnings.Add($"Type '{qualifiedName}' overwritten");
                        break;
                    case ConflictResolution.Namespace:
                        if (ns != null)
                        {
                            _warnings.Add(
                                $"Type '{qualifiedName}' from namespace '{ns}' conflicts with existing type");
                        }
                        break;
                }
            }

            // Rewrite type references in fields
            var updatedFields = new List<SchemaField>();
            foreach (var field in schemaType.SchemaFields)
            {
                updatedFields.Add(UpdateFieldReferences(field));
            }

            string finalName = _options.ConflictResolution == ConflictResolution.Namespace
                ? qualifiedName
                : typeName;

            merged[qualifiedName] = new SchemaType
            {
                Name = finalName,
                Description = schemaType.Description,
                SchemaFields = updatedFields,
                Parents = new List<string>(schemaType.Parents),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Type Inheritance Expansion
        // ─────────────────────────────────────────────────────────────────────

        private Dictionary<string, SchemaType> ExpandTypeInheritance(
            Dictionary<string, SchemaType> types)
        {
            var expanded = new Dictionary<string, SchemaType>();
            var visited = new HashSet<string>();

            foreach (var kvp in types)
            {
                var expandedType = ExpandSingleType(kvp.Key, kvp.Value, types, visited);
                expanded[kvp.Key] = expandedType;
            }

            return expanded;
        }

        private SchemaType ExpandSingleType(
            string typeName,
            SchemaType schemaType,
            Dictionary<string, SchemaType> allTypes,
            HashSet<string> visited)
        {
            // No parents means no inheritance
            if (schemaType.Parents == null || schemaType.Parents.Count == 0)
                return CloneSchemaType(schemaType);

            // Circular check
            if (visited.Contains(typeName))
            {
                _warnings.Add($"Circular type inheritance detected for '{typeName}'");
                return CloneSchemaType(schemaType);
            }
            visited.Add(typeName);

            // Collect fields from all parent types
            var mergedFields = new List<SchemaField>();
            var mergedFieldNames = new HashSet<string>();

            foreach (string parentName in schemaType.Parents)
            {
                string qualifiedParent = ResolveTypeName(parentName);
                if (allTypes.TryGetValue(qualifiedParent, out var parentType))
                {
                    var expandedParent = ExpandSingleType(qualifiedParent, parentType, allTypes, visited);
                    foreach (var field in expandedParent.SchemaFields)
                    {
                        if (!mergedFieldNames.Contains(field.Name))
                        {
                            mergedFieldNames.Add(field.Name);
                            mergedFields.Add(CloneSchemaField(field));
                        }
                    }
                }
            }

            // Add/override with local fields
            foreach (var field in schemaType.SchemaFields)
            {
                if (mergedFieldNames.Contains(field.Name))
                {
                    _warnings.Add($"Field '{field.Name}' in type '{typeName}' overrides base type field");
                    mergedFields.RemoveAll(f => f.Name == field.Name);
                }
                mergedFieldNames.Add(field.Name);
                mergedFields.Add(CloneSchemaField(field));
            }

            visited.Remove(typeName);

            return new SchemaType
            {
                Name = schemaType.Name,
                Description = schemaType.Description,
                SchemaFields = mergedFields,
                Parents = new List<string>(schemaType.Parents),
            };
        }

        private string ResolveTypeName(string typeName)
        {
            if (typeName.Contains('.'))
            {
                // Already namespaced: convert dots to underscores
                int lastDot = typeName.LastIndexOf('.');
                string ns = typeName.Substring(0, lastDot).Replace('.', '_');
                string name = typeName.Substring(lastDot + 1);
                return BuildQualifiedName(name, ns);
            }

            // Simple name: look up in type source map
            if (_typeSourceMap.TryGetValue(typeName, out string? sourceNs))
            {
                return BuildQualifiedName(typeName, sourceNs);
            }

            return typeName;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Reference Rewriting
        // ─────────────────────────────────────────────────────────────────────

        private SchemaField UpdateFieldReferences(SchemaField field)
        {
            var updated = CloneSchemaField(field);

            if (field.FieldType is ReferenceFieldType refType)
            {
                updated.FieldType = SchemaFieldType.Reference(RewriteTypeReference(refType.Target));
            }
            else if (field.FieldType is TypeRefFieldType typeRefType)
            {
                updated.FieldType = SchemaFieldType.TypeRef(RewriteTypeReference(typeRefType.Name));
            }

            return updated;
        }

        private string RewriteTypeReference(string name)
        {
            if (name.Contains('.'))
            {
                // Namespaced reference: "types.address" -> "types_address"
                int lastDot = name.LastIndexOf('.');
                string ns = name.Substring(0, lastDot).Replace('.', '_');
                string typePart = name.Substring(lastDot + 1);
                return BuildQualifiedName(typePart, ns);
            }

            if (_options.ConflictResolution == ConflictResolution.Namespace)
            {
                // Simple reference: look up where the type is actually defined
                if (_typeSourceMap.TryGetValue(name, out string? ns) && ns != null)
                {
                    return BuildQualifiedName(name, ns);
                }
            }

            // Not found in map, leave as-is (might be a local type)
            return name;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Merge Fields / Arrays / Constraints
        // ─────────────────────────────────────────────────────────────────────

        private Dictionary<string, SchemaField> MergeFields(ResolvedSchema resolved)
        {
            var merged = new Dictionary<string, SchemaField>();

            // Add fields from imports
            foreach (var imp in resolved.Imports)
            {
                if (imp.Schema != null)
                {
                    foreach (var kvp in imp.Schema.Fields)
                    {
                        string qualifiedPath = kvp.Key;
                        if (_options.ConflictResolution == ConflictResolution.Namespace && imp.Alias != null)
                        {
                            qualifiedPath = $"{imp.Alias}_{kvp.Key}";
                        }

                        merged[qualifiedPath] = UpdateFieldReferences(kvp.Value);
                    }
                }
            }

            // Add fields from primary schema (overrides imports)
            foreach (var kvp in resolved.Schema.Fields)
            {
                merged[kvp.Key] = UpdateFieldReferences(kvp.Value);
            }

            return merged;
        }

        private Dictionary<string, SchemaArray> MergeArrays(ResolvedSchema resolved)
        {
            var merged = new Dictionary<string, SchemaArray>();

            foreach (var imp in resolved.Imports)
            {
                if (imp.Schema != null)
                {
                    foreach (var kvp in imp.Schema.Arrays)
                    {
                        string qualifiedPath = kvp.Key;
                        if (_options.ConflictResolution == ConflictResolution.Namespace && imp.Alias != null)
                        {
                            qualifiedPath = $"{imp.Alias}_{kvp.Key}";
                        }

                        merged[qualifiedPath] = new SchemaArray
                        {
                            Name = qualifiedPath,
                            ItemType = kvp.Value.ItemType,
                            MinItems = kvp.Value.MinItems,
                            MaxItems = kvp.Value.MaxItems,
                            IsUnique = kvp.Value.IsUnique,
                        };
                    }
                }
            }

            foreach (var kvp in resolved.Schema.Arrays)
            {
                merged[kvp.Key] = kvp.Value;
            }

            return merged;
        }

        private Dictionary<string, List<SchemaObjectConstraint>> MergeConstraints(ResolvedSchema resolved)
        {
            var merged = new Dictionary<string, List<SchemaObjectConstraint>>();

            foreach (var imp in resolved.Imports)
            {
                if (imp.Schema != null)
                {
                    foreach (var kvp in imp.Schema.Constraints)
                    {
                        string qualifiedPath = kvp.Key;
                        if (_options.ConflictResolution == ConflictResolution.Namespace && imp.Alias != null)
                        {
                            qualifiedPath = $"{imp.Alias}_{kvp.Key}";
                        }

                        if (!merged.TryGetValue(qualifiedPath, out var list))
                        {
                            list = new List<SchemaObjectConstraint>();
                            merged[qualifiedPath] = list;
                        }
                        list.AddRange(kvp.Value);
                    }
                }
            }

            foreach (var kvp in resolved.Schema.Constraints)
            {
                if (!merged.TryGetValue(kvp.Key, out var list))
                {
                    list = new List<SchemaObjectConstraint>();
                    merged[kvp.Key] = list;
                }
                list.AddRange(kvp.Value);
            }

            return merged;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tree Shaking
        // ─────────────────────────────────────────────────────────────────────

        private void CollectReferencedTypes(
            ResolvedSchema resolved,
            Dictionary<string, SchemaType> allTypes,
            Dictionary<string, SchemaField> mergedFields,
            Dictionary<string, SchemaArray> mergedArrays)
        {
            var processedTypePaths = new HashSet<string>();

            // Start with all types defined in the primary schema
            foreach (string typeName in resolved.Schema.Types.Keys)
            {
                MarkTypeReferenced(typeName, null, allTypes, mergedFields, mergedArrays, processedTypePaths);
            }

            // Also include types referenced from primary schema fields
            foreach (var field in resolved.Schema.Fields.Values)
            {
                CollectTypeRefsFromField(field, allTypes, mergedFields, mergedArrays, processedTypePaths);
            }

            // And from primary schema arrays
            foreach (var array in resolved.Schema.Arrays.Values)
            {
                CollectTypeRefsFromFieldType(array.ItemType, allTypes, mergedFields, mergedArrays, processedTypePaths);
            }
        }

        private void MarkTypeReferenced(
            string typeName,
            string? ns,
            Dictionary<string, SchemaType> allTypes,
            Dictionary<string, SchemaField> mergedFields,
            Dictionary<string, SchemaArray> mergedArrays,
            HashSet<string> processedTypePaths)
        {
            string qualifiedName = BuildQualifiedName(typeName, ns);

            if (!_referencedTypes.Add(qualifiedName))
                return;

            // Find the type and recursively mark types it references
            if (allTypes.TryGetValue(qualifiedName, out var schemaType))
            {
                foreach (var field in schemaType.SchemaFields)
                {
                    CollectTypeRefsFromField(field, allTypes, mergedFields, mergedArrays, processedTypePaths);
                }

                // If this type inherits, process base types
                if (schemaType.Parents != null)
                {
                    foreach (string parent in schemaType.Parents)
                    {
                        ProcessInheritedFieldSections(
                            parent, allTypes, mergedFields, mergedArrays, processedTypePaths);
                    }
                }
            }

            // Process field sections belonging to this type path
            ProcessFieldSectionsForType(qualifiedName, allTypes, mergedFields, mergedArrays, processedTypePaths);
        }

        private void ProcessInheritedFieldSections(
            string baseTypeName,
            Dictionary<string, SchemaType> allTypes,
            Dictionary<string, SchemaField> mergedFields,
            Dictionary<string, SchemaArray> mergedArrays,
            HashSet<string> processedTypePaths)
        {
            string qualified = ResolveTypeName(baseTypeName);

            // Try multiple name formats
            if (!allTypes.ContainsKey(qualified))
            {
                if (baseTypeName.Contains('_'))
                    qualified = baseTypeName;
                if (!allTypes.ContainsKey(qualified))
                    qualified = baseTypeName;
            }

            // Mark the base type as referenced
            if (_referencedTypes.Add(qualified))
            {
                if (allTypes.TryGetValue(qualified, out var baseType))
                {
                    foreach (var field in baseType.SchemaFields)
                    {
                        CollectTypeRefsFromField(field, allTypes, mergedFields, mergedArrays, processedTypePaths);
                    }
                    // Recursively check if base type also inherits
                    if (baseType.Parents != null)
                    {
                        foreach (string parent in baseType.Parents)
                        {
                            ProcessInheritedFieldSections(
                                parent, allTypes, mergedFields, mergedArrays, processedTypePaths);
                        }
                    }
                }
            }

            ProcessFieldSectionsForType(qualified, allTypes, mergedFields, mergedArrays, processedTypePaths);
        }

        private void ProcessFieldSectionsForType(
            string typePath,
            Dictionary<string, SchemaType> allTypes,
            Dictionary<string, SchemaField> mergedFields,
            Dictionary<string, SchemaArray> mergedArrays,
            HashSet<string> processedTypePaths)
        {
            if (processedTypePaths.Contains(typePath))
                return;
            processedTypePaths.Add(typePath);

            string prefix = typePath + ".";

            // Find nested types that start with this type path
            var nestedTypeNames = allTypes.Keys
                .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            foreach (string nestedName in nestedTypeNames)
            {
                if (_referencedTypes.Add(nestedName))
                {
                    if (allTypes.TryGetValue(nestedName, out var nestedType))
                    {
                        foreach (var field in nestedType.SchemaFields)
                        {
                            CollectTypeRefsFromField(
                                field, allTypes, mergedFields, mergedArrays, processedTypePaths);
                        }
                        // Check inheritance
                        if (nestedType.Parents != null)
                        {
                            foreach (string parent in nestedType.Parents)
                            {
                                ProcessInheritedFieldSections(
                                    parent, allTypes, mergedFields, mergedArrays, processedTypePaths);
                            }
                        }
                        // Recursively process nested
                        ProcessFieldSectionsForType(
                            nestedName, allTypes, mergedFields, mergedArrays, processedTypePaths);
                    }
                }
            }

            // Find field paths starting with this type path
            var fieldPaths = mergedFields
                .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.Ordinal) || kvp.Key == typePath)
                .ToList();

            foreach (var kvp in fieldPaths)
            {
                CollectTypeRefsFromField(kvp.Value, allTypes, mergedFields, mergedArrays, processedTypePaths);
            }

            // Check arrays
            var arrayPaths = mergedArrays
                .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.Ordinal) || kvp.Key == typePath)
                .ToList();

            foreach (var kvp in arrayPaths)
            {
                CollectTypeRefsFromFieldType(
                    kvp.Value.ItemType, allTypes, mergedFields, mergedArrays, processedTypePaths);
            }
        }

        private void CollectTypeRefsFromField(
            SchemaField field,
            Dictionary<string, SchemaType> allTypes,
            Dictionary<string, SchemaField> mergedFields,
            Dictionary<string, SchemaArray> mergedArrays,
            HashSet<string> processedTypePaths)
        {
            CollectTypeRefsFromFieldType(field.FieldType, allTypes, mergedFields, mergedArrays, processedTypePaths);
        }

        private void CollectTypeRefsFromFieldType(
            SchemaFieldType fieldType,
            Dictionary<string, SchemaType> allTypes,
            Dictionary<string, SchemaField> mergedFields,
            Dictionary<string, SchemaArray> mergedArrays,
            HashSet<string> processedTypePaths)
        {
            string? target = null;
            if (fieldType is ReferenceFieldType refType)
                target = refType.Target;
            else if (fieldType is TypeRefFieldType typeRefType)
                target = typeRefType.Name;
            else if (fieldType is UnionFieldType unionType)
            {
                foreach (var member in unionType.Types)
                {
                    CollectTypeRefsFromFieldType(member, allTypes, mergedFields, mergedArrays, processedTypePaths);
                }
                return;
            }
            else
            {
                return;
            }

            string qualified = ResolveTypeRefName(target);

            // Try underscore format if not found
            if (!allTypes.ContainsKey(qualified) && target.Contains('_'))
            {
                qualified = target;
            }

            if (_referencedTypes.Add(qualified))
            {
                if (allTypes.TryGetValue(qualified, out var refSchemaType))
                {
                    foreach (var f in refSchemaType.SchemaFields)
                    {
                        CollectTypeRefsFromField(f, allTypes, mergedFields, mergedArrays, processedTypePaths);
                    }
                    // Check inheritance
                    if (refSchemaType.Parents != null)
                    {
                        foreach (string parent in refSchemaType.Parents)
                        {
                            ProcessInheritedFieldSections(
                                parent, allTypes, mergedFields, mergedArrays, processedTypePaths);
                        }
                    }
                }
                ProcessFieldSectionsForType(qualified, allTypes, mergedFields, mergedArrays, processedTypePaths);
            }
        }

        private string ResolveTypeRefName(string name)
        {
            if (name.Contains('.'))
            {
                int lastDot = name.LastIndexOf('.');
                string ns = name.Substring(0, lastDot).Replace('.', '_');
                string typePart = name.Substring(lastDot + 1);
                return BuildQualifiedName(typePart, ns);
            }
            // Simple name
            if (_typeSourceMap.TryGetValue(name, out string? sourceNs))
            {
                return BuildQualifiedName(name, sourceNs);
            }
            return name;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tree Shaking Filters
        // ─────────────────────────────────────────────────────────────────────

        private Dictionary<string, SchemaType> FilterReferencedTypes(
            Dictionary<string, SchemaType> types)
        {
            return types
                .Where(kvp => _referencedTypes.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<string, SchemaField> FilterReferencedFields(
            Dictionary<string, SchemaField> fields)
        {
            return fields
                .Where(kvp => IsTypePathReferenced(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<string, SchemaArray> FilterReferencedArrays(
            Dictionary<string, SchemaArray> arrays)
        {
            return arrays
                .Where(kvp => IsTypePathReferenced(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<string, List<SchemaObjectConstraint>> FilterReferencedConstraints(
            Dictionary<string, List<SchemaObjectConstraint>> constraints)
        {
            return constraints
                .Where(kvp => IsTypePathReferenced(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>Extract the type path from a field path (first segment before any dot).</summary>
        private static string GetTypePathFromFieldPath(string fieldPath)
        {
            int idx = fieldPath.IndexOf('.');
            return idx >= 0 ? fieldPath.Substring(0, idx) : fieldPath;
        }

        /// <summary>Check if a type path or any parent is referenced.</summary>
        private bool IsTypePathReferenced(string path)
        {
            string typePath = GetTypePathFromFieldPath(path);
            if (_referencedTypes.Contains(typePath))
                return true;

            // Primary schema fields don't have a type prefix
            if (!typePath.Contains('_'))
                return true;

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Cloning Helpers (netstandard2.0 safe)
        // ─────────────────────────────────────────────────────────────────────

        private static SchemaType CloneSchemaType(SchemaType source)
        {
            return new SchemaType
            {
                Name = source.Name,
                Description = source.Description,
                SchemaFields = source.SchemaFields.Select(CloneSchemaField).ToList(),
                Parents = new List<string>(source.Parents ?? new List<string>()),
            };
        }

        private static SchemaField CloneSchemaField(SchemaField source)
        {
            return new SchemaField
            {
                Name = source.Name,
                FieldType = source.FieldType,
                Required = source.Required,
                Confidential = source.Confidential,
                Deprecated = source.Deprecated,
                Description = source.Description,
                Constraints = new List<SchemaConstraint>(source.Constraints ?? new List<SchemaConstraint>()),
                DefaultValue = source.DefaultValue,
                Conditionals = new List<SchemaConditional>(source.Conditionals ?? new List<SchemaConditional>()),
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Convenience Functions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Convenience methods for schema flattening.</summary>
    public static class SchemaFlattening
    {
        /// <summary>Flatten a resolved schema using default options.</summary>
        /// <param name="resolved">The resolved schema from the import resolver.</param>
        /// <returns>The flattened result.</returns>
        public static FlattenedResult FlattenSchema(ResolvedSchema resolved)
        {
            var flattener = new SchemaFlattener();
            return flattener.Flatten(resolved);
        }

        /// <summary>
        /// Flatten a resolved schema with specified options and return the flattened result.
        /// </summary>
        /// <param name="resolved">The resolved schema from the import resolver.</param>
        /// <param name="options">Flattener options.</param>
        /// <returns>The flattened result.</returns>
        public static FlattenedResult FlattenSchema(ResolvedSchema resolved, FlattenerOptions options)
        {
            var flattener = new SchemaFlattener(options);
            return flattener.Flatten(resolved);
        }
    }
}
