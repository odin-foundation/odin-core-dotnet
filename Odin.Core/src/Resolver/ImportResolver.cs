using System;
using System.Collections.Generic;
using System.Linq;
using Odin.Core.Types;

namespace Odin.Core.Resolver
{
    /// <summary>
    /// Abstraction for loading files from the filesystem or other sources.
    /// Implementations control how import paths are resolved and how file
    /// content is read. This enables testing with virtual filesystems and
    /// sandboxed file access.
    /// </summary>
    public interface IFileReader
    {
        /// <summary>Read the content of a file at the given absolute path.</summary>
        /// <param name="path">Absolute path to the file.</param>
        /// <returns>The file content as a string.</returns>
        /// <exception cref="System.IO.IOException">When the file cannot be read.</exception>
        string ReadFile(string path);

        /// <summary>
        /// Resolve an import path relative to a base file path.
        /// Returns the absolute/canonical path to the imported file.
        /// </summary>
        /// <param name="basePath">The absolute path of the file containing the import directive.</param>
        /// <param name="importPath">The import path to resolve (relative or absolute).</param>
        /// <returns>The resolved absolute path.</returns>
        string ResolvePath(string basePath, string importPath);
    }

    /// <summary>Configuration for the import resolver.</summary>
    public sealed class ResolverOptions
    {
        /// <summary>Maximum import nesting depth (default: 32).</summary>
        public int MaxImportDepth { get; set; } = 32;

        /// <summary>Maximum total number of imports (default: 100).</summary>
        public int MaxTotalImports { get; set; } = 100;

        /// <summary>Whether to resolve imports in schema mode (types) or document mode.</summary>
        public bool SchemaMode { get; set; } = true;

        /// <summary>Allowed file extensions (default: [".odin"]).</summary>
        public List<string> AllowedExtensions { get; set; } = new List<string> { ".odin" };

        /// <summary>Maximum file size in bytes (default: 10 MB).</summary>
        public int MaxFileSize { get; set; } = 10 * 1024 * 1024;

        /// <summary>Creates default resolver options.</summary>
        public static ResolverOptions Default() => new ResolverOptions();
    }

    /// <summary>
    /// Registry of types collected from imported schemas.
    /// Supports local (unqualified) and namespaced type lookups.
    /// </summary>
    public sealed class TypeRegistry
    {
        /// <summary>Types without namespace (local types).</summary>
        public Dictionary<string, SchemaType> LocalTypes { get; } = new Dictionary<string, SchemaType>();

        /// <summary>Types organized by namespace (import alias).</summary>
        public Dictionary<string, Dictionary<string, SchemaType>> Namespaces { get; } =
            new Dictionary<string, Dictionary<string, SchemaType>>();

        /// <summary>Creates an empty type registry.</summary>
        public TypeRegistry() { }

        /// <summary>
        /// Register all types from a schema under an optional namespace.
        /// When <paramref name="ns"/> is null, types are registered as local types.
        /// </summary>
        /// <param name="types">The types to register.</param>
        /// <param name="ns">Optional namespace alias.</param>
        public void RegisterAll(Dictionary<string, SchemaType> types, string? ns = null)
        {
            if (ns != null)
            {
                if (!Namespaces.TryGetValue(ns, out var nsMap))
                {
                    nsMap = new Dictionary<string, SchemaType>();
                    Namespaces[ns] = nsMap;
                }
                foreach (var kvp in types)
                {
                    nsMap[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                foreach (var kvp in types)
                {
                    LocalTypes[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <summary>
        /// Look up a type by name, searching local types first, then namespaced
        /// references (e.g. "namespace.TypeName"), then all namespaces for unqualified names.
        /// </summary>
        /// <param name="name">The type name to look up.</param>
        /// <returns>The schema type, or null if not found.</returns>
        public SchemaType? Lookup(string name)
        {
            // Try local first
            if (LocalTypes.TryGetValue(name, out var localType))
                return localType;

            // Try namespaced: "namespace.TypeName"
            int dotPos = name.IndexOf('.');
            if (dotPos >= 0)
            {
                string ns = name.Substring(0, dotPos);
                string typeName = name.Substring(dotPos + 1);
                if (Namespaces.TryGetValue(ns, out var nsMap) && nsMap.TryGetValue(typeName, out var nsType))
                    return nsType;
            }

            // Search all namespaces for unqualified name
            foreach (var nsMap in Namespaces.Values)
            {
                if (nsMap.TryGetValue(name, out var foundType))
                    return foundType;
            }

            return null;
        }

        /// <summary>Returns all type names (including namespaced as "namespace.TypeName").</summary>
        public List<string> AllTypeNames()
        {
            var names = new List<string>(LocalTypes.Keys);
            foreach (var kvp in Namespaces)
            {
                foreach (var typeName in kvp.Value.Keys)
                {
                    names.Add($"{kvp.Key}.{typeName}");
                }
            }
            return names;
        }
    }

    /// <summary>Result of resolving all imports for a document.</summary>
    public sealed class ResolvedDocument
    {
        /// <summary>The original document.</summary>
        public OdinDocument Document { get; }

        /// <summary>All resolved import paths.</summary>
        public List<string> ResolvedPaths { get; }

        /// <summary>The merged type registry from all imports.</summary>
        public TypeRegistry TypeRegistry { get; }

        /// <summary>Creates a resolved document result.</summary>
        public ResolvedDocument(OdinDocument document, List<string> resolvedPaths, TypeRegistry typeRegistry)
        {
            Document = document;
            ResolvedPaths = resolvedPaths;
            TypeRegistry = typeRegistry;
        }
    }

    /// <summary>A single resolved import with its parsed schema and alias.</summary>
    public sealed class ResolvedImport
    {
        /// <summary>The resolved file path.</summary>
        public string Path { get; }

        /// <summary>The import alias (e.g., "types" from <c>@import "types.odin" as types</c>).</summary>
        public string? Alias { get; }

        /// <summary>The parsed schema (if successfully parsed in schema mode).</summary>
        public OdinSchemaDefinition? Schema { get; }

        /// <summary>Creates a resolved import.</summary>
        public ResolvedImport(string path, string? alias, OdinSchemaDefinition? schema)
        {
            Path = path;
            Alias = alias;
            Schema = schema;
        }
    }

    /// <summary>Result of resolving all imports for a schema.</summary>
    public sealed class ResolvedSchema
    {
        /// <summary>The original schema.</summary>
        public OdinSchemaDefinition Schema { get; }

        /// <summary>All resolved import paths.</summary>
        public List<string> ResolvedPaths { get; }

        /// <summary>The merged type registry from all imports.</summary>
        public TypeRegistry TypeRegistry { get; }

        /// <summary>Per-import resolved schemas (path, schema, and alias).</summary>
        public List<ResolvedImport> Imports { get; }

        /// <summary>Creates a resolved schema result.</summary>
        public ResolvedSchema(
            OdinSchemaDefinition schema,
            List<string> resolvedPaths,
            TypeRegistry typeRegistry,
            List<ResolvedImport> imports)
        {
            Schema = schema;
            ResolvedPaths = resolvedPaths;
            TypeRegistry = typeRegistry;
            Imports = imports;
        }
    }

    /// <summary>
    /// Resolves <c>@import</c> directives in ODIN documents and schemas.
    /// The resolver loads imported files, detects circular dependencies,
    /// and builds a type registry from imported schemas.
    /// </summary>
    public sealed class ImportResolver
    {
        private readonly IFileReader _reader;
        private readonly ResolverOptions _options;
        private readonly Dictionary<string, CachedEntry> _cache = new Dictionary<string, CachedEntry>();

        /// <summary>Creates a new import resolver with the given file reader and options.</summary>
        /// <param name="reader">The file reader implementation.</param>
        /// <param name="options">Resolver options (or null for defaults).</param>
        public ImportResolver(IFileReader reader, ResolverOptions? options = null)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _options = options ?? ResolverOptions.Default();
        }

        /// <summary>
        /// Delegate for parsing ODIN document text. Set this to supply
        /// your own parser implementation.
        /// </summary>
        public Func<string, OdinDocument>? DocumentParser { get; set; }

        /// <summary>
        /// Delegate for parsing ODIN schema text. Set this to supply
        /// your own schema parser implementation.
        /// </summary>
        public Func<string, OdinSchemaDefinition>? SchemaParser { get; set; }

        /// <summary>
        /// Resolve a document file and all its imports.
        /// Reads the document, parses it, and recursively resolves all <c>@import</c> directives.
        /// </summary>
        /// <param name="filePath">Absolute path to the document file.</param>
        /// <returns>The resolved document with type registry and resolved paths.</returns>
        /// <exception cref="OdinParseException">When a file cannot be read, parsed, or a circular import is detected.</exception>
        public ResolvedDocument ResolveDocument(string filePath)
        {
            string content;
            try
            {
                content = _reader.ReadFile(filePath);
            }
            catch (Exception ex) when (!(ex is OdinParseException))
            {
                throw new OdinParseException(
                    ParseErrorCode.UnexpectedCharacter, 1, 1,
                    $"Failed to read file '{filePath}': {ex.Message}");
            }

            var doc = ParseDocumentText(content);

            var detector = new CircularDetector();
            var registry = new TypeRegistry();
            var resolvedPaths = new List<string>();
            var resolvedImports = new List<ResolvedImport>();
            int totalImports = 0;

            detector.Enter(filePath);

            ResolveImportsRecursive(
                filePath,
                doc.Imports,
                detector,
                registry,
                resolvedPaths,
                resolvedImports,
                ref totalImports,
                depth: 0);

            detector.Exit();

            return new ResolvedDocument(doc, resolvedPaths, registry);
        }

        /// <summary>
        /// Resolve a schema file and all its imports.
        /// Reads the schema, parses it, registers its types, and recursively
        /// resolves all <c>@import</c> directives.
        /// </summary>
        /// <param name="filePath">Absolute path to the schema file.</param>
        /// <returns>The resolved schema with type registry, resolved paths, and per-import schemas.</returns>
        /// <exception cref="OdinParseException">When a file cannot be read, parsed, or a circular import is detected.</exception>
        public ResolvedSchema ResolveSchema(string filePath)
        {
            string content;
            try
            {
                content = _reader.ReadFile(filePath);
            }
            catch (Exception ex) when (!(ex is OdinParseException))
            {
                throw new OdinParseException(
                    ParseErrorCode.UnexpectedCharacter, 1, 1,
                    $"Failed to read schema '{filePath}': {ex.Message}");
            }

            var schema = ParseSchemaText(content);
            var detector = new CircularDetector();
            var registry = new TypeRegistry();
            var resolvedPaths = new List<string>();
            var resolvedImports = new List<ResolvedImport>();
            int totalImports = 0;

            detector.Enter(filePath);

            // Register types from this schema
            registry.RegisterAll(schema.Types);

            // Convert schema imports to OdinImport for recursion
            var imports = schema.Imports
                .Select(i => new OdinImport(i.Path) { Alias = i.Alias, Line = 0 })
                .ToArray();

            ResolveImportsRecursive(
                filePath,
                imports,
                detector,
                registry,
                resolvedPaths,
                resolvedImports,
                ref totalImports,
                depth: 0);

            detector.Exit();

            return new ResolvedSchema(schema, resolvedPaths, registry, resolvedImports);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Recursive Import Resolution
        // ─────────────────────────────────────────────────────────────────────

        private void ResolveImportsRecursive(
            string basePath,
            IReadOnlyList<OdinImport> imports,
            CircularDetector detector,
            TypeRegistry registry,
            List<string> resolvedPaths,
            List<ResolvedImport> resolvedImports,
            ref int totalImports,
            int depth)
        {
            if (depth > _options.MaxImportDepth)
            {
                throw new OdinParseException(
                    ParseErrorCode.MaximumDepthExceeded, 1, 1,
                    $"Import depth {depth} exceeds maximum {_options.MaxImportDepth}");
            }

            foreach (var import in imports)
            {
                totalImports++;
                if (totalImports > _options.MaxTotalImports)
                {
                    throw new OdinParseException(
                        ParseErrorCode.MaximumDocumentSizeExceeded, import.Line, 1,
                        $"Total imports {totalImports} exceeds maximum {_options.MaxTotalImports}");
                }

                // Validate extension
                string importPath = import.Path;
                bool hasAllowedExtension = false;
                foreach (var ext in _options.AllowedExtensions)
                {
                    if (importPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllowedExtension = true;
                        break;
                    }
                }
                if (!hasAllowedExtension)
                {
                    throw new OdinParseException(
                        ParseErrorCode.UnexpectedCharacter, import.Line, 1,
                        $"Import path '{importPath}' has disallowed extension");
                }

                // Resolve path
                string resolved;
                try
                {
                    resolved = _reader.ResolvePath(basePath, importPath);
                }
                catch (Exception ex)
                {
                    throw new OdinParseException(
                        ParseErrorCode.UnexpectedCharacter, import.Line, 1,
                        $"Failed to resolve import '{importPath}': {ex.Message}");
                }

                // Check circular
                if (detector.IsCircular(resolved))
                {
                    throw new OdinParseException(
                        ParseErrorCode.UnexpectedCharacter, import.Line, 1,
                        $"Circular import detected: {importPath}");
                }

                // Check cache
                string normalized = NormalizePath(resolved);
                if (_cache.TryGetValue(normalized, out var cached))
                {
                    if (cached.Schema != null)
                    {
                        registry.RegisterAll(cached.Schema.Types, import.Alias);
                        resolvedImports.Add(new ResolvedImport(resolved, import.Alias, cached.Schema));
                    }
                    else
                    {
                        resolvedImports.Add(new ResolvedImport(resolved, import.Alias, null));
                    }
                    resolvedPaths.Add(resolved);
                    continue;
                }

                // Load and parse
                string content;
                try
                {
                    content = _reader.ReadFile(resolved);
                }
                catch (Exception ex)
                {
                    throw new OdinParseException(
                        ParseErrorCode.UnexpectedCharacter, import.Line, 1,
                        $"Failed to read import '{resolved}': {ex.Message}");
                }

                if (content.Length > _options.MaxFileSize)
                {
                    throw new OdinParseException(
                        ParseErrorCode.MaximumDocumentSizeExceeded, import.Line, 1,
                        $"Import file '{importPath}' exceeds maximum size");
                }

                detector.Enter(resolved);

                if (_options.SchemaMode)
                {
                    // Parse as schema
                    var schema = ParseSchemaText(content);
                    registry.RegisterAll(schema.Types, import.Alias);

                    resolvedImports.Add(new ResolvedImport(resolved, import.Alias, schema));

                    // Convert schema imports to OdinImport for recursion
                    var nestedImports = schema.Imports
                        .Select(i => new OdinImport(i.Path) { Alias = i.Alias, Line = 0 })
                        .ToArray();

                    _cache[normalized] = new CachedEntry(schema);

                    ResolveImportsRecursive(
                        resolved,
                        nestedImports,
                        detector,
                        registry,
                        resolvedPaths,
                        resolvedImports,
                        ref totalImports,
                        depth + 1);
                }
                else
                {
                    // Parse as document
                    var doc = ParseDocumentText(content);
                    var nestedImports = doc.Imports;

                    resolvedImports.Add(new ResolvedImport(resolved, import.Alias, null));

                    _cache[normalized] = new CachedEntry(null);

                    ResolveImportsRecursive(
                        resolved,
                        nestedImports,
                        detector,
                        registry,
                        resolvedPaths,
                        resolvedImports,
                        ref totalImports,
                        depth + 1);
                }

                detector.Exit();
                resolvedPaths.Add(resolved);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Parsing Helpers
        // ─────────────────────────────────────────────────────────────────────

        private OdinDocument ParseDocumentText(string content)
        {
            if (DocumentParser != null)
                return DocumentParser(content);

            // Fallback: return empty document. In a complete SDK this would call Odin.Parse.
            return OdinDocument.Empty();
        }

        private OdinSchemaDefinition ParseSchemaText(string content)
        {
            if (SchemaParser != null)
                return SchemaParser(content);

            // Fallback: return empty schema. In a complete SDK this would call SchemaParser.Parse.
            return new OdinSchemaDefinition();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Path Normalization
        // ─────────────────────────────────────────────────────────────────────

        internal static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').ToLowerInvariant();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Cache Entry
        // ─────────────────────────────────────────────────────────────────────

        private sealed class CachedEntry
        {
            public OdinSchemaDefinition? Schema { get; }

            public CachedEntry(OdinSchemaDefinition? schema)
            {
                Schema = schema;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Circular Detector
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Stack-based circular import detector. Maintains an ordered chain
        /// of file paths currently being resolved and a hash set for O(1) lookup.
        /// </summary>
        internal sealed class CircularDetector
        {
            private readonly List<string> _chain = new List<string>();
            private readonly HashSet<string> _chainSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Enter a path into the chain. Throws if circular.
            /// </summary>
            /// <param name="path">The file path to enter.</param>
            /// <exception cref="OdinParseException">When a circular import is detected.</exception>
            public void Enter(string path)
            {
                string normalized = NormalizePath(path);
                if (_chainSet.Contains(normalized))
                {
                    string cycle = FormatCycle(normalized);
                    throw new OdinParseException(
                        ParseErrorCode.UnexpectedCharacter, 1, 1,
                        $"Circular import detected: {cycle}");
                }
                _chainSet.Add(normalized);
                _chain.Add(normalized);
            }

            /// <summary>Remove the top of the chain.</summary>
            public void Exit()
            {
                if (_chain.Count > 0)
                {
                    string last = _chain[_chain.Count - 1];
                    _chain.RemoveAt(_chain.Count - 1);
                    _chainSet.Remove(last);
                }
            }

            /// <summary>Check if a path would create a cycle.</summary>
            /// <param name="path">The file path to check.</param>
            /// <returns>True if the path is already in the chain.</returns>
            public bool IsCircular(string path)
            {
                return _chainSet.Contains(NormalizePath(path));
            }

            private string FormatCycle(string path)
            {
                var cycle = new List<string>();
                bool found = false;
                foreach (string p in _chain)
                {
                    if (p == path) found = true;
                    if (found) cycle.Add(p);
                }
                cycle.Add(path);
                return string.Join(" -> ", cycle);
            }
        }
    }
}
