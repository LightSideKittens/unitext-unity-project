using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ============================================================================
// UniTextDocGen — Roslyn-based Documentation Generator
// ============================================================================

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run -- <input-path> <output-json>");
    Console.WriteLine("  input-path:  Path to directory with .cs files");
    Console.WriteLine("  output-json: Path to output api.json");
    return 1;
}

var inputPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);

if (!Directory.Exists(inputPath))
{
    Console.Error.WriteLine($"Error: Directory not found: {inputPath}");
    return 1;
}

Console.WriteLine($"Input:  {inputPath}");
Console.WriteLine($"Output: {outputPath}");

var generator = new DocumentationGenerator();
var apiDoc = generator.Generate(inputPath);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};

var json = JsonSerializer.Serialize(apiDoc, jsonOptions);
File.WriteAllText(outputPath, json);

// Generate audit file alongside api.json
var auditPath = Path.Combine(Path.GetDirectoryName(outputPath)!, "types-audit.json");
var auditEntries = generator.CollectAllTypes(inputPath);
DocumentationGenerator.WriteAuditFile(auditEntries, auditPath, jsonOptions);

Console.WriteLine($"Generated {apiDoc.Types.Count} types → {outputPath}");
Console.WriteLine($"Audit: {auditEntries.Count} total types → {auditPath}");
return 0;

// ============================================================================
// Data Models
// ============================================================================

public class ApiDocument
{
    public string Version { get; set; } = "0.9.0";
    public string GeneratedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public List<string> Namespaces { get; set; } = new();
    public List<TypeDoc> Types { get; set; } = new();
    public Dictionary<string, List<CategoryEntry>> Categories { get; set; } = new();
    public Dictionary<string, string> ExternalTypes { get; set; } = new();
}

public class CategoryEntry
{
    public string Name { get; set; } = "";
    public int Priority { get; set; } = 100;
}

public class TypeDoc
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Namespace { get; set; }
    public string? DeclaringType { get; set; } // For nested types: parent type name
    public string Kind { get; set; } = ""; // class, struct, interface, enum
    public string AccessModifier { get; set; } = "public";
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public bool IsStatic { get; set; }
    public string? BaseType { get; set; }
    public List<string> Interfaces { get; set; } = new();
    public string? Summary { get; set; }
    public string? Remarks { get; set; }
    public string? Example { get; set; }
    public List<string> SeeAlso { get; set; } = new();
    public List<TypeParameterDoc> TypeParameters { get; set; } = new();
    public string? Category { get; set; }
    public MembersDoc Members { get; set; } = new();
}

public class MembersDoc
{
    public List<MethodDoc> Methods { get; set; } = new();
    public List<PropertyDoc> Properties { get; set; } = new();
    public List<FieldDoc> Fields { get; set; } = new();
    public List<EventDoc> Events { get; set; } = new();
}

public class MethodDoc
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AccessModifier { get; set; } = "public";
    public bool IsAbstract { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsStatic { get; set; }
    public bool IsOverride { get; set; }
    public bool IsConstructor { get; set; }
    public string ReturnType { get; set; } = "void";
    public string? Summary { get; set; }
    public string? Remarks { get; set; }
    public string? Example { get; set; }
    public string? Returns { get; set; }
    public List<ParamDoc> Params { get; set; } = new();
    public List<string> ParamTypes { get; set; } = new();
    public List<ExceptionDoc> Exceptions { get; set; } = new();
    public List<string> SeeAlso { get; set; } = new();
    public List<TypeParameterDoc> TypeParameters { get; set; } = new();
}

public class PropertyDoc
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AccessModifier { get; set; } = "public";
    public bool IsStatic { get; set; }
    public string Type { get; set; } = "";
    public bool HasGetter { get; set; }
    public bool HasSetter { get; set; }
    public string? Summary { get; set; }
    public string? Remarks { get; set; }
    public string? Value { get; set; }
    public List<string> SeeAlso { get; set; } = new();
}

public class FieldDoc
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AccessModifier { get; set; } = "public";
    public bool IsStatic { get; set; }
    public bool IsReadonly { get; set; }
    public bool IsConst { get; set; }
    public string Type { get; set; } = "";
    public string? DefaultValue { get; set; }
    public string? Summary { get; set; }
}

public class EventDoc
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AccessModifier { get; set; } = "public";
    public bool IsStatic { get; set; }
    public string Type { get; set; } = "";
    public string? Summary { get; set; }
}

public class ParamDoc
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Description { get; set; }
}

public class ExceptionDoc
{
    public string Type { get; set; } = "";
    public string? Description { get; set; }
}

public class TypeParameterDoc
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public record TypeAuditEntry(
    string Name,
    string Kind,
    string Access,
    string File,
    bool HasSummary,
    bool IsPublic);

// ============================================================================
// Documentation Generator
// ============================================================================

public class DocumentationGenerator
{
    // Категории типов - order matters! More specific patterns first.
    // Prefix "=" means exact match, otherwise Contains().
    private static readonly (string pattern, string category)[] Categories = new[]
    {
        // ============ Emoji (before Fonts, more specific) ============
        ("EmojiFont", "emoji"),
        ("SystemEmojiFont", "emoji"),
        ("Emoji", "emoji"),

        // ============ Editor (before Fonts — UniTextFontEditor must not match UniTextFont) ============
        ("UniTextFontEditor", "editor"),
        ("UniTextFontToolsWindow", "editor"),
        ("UniTextEditor", "editor"),
        ("UniTextBuildProcessor", "editor"),
        ("UniTextEditorResources", "editor"),
        ("EditorShaderUtilities", "editor"),
        ("EscapeTextArea", "editor"),
        ("ModRegisterDrawer", "editor"),
        ("StyledListDrawer", "editor"),
        ("StyledListUtility", "editor"),
        ("TypeGroupAttribute", "editor"),
        ("TypeSelectorAttribute", "editor"),
        ("TypeSelectorDrawer", "editor"),
        ("TypedListDrawer", "editor"),
        ("ListCallbacks", "editor"),

        // ============ Fonts ============
        ("UniTextFont", "fonts"),
        ("UniTextAppearance", "fonts"),
        ("UniTextRenderMode", "fonts"),  // SDF/Smooth/Mono — defined in FontTypes.cs
        ("UniTextFontError", "fonts"),
        ("FontMaterial", "fonts"),
        ("FaceInfo", "fonts"),
        ("SharedFontCache", "fonts"),
        ("FreeType", "fonts"),
        ("CachedGlyphData", "fonts"),
        ("CharacterSet", "fonts"),
        ("=Glyph", "fonts"),            // Glyph struct (exact — avoid matching GlyphModifier)
        ("GlyphMetrics", "fonts"),
        ("GlyphRect", "fonts"),
        ("SdfGlyph", "fonts"),          // SdfGlyphRenderer, SdfGlyphResult
        ("BitmapData", "fonts"),
        ("FontSubsetter", "fonts"),
        ("=COLRv1Renderer", "fonts"),   // Exact match (not COLRv1RendererPool → utilities)

        // ============ Core ============
        ("UniText", "core"),
        ("TextProcessor", "core"),
        ("TextLayout", "core"),
        ("LayoutSettings", "core"),
        ("HorizontalAlignment", "core"),
        ("VerticalAlignment", "core"),
        ("TruncationMode", "core"),
        ("TextLine", "core"),
        ("TextRun", "core"),
        ("PositionedGlyph", "core"),
        ("TextHitResult", "core"),
        ("TextProcessSettings", "core"),
        ("DirtyFlags", "core"),
        ("GeneratedMeshSegment", "core"),
        ("RenderedGlyph", "core"),
        ("SharedPipelineComponents", "core"),
        ("TextRange", "core"),
        ("TextHighlighter", "core"),    // TextHighlighter, DefaultTextHighlighter
        ("SharedMeshes", "core"),

        // ============ Parse Rules (before Modifiers - more specific) ============
        ("ParseRule", "parseRules"),
        ("IParseRule", "parseRules"),
        ("RangeRule", "parseRules"),
        ("EllipsisTagRule", "parseRules"),
        ("RangeRule.Data", "parseRules"),

        // ============ Modifiers ============
        ("BaseModifier", "modifiers"),
        ("GlyphModifier", "modifiers"),
        ("Modifier", "modifiers"),
        ("AttributeParser", "modifiers"),
        ("AttributeSpan", "modifiers"),
        ("ParsedRange", "modifiers"),
        ("AttributeKeys", "modifiers"),
        ("IAttributeData", "modifiers"),
        ("LinkData", "modifiers"),
        ("ModRegister", "modifiers"),
        ("ListItemInfo", "modifiers"),
        ("OrderedMarkerStyle", "modifiers"),
        ("GlyphRenderHelper", "modifiers"),
        ("LineRenderHelper", "modifiers"),
        ("InlineObject", "modifiers"),
        ("RectTransformWrapper", "modifiers"),

        // ============ Unicode ============
        ("Bidi", "unicode"),
        ("LineBreak", "unicode"),
        ("Grapheme", "unicode"),
        ("Script", "unicode"),
        ("Unicode", "unicode"),
        ("BracketEntry", "unicode"),
        ("DefaultIgnorable", "unicode"),
        ("EastAsianWidth", "unicode"),
        ("ExtendedPictographic", "unicode"),
        ("GeneralCategory", "unicode"),
        ("IndicConjunctBreak", "unicode"),
        ("JoiningGroup", "unicode"),
        ("JoiningType", "unicode"),
        ("MirrorEntry", "unicode"),
        ("RangeEntry", "unicode"),
        ("TextDirection", "unicode"),

        // ============ Shaping ============
        ("Shap", "shaping"),
        ("HarfBuzz", "shaping"),
        ("Itemizer", "shaping"),

        // ============ Utilities ============
        ("Pool", "utilities"),
        ("Cat", "utilities"),
        ("FastIntDictionary", "utilities"),
        ("PathAccessor", "utilities"),
        ("ObjectPathAccessor", "utilities"),
        ("TypedPathAccessor", "utilities"),
        ("CachedAccessor", "utilities"),
        ("DirectFieldAccessor", "utilities"),
        ("CachedTransformData", "utilities"),
        ("InteractiveRange", "core"),
        ("Range", "utilities"),
        ("RangeEx", "utilities"),
        ("IndexEx", "utilities"),
        ("Enumerator", "utilities"),
        ("ObjectUtils", "utilities"),
        ("StyledList", "utilities"),     // StyledList<T> (StyledListDrawer matched earlier → editor)
        ("TypedList", "utilities"),      // TypedList<T> (TypedListDrawer matched earlier → editor)
    };

    // Типы которые нужно исключить
    private static readonly HashSet<string> ExcludedTypes = new()
    {
        "<>", // compiler generated
        "__",  // compiler generated
        "UniText_BaseShaderGUI",   // Editor only
        "UniText_BitmapShaderGUI", // Editor only
        "UniText_SDFShaderGUI",    // Editor only
        "BasicUsageExample",       // Example code, not API
        "EventSystemBootstrap",    // Example code, not API
        "DraggableRect",           // Example code, not API
        "TestSegmentFontInfo",     // Test helper, not API
    };

    public ApiDocument Generate(string inputPath)
    {
        var csFiles = Directory.GetFiles(inputPath, "*.cs", SearchOption.AllDirectories);
        Console.WriteLine($"Found {csFiles.Length} .cs files");

        var types = new List<TypeDoc>();

        foreach (var file in csFiles)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code, path: file);
            var root = tree.GetCompilationUnitRoot();

            // Создаём compilation для semantic analysis
            var compilation = CSharpCompilation.Create("DocGen")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree);
            var semanticModel = compilation.GetSemanticModel(tree);

            var typeDeclarations = root.DescendantNodes()
                .OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in typeDeclarations)
            {
                var typeDoc = ProcessType(typeDecl, semanticModel);
                if (typeDoc != null && !ShouldExclude(typeDoc.Name))
                {
                    types.Add(typeDoc);
                }
            }

            // Enums
            var enumDeclarations = root.DescendantNodes()
                .OfType<EnumDeclarationSyntax>();

            foreach (var enumDecl in enumDeclarations)
            {
                var typeDoc = ProcessEnum(enumDecl, semanticModel);
                if (typeDoc != null && !ShouldExclude(typeDoc.Name))
                {
                    types.Add(typeDoc);
                }
            }
        }

        // Merge partial classes (combine all members and interfaces)
        types = types
            .GroupBy(t => t.Id)
            .Select(MergePartialTypes)
            .ToList();

        // Sort by category, then by name
        types = types
            .OrderBy(t => t.Category ?? "zzz")
            .ThenBy(t => t.Name)
            .ToList();

        var categories = BuildCategoriesMetadata(types);
        var externalTypes = BuildExternalTypes(types);

        return new ApiDocument
        {
            Types = types,
            Categories = categories,
            ExternalTypes = externalTypes
        };
    }

    /// <summary>
    /// Collects ALL types from source files (including non-public and excluded)
    /// for audit purposes.
    /// </summary>
    public List<TypeAuditEntry> CollectAllTypes(string inputPath)
    {
        var csFiles = Directory.GetFiles(inputPath, "*.cs", SearchOption.AllDirectories);
        var entries = new List<TypeAuditEntry>();
        var seen = new HashSet<string>(); // deduplicate partial classes

        foreach (var file in csFiles)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code, path: file);
            var root = tree.GetCompilationUnitRoot();

            var compilation = CSharpCompilation.Create("Audit")
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
                .AddSyntaxTrees(tree);
            var semanticModel = compilation.GetSemanticModel(tree);

            // Relative path for readability
            var relPath = Path.GetRelativePath(inputPath, file).Replace('\\', '/');

            foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (symbol == null) continue;

                var name = typeDecl is EnumDeclarationSyntax
                    ? symbol.Name
                    : FormatTypeNameWithGenerics(symbol);

                var fullId = typeDecl is EnumDeclarationSyntax
                    ? GetFullEnumId(symbol)
                    : GetFullTypeId(symbol);

                // Deduplicate partial classes — keep first occurrence
                if (!seen.Add(fullId)) continue;

                var kind = typeDecl switch
                {
                    EnumDeclarationSyntax => "enum",
                    _ => GetTypeKind((TypeDeclarationSyntax)typeDecl)
                };

                var isPublic = IsPubliclyAccessible(symbol);
                var hasSummary = GetXmlDocumentation(typeDecl) is { } xml
                    && !string.IsNullOrWhiteSpace(ExtractXmlElement(xml, "summary"));

                entries.Add(new TypeAuditEntry(
                    Name: name,
                    Kind: kind,
                    Access: GetAccessModifier(symbol.DeclaredAccessibility),
                    File: relPath,
                    HasSummary: hasSummary,
                    IsPublic: isPublic));
            }
        }

        return entries.OrderBy(e => e.File).ThenBy(e => e.Name).ToList();
    }

    public static void WriteAuditFile(List<TypeAuditEntry> entries, string outputPath, JsonSerializerOptions jsonOptions)
    {
        // Build nested dictionary mirroring the file tree
        var root = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in entries.GroupBy(e => e.File).OrderBy(g => g.Key))
        {
            var segments = group.Key.Split('/');
            var current = root;

            // Navigate/create folder nodes
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!current.TryGetValue(segments[i], out var child))
                {
                    child = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    current[segments[i]] = child;
                }
                current = (SortedDictionary<string, object>)child;
            }

            // Leaf = .cs file → array of types
            var fileName = segments[^1];
            var types = group.OrderBy(e => e.Name).Select(e => new Dictionary<string, object>
            {
                ["name"] = e.Name,
                ["kind"] = e.Kind,
                ["access"] = e.Access,
                ["hasSummary"] = e.HasSummary
            }).ToList<object>();

            current[fileName] = types;
        }

        var json = JsonSerializer.Serialize(root, jsonOptions);
        File.WriteAllText(outputPath, json);
    }

    private static TypeDoc MergePartialTypes(IGrouping<string, TypeDoc> group)
    {
        var parts = group.ToList();
        if (parts.Count == 1)
            return parts[0];

        // Start with the part that has the best documentation
        var result = parts.OrderByDescending(t => (t.Summary?.Length ?? 0) + (t.Remarks?.Length ?? 0)).First();

        // Merge from all other parts
        foreach (var part in parts.Where(p => p != result))
        {
            // Merge interfaces (deduplicate)
            foreach (var iface in part.Interfaces)
            {
                if (!result.Interfaces.Contains(iface))
                    result.Interfaces.Add(iface);
            }

            // Merge methods (by Id to avoid duplicates)
            var existingMethodIds = new HashSet<string>(result.Members.Methods.Select(m => m.Id));
            foreach (var method in part.Members.Methods)
            {
                if (!existingMethodIds.Contains(method.Id))
                    result.Members.Methods.Add(method);
            }

            // Merge properties (by Id)
            var existingPropIds = new HashSet<string>(result.Members.Properties.Select(p => p.Id));
            foreach (var prop in part.Members.Properties)
            {
                if (!existingPropIds.Contains(prop.Id))
                    result.Members.Properties.Add(prop);
            }

            // Merge fields (by Id)
            var existingFieldIds = new HashSet<string>(result.Members.Fields.Select(f => f.Id));
            foreach (var field in part.Members.Fields)
            {
                if (!existingFieldIds.Contains(field.Id))
                    result.Members.Fields.Add(field);
            }

            // Merge events (by Id)
            var existingEventIds = new HashSet<string>(result.Members.Events.Select(e => e.Id));
            foreach (var evt in part.Members.Events)
            {
                if (!existingEventIds.Contains(evt.Id))
                    result.Members.Events.Add(evt);
            }

            // Merge SeeAlso
            foreach (var see in part.SeeAlso)
            {
                if (!result.SeeAlso.Contains(see))
                    result.SeeAlso.Add(see);
            }

            // Take better Summary/Remarks if current is empty
            if (string.IsNullOrEmpty(result.Summary) && !string.IsNullOrEmpty(part.Summary))
                result.Summary = part.Summary;
            if (string.IsNullOrEmpty(result.Remarks) && !string.IsNullOrEmpty(part.Remarks))
                result.Remarks = part.Remarks;
            if (string.IsNullOrEmpty(result.Example) && !string.IsNullOrEmpty(part.Example))
                result.Example = part.Example;
        }

        return result;
    }

    private Dictionary<string, List<CategoryEntry>> BuildCategoriesMetadata(List<TypeDoc> types)
    {
        var result = new Dictionary<string, List<CategoryEntry>>();

        // Priority mappings for important types
        var priorityMap = new Dictionary<string, int>
        {
            // Core
            ["UniText"] = 1,
            ["TextProcessor"] = 2,
            ["TextLayout"] = 3,
            ["UniTextBuffers"] = 4,
            ["UniTextMeshGenerator"] = 5,
            ["UniTextSettings"] = 6,
            ["UniTextDebug"] = 7,

            // Unicode
            ["BidiEngine"] = 1,
            ["GraphemeBreaker"] = 2,
            ["LineBreakAlgorithm"] = 3,
            ["ScriptAnalyzer"] = 4,
            ["UnicodeData"] = 5,
            ["IUnicodeDataProvider"] = 6,

            // Fonts
            ["UniTextFont"] = 1,
            ["UniTextFontEngine"] = 2,
            ["UniTextFonts"] = 3,
            ["UniTextAppearance"] = 4,
            ["UniTextFontProvider"] = 5,

            // Modifiers
            ["BaseModifier"] = 1,
            ["GlyphModifier"] = 2,
            ["AttributeParser"] = 3,
            ["AttributeSpan"] = 4,
            ["ParsedRange"] = 5,
            ["ModRegister"] = 6,

            // Parse Rules
            ["IParseRule"] = 1,
            ["TagParseRule"] = 2,
            ["CompositeParseRule"] = 3,
            ["StringParseRule"] = 4,

            // Shaping
            ["Shaper"] = 1,
            ["IShaper"] = 2,

            // Emoji
            ["EmojiFont"] = 1,
            ["SystemEmojiFont"] = 2,

            // Core data structures (after main classes)
            ["LayoutSettings"] = 10,
            ["TextProcessSettings"] = 11,
            ["TextRun"] = 20,
            ["TextLine"] = 21,
            ["PositionedGlyph"] = 22,
            ["RenderedGlyph"] = 23,
            ["TextHitResult"] = 24,
            ["TextRange"] = 25,
            ["InlineObject"] = 26,
            ["GeneratedMeshSegment"] = 27,
            ["DirtyFlags"] = 30,
            ["HorizontalAlignment"] = 31,
            ["VerticalAlignment"] = 32,
            ["TruncationMode"] = 33,
        };

        foreach (var type in types)
        {
            var category = type.Category ?? "other";
            if (!result.ContainsKey(category))
                result[category] = new List<CategoryEntry>();

            var priority = priorityMap.TryGetValue(type.Name, out var p) ? p : 100;
            result[category].Add(new CategoryEntry { Name = type.Name, Priority = priority });
        }

        // Sort each category by priority
        foreach (var category in result.Keys.ToList())
        {
            result[category] = result[category]
                .OrderBy(e => e.Priority)
                .ThenBy(e => e.Name)
                .ToList();
        }

        return result;
    }

    private Dictionary<string, string> BuildExternalTypes(List<TypeDoc> types)
    {
        var internalTypeNames = new HashSet<string>(types.Select(t => t.Name));
        var usedTypes = new HashSet<string>();

        // Collect all type references from members
        foreach (var type in types)
        {
            // Base type
            if (!string.IsNullOrEmpty(type.BaseType))
                CollectTypeNames(type.BaseType, usedTypes);

            // Interfaces
            foreach (var iface in type.Interfaces)
                CollectTypeNames(iface, usedTypes);

            // Methods
            foreach (var method in type.Members.Methods)
            {
                CollectTypeNames(method.ReturnType, usedTypes);
                foreach (var paramType in method.ParamTypes)
                    CollectTypeNames(paramType, usedTypes);
            }

            // Properties
            foreach (var prop in type.Members.Properties)
                CollectTypeNames(prop.Type, usedTypes);

            // Fields
            foreach (var field in type.Members.Fields)
                CollectTypeNames(field.Type, usedTypes);

            // Events
            foreach (var evt in type.Members.Events)
                CollectTypeNames(evt.Type, usedTypes);
        }

        // Filter out internal types and build URLs
        var result = new Dictionary<string, string>();
        foreach (var typeName in usedTypes.OrderBy(t => t))
        {
            if (internalTypeNames.Contains(typeName))
                continue;

            var url = GetExternalTypeUrl(typeName);
            if (url != null)
                result[typeName] = url;
        }

        return result;
    }

    private static void CollectTypeNames(string typeString, HashSet<string> result)
    {
        if (string.IsNullOrEmpty(typeString)) return;

        // Parse generic types: Dictionary<string, List<int>> → Dictionary, string, List, int
        var current = new StringBuilder();
        foreach (var c in typeString)
        {
            if (c == '<' || c == '>' || c == ',' || c == ' ' || c == '[' || c == ']')
            {
                if (current.Length > 0)
                {
                    var name = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(name) && !IsPrimitive(name))
                        result.Add(name);
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            var name = current.ToString().Trim();
            if (!string.IsNullOrEmpty(name) && !IsPrimitive(name))
                result.Add(name);
        }
    }

    private static bool IsPrimitive(string typeName)
    {
        return typeName switch
        {
            "void" or "bool" or "byte" or "sbyte" or "char" or
            "short" or "ushort" or "int" or "uint" or "long" or "ulong" or
            "float" or "double" or "decimal" or "string" or "object" or
            "nint" or "nuint" or "T" or "TKey" or "TValue" or "TSource" or "TResult" => true,
            _ => false
        };
    }

    // Unity types mapping
    private static readonly Dictionary<string, string> UnityTypes = new()
    {
        // Core
        ["MonoBehaviour"] = "MonoBehaviour",
        ["ScriptableObject"] = "ScriptableObject",
        ["GameObject"] = "GameObject",
        ["Transform"] = "Transform",
        ["Component"] = "Component",
        ["Object"] = "Object",

        // Math
        ["Vector2"] = "Vector2",
        ["Vector3"] = "Vector3",
        ["Vector4"] = "Vector4",
        ["Vector2Int"] = "Vector2Int",
        ["Vector3Int"] = "Vector3Int",
        ["Quaternion"] = "Quaternion",
        ["Matrix4x4"] = "Matrix4x4",
        ["Rect"] = "Rect",
        ["RectInt"] = "RectInt",
        ["Bounds"] = "Bounds",
        ["BoundsInt"] = "BoundsInt",

        // Graphics
        ["Color"] = "Color",
        ["Color32"] = "Color32",
        ["Material"] = "Material",
        ["Shader"] = "Shader",
        ["Texture"] = "Texture",
        ["Texture2D"] = "Texture2D",
        ["RenderTexture"] = "RenderTexture",
        ["Sprite"] = "Sprite",
        ["Mesh"] = "Mesh",
        ["MeshFilter"] = "MeshFilter",
        ["MeshRenderer"] = "MeshRenderer",

        // UI
        ["Canvas"] = "Canvas",
        ["CanvasRenderer"] = "CanvasRenderer",
        ["RectTransform"] = "RectTransform",
        ["Image"] = "UI.Image",
        ["Text"] = "UI.Text",
        ["Graphic"] = "UI.Graphic",
        ["MaskableGraphic"] = "UI.MaskableGraphic",
        ["VertexHelper"] = "UI.VertexHelper",

        // Text
        ["Font"] = "Font",
        ["FontStyle"] = "FontStyle",
        ["TextAnchor"] = "TextAnchor",
        ["HorizontalWrapMode"] = "HorizontalWrapMode",
        ["VerticalWrapMode"] = "VerticalWrapMode",

        // Input
        ["KeyCode"] = "KeyCode",
        ["Touch"] = "Touch",
        ["PointerEventData"] = "EventSystems.PointerEventData",

        // Assets
        ["AssetBundle"] = "AssetBundle",
        ["Resources"] = "Resources",

        // Attributes
        ["SerializeField"] = "SerializeField",
        ["HideInInspector"] = "HideInInspector",
        ["Header"] = "HeaderAttribute",
        ["Tooltip"] = "TooltipAttribute",
        ["Range"] = "RangeAttribute",
        ["Space"] = "SpaceAttribute",
        ["TextArea"] = "TextAreaAttribute",
        ["Multiline"] = "MultilineAttribute",

        // Events
        ["UnityEvent"] = "Events.UnityEvent",
        ["UnityAction"] = "Events.UnityAction",

        // Coroutines
        ["Coroutine"] = "Coroutine",
        ["WaitForSeconds"] = "WaitForSeconds",
        ["WaitForEndOfFrame"] = "WaitForEndOfFrame",
        ["YieldInstruction"] = "YieldInstruction",

        // Other
        ["AnimationCurve"] = "AnimationCurve",
        ["Gradient"] = "Gradient",
        ["GradientColorKey"] = "GradientColorKey",
        ["GradientAlphaKey"] = "GradientAlphaKey",
        ["LayerMask"] = "LayerMask",
        ["Application"] = "Application",
        ["Debug"] = "Debug",
        ["Time"] = "Time",
        ["Mathf"] = "Mathf",
        ["Random"] = "Random",
        ["GUIContent"] = "GUIContent",
        ["GUIStyle"] = "GUIStyle",
    };

    // .NET types mapping (namespace → URL path)
    private static readonly Dictionary<string, string> DotNetTypes = new()
    {
        // System
        ["Action"] = "system.action",
        ["Func"] = "system.func-1",
        ["Predicate"] = "system.predicate-1",
        ["Comparison"] = "system.comparison-1",
        ["EventHandler"] = "system.eventhandler",
        ["IDisposable"] = "system.idisposable",
        ["IComparable"] = "system.icomparable",
        ["IEquatable"] = "system.iequatable-1",
        ["ICloneable"] = "system.icloneable",
        ["IFormattable"] = "system.iformattable",
        ["Exception"] = "system.exception",
        ["ArgumentException"] = "system.argumentexception",
        ["ArgumentNullException"] = "system.argumentnullexception",
        ["InvalidOperationException"] = "system.invalidoperationexception",
        ["NotSupportedException"] = "system.notsupportedexception",
        ["NotImplementedException"] = "system.notimplementedexception",
        ["Nullable"] = "system.nullable-1",
        ["Span"] = "system.span-1",
        ["ReadOnlySpan"] = "system.readonlyspan-1",
        ["Memory"] = "system.memory-1",
        ["ReadOnlyMemory"] = "system.readonlymemory-1",
        ["ArraySegment"] = "system.arraysegment-1",
        ["Tuple"] = "system.tuple",
        ["ValueTuple"] = "system.valuetuple",
        ["Lazy"] = "system.lazy-1",
        ["WeakReference"] = "system.weakreference",
        ["GCHandle"] = "system.runtime.interopservices.gchandle",
        ["IntPtr"] = "system.intptr",
        ["UIntPtr"] = "system.uintptr",
        ["Guid"] = "system.guid",
        ["DateTime"] = "system.datetime",
        ["DateTimeOffset"] = "system.datetimeoffset",
        ["TimeSpan"] = "system.timespan",
        ["Uri"] = "system.uri",
        ["Version"] = "system.version",
        ["Type"] = "system.type",
        ["Attribute"] = "system.attribute",
        ["Enum"] = "system.enum",
        ["Array"] = "system.array",
        ["String"] = "system.string",
        ["StringBuilder"] = "system.text.stringbuilder",
        ["Encoding"] = "system.text.encoding",
        ["Regex"] = "system.text.regularexpressions.regex",
        ["Match"] = "system.text.regularexpressions.match",

        // System.Collections.Generic
        ["List"] = "system.collections.generic.list-1",
        ["Dictionary"] = "system.collections.generic.dictionary-2",
        ["HashSet"] = "system.collections.generic.hashset-1",
        ["Queue"] = "system.collections.generic.queue-1",
        ["Stack"] = "system.collections.generic.stack-1",
        ["LinkedList"] = "system.collections.generic.linkedlist-1",
        ["SortedList"] = "system.collections.generic.sortedlist-2",
        ["SortedSet"] = "system.collections.generic.sortedset-1",
        ["SortedDictionary"] = "system.collections.generic.sorteddictionary-2",
        ["IEnumerable"] = "system.collections.generic.ienumerable-1",
        ["IEnumerator"] = "system.collections.generic.ienumerator-1",
        ["ICollection"] = "system.collections.generic.icollection-1",
        ["IList"] = "system.collections.generic.ilist-1",
        ["IDictionary"] = "system.collections.generic.idictionary-2",
        ["ISet"] = "system.collections.generic.iset-1",
        ["IReadOnlyList"] = "system.collections.generic.ireadonlylist-1",
        ["IReadOnlyCollection"] = "system.collections.generic.ireadonlycollection-1",
        ["IReadOnlyDictionary"] = "system.collections.generic.ireadonlydictionary-2",
        ["KeyValuePair"] = "system.collections.generic.keyvaluepair-2",
        ["Comparer"] = "system.collections.generic.comparer-1",
        ["EqualityComparer"] = "system.collections.generic.equalitycomparer-1",
        ["IComparer"] = "system.collections.generic.icomparer-1",
        ["IEqualityComparer"] = "system.collections.generic.iequalitycomparer-1",

        // System.IO
        ["Stream"] = "system.io.stream",
        ["MemoryStream"] = "system.io.memorystream",
        ["FileStream"] = "system.io.filestream",
        ["StreamReader"] = "system.io.streamreader",
        ["StreamWriter"] = "system.io.streamwriter",
        ["BinaryReader"] = "system.io.binaryreader",
        ["BinaryWriter"] = "system.io.binarywriter",
        ["TextReader"] = "system.io.textreader",
        ["TextWriter"] = "system.io.textwriter",
        ["StringReader"] = "system.io.stringreader",
        ["StringWriter"] = "system.io.stringwriter",
        ["Path"] = "system.io.path",
        ["File"] = "system.io.file",
        ["Directory"] = "system.io.directory",
        ["FileInfo"] = "system.io.fileinfo",
        ["DirectoryInfo"] = "system.io.directoryinfo",

        // System.Threading
        ["Task"] = "system.threading.tasks.task",
        ["CancellationToken"] = "system.threading.cancellationtoken",
        ["CancellationTokenSource"] = "system.threading.cancellationtokensource",
        ["SemaphoreSlim"] = "system.threading.semaphoreslim",
        ["Mutex"] = "system.threading.mutex",
        ["Thread"] = "system.threading.thread",
        ["ThreadPool"] = "system.threading.threadpool",
        ["Monitor"] = "system.threading.monitor",
        ["Interlocked"] = "system.threading.interlocked",

        // Value types
        ["Int32"] = "system.int32",
        ["Int64"] = "system.int64",
        ["Int16"] = "system.int16",
        ["UInt32"] = "system.uint32",
        ["UInt64"] = "system.uint64",
        ["UInt16"] = "system.uint16",
        ["Byte"] = "system.byte",
        ["SByte"] = "system.sbyte",
        ["Single"] = "system.single",
        ["Double"] = "system.double",
        ["Decimal"] = "system.decimal",
        ["Boolean"] = "system.boolean",
        ["Char"] = "system.char",
    };

    private static string? GetExternalTypeUrl(string typeName)
    {
        // Check Unity types first
        if (UnityTypes.TryGetValue(typeName, out var unityPath))
            return $"https://docs.unity3d.com/ScriptReference/{unityPath}.html";

        // Check .NET types
        if (DotNetTypes.TryGetValue(typeName, out var dotnetPath))
            return $"https://learn.microsoft.com/en-us/dotnet/api/{dotnetPath}";

        // Unknown external type - no URL
        return null;
    }

    private bool ShouldExclude(string name)
    {
        foreach (var pattern in ExcludedTypes)
        {
            if (name.Contains(pattern)) return true;
        }
        return false;
    }

    private static bool IsPubliclyAccessible(INamedTypeSymbol symbol)
    {
        // Проверяем сам тип
        if (symbol.DeclaredAccessibility != Accessibility.Public) return false;

        // Проверяем все родительские типы
        var parent = symbol.ContainingType;
        while (parent != null)
        {
            if (parent.DeclaredAccessibility != Accessibility.Public) return false;
            parent = parent.ContainingType;
        }

        return true;
    }

    private TypeDoc? ProcessType(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
        if (symbol == null) return null;

        // Пропускаем non-public типы (включая проверку родительских типов)
        if (!IsPubliclyAccessible(symbol)) return null;

        var typeName = FormatTypeNameWithGenerics(symbol);
        var fullId = GetFullTypeId(symbol);

        var doc = new TypeDoc
        {
            Id = fullId,
            Name = typeName,
            Namespace = symbol.ContainingNamespace?.IsGlobalNamespace == true
                ? null
                : symbol.ContainingNamespace?.ToDisplayString(),
            DeclaringType = symbol.ContainingType != null
                ? GetFullTypeId(symbol.ContainingType)
                : null,
            Kind = GetTypeKind(typeDecl),
            AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
            IsAbstract = symbol.IsAbstract,
            IsSealed = symbol.IsSealed,
            IsStatic = symbol.IsStatic,
            BaseType = GetBaseTypeName(symbol),
            Interfaces = symbol.Interfaces.Select(i => FormatTypeName(i)).ToList(),
            Category = GetCategory(fullId)
        };

        // XML Documentation
        var xmlComment = GetXmlDocumentation(typeDecl);
        if (xmlComment != null)
        {
            doc.Summary = ExtractXmlElement(xmlComment, "summary");
            doc.Remarks = ExtractXmlElement(xmlComment, "remarks");
            doc.Example = ExtractCodeBlock(xmlComment, "example");
            doc.SeeAlso = ExtractSeeAlsoRefs(xmlComment);
            doc.TypeParameters = ExtractTypeParams(xmlComment);
        }

        // Members
        doc.Members = ProcessMembers(typeDecl, semanticModel);

        return doc;
    }

    private TypeDoc? ProcessEnum(EnumDeclarationSyntax enumDecl, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(enumDecl);
        if (symbol == null) return null;

        // Пропускаем non-public типы (включая проверку родительских типов)
        if (!IsPubliclyAccessible(symbol)) return null;

        var fullId = GetFullEnumId(symbol);

        var doc = new TypeDoc
        {
            Id = fullId,
            Name = symbol.Name,
            DeclaringType = symbol.ContainingType != null
                ? GetFullTypeId(symbol.ContainingType)
                : null,
            Kind = "enum",
            AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
            Category = GetCategory(fullId)
        };

        var xmlComment = GetXmlDocumentation(enumDecl);
        if (xmlComment != null)
        {
            doc.Summary = ExtractXmlElement(xmlComment, "summary");
            doc.Remarks = ExtractXmlElement(xmlComment, "remarks");
        }

        // Process enum members
        foreach (var member in enumDecl.Members)
        {
            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
            if (memberSymbol == null) continue;

            var fieldDoc = new FieldDoc
            {
                Id = $"{fullId}.{memberSymbol.Name}",
                Name = memberSymbol.Name,
                AccessModifier = "public",
                IsStatic = true,
                IsReadonly = true,
                Type = symbol.Name
            };

            // Get value if explicitly assigned
            if (member.EqualsValue != null)
            {
                fieldDoc.DefaultValue = member.EqualsValue.Value.ToString();
            }
            else if (memberSymbol.HasConstantValue)
            {
                fieldDoc.DefaultValue = memberSymbol.ConstantValue?.ToString();
            }

            var memberXmlComment = GetXmlDocumentation(member);
            if (memberXmlComment != null)
            {
                fieldDoc.Summary = ExtractXmlElement(memberXmlComment, "summary");
            }

            doc.Members.Fields.Add(fieldDoc);
        }

        return doc;
    }

    private MembersDoc ProcessMembers(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel)
    {
        var members = new MembersDoc();

        foreach (var member in typeDecl.Members)
        {
            switch (member)
            {
                case ConstructorDeclarationSyntax ctor:
                    var ctorDoc = ProcessConstructor(ctor, semanticModel, typeDecl);
                    if (ctorDoc != null) members.Methods.Add(ctorDoc);
                    break;

                case MethodDeclarationSyntax method:
                    var methodDoc = ProcessMethod(method, semanticModel, typeDecl);
                    if (methodDoc != null) members.Methods.Add(methodDoc);
                    break;

                case PropertyDeclarationSyntax property:
                    var propDoc = ProcessProperty(property, semanticModel, typeDecl);
                    if (propDoc != null) members.Properties.Add(propDoc);
                    break;

                case FieldDeclarationSyntax field:
                    var fieldDocs = ProcessField(field, semanticModel, typeDecl);
                    members.Fields.AddRange(fieldDocs);
                    break;

                case EventDeclarationSyntax evt:
                    var evtDoc = ProcessEvent(evt, semanticModel, typeDecl);
                    if (evtDoc != null) members.Events.Add(evtDoc);
                    break;

                case EventFieldDeclarationSyntax evtField:
                    var evtFieldDocs = ProcessEventField(evtField, semanticModel, typeDecl);
                    members.Events.AddRange(evtFieldDocs);
                    break;
            }
        }

        return members;
    }

    private MethodDoc? ProcessMethod(MethodDeclarationSyntax method, SemanticModel semanticModel, TypeDeclarationSyntax parentType)
    {
        var symbol = semanticModel.GetDeclaredSymbol(method);
        if (symbol == null) return null;

        // Только public и protected
        if (symbol.DeclaredAccessibility != Accessibility.Public &&
            symbol.DeclaredAccessibility != Accessibility.Protected)
            return null;

        var parentSymbol = semanticModel.GetDeclaredSymbol(parentType);
        var parentName = parentSymbol?.Name ?? "";

        var doc = new MethodDoc
        {
            Id = $"{parentName}.{symbol.Name}",
            Name = symbol.Name,
            AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
            IsAbstract = symbol.IsAbstract,
            IsVirtual = symbol.IsVirtual,
            IsStatic = symbol.IsStatic,
            IsOverride = symbol.IsOverride,
            ReturnType = FormatTypeName(symbol.ReturnType),
            ParamTypes = symbol.Parameters.Select(p => FormatTypeName(p.Type)).ToList()
        };

        // Type parameters
        if (symbol.TypeParameters.Length > 0)
        {
            doc.TypeParameters = symbol.TypeParameters
                .Select(tp => new TypeParameterDoc { Name = tp.Name })
                .ToList();
        }

        // XML Documentation
        var xmlComment = GetXmlDocumentation(method);
        if (xmlComment != null)
        {
            doc.Summary = ExtractXmlElement(xmlComment, "summary");
            doc.Remarks = ExtractXmlElement(xmlComment, "remarks");
            doc.Example = ExtractCodeBlock(xmlComment, "example");
            doc.Returns = ExtractXmlElement(xmlComment, "returns");
            doc.Params = ExtractParams(xmlComment, symbol);
            doc.Exceptions = ExtractExceptions(xmlComment);
            doc.SeeAlso = ExtractSeeAlsoRefs(xmlComment);

            // Добавляем описания к type parameters
            foreach (var tp in doc.TypeParameters)
            {
                tp.Description = ExtractTypeParamDescription(xmlComment, tp.Name);
            }
        }

        return doc;
    }

    private MethodDoc? ProcessConstructor(ConstructorDeclarationSyntax ctor, SemanticModel semanticModel, TypeDeclarationSyntax parentType)
    {
        var symbol = semanticModel.GetDeclaredSymbol(ctor);
        if (symbol == null) return null;

        if (symbol.DeclaredAccessibility != Accessibility.Public &&
            symbol.DeclaredAccessibility != Accessibility.Protected)
            return null;

        var parentSymbol = semanticModel.GetDeclaredSymbol(parentType);
        var parentName = parentSymbol?.Name ?? "";

        var doc = new MethodDoc
        {
            Id = $"{parentName}.ctor",
            Name = parentName,
            AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
            IsConstructor = true,
            ReturnType = parentName,
            ParamTypes = symbol.Parameters.Select(p => FormatTypeName(p.Type)).ToList()
        };

        var xmlComment = GetXmlDocumentation(ctor);
        if (xmlComment != null)
        {
            doc.Summary = ExtractXmlElement(xmlComment, "summary");
            doc.Remarks = ExtractXmlElement(xmlComment, "remarks");
            doc.Example = ExtractCodeBlock(xmlComment, "example");
            doc.Params = ExtractParams(xmlComment, symbol);
            doc.Exceptions = ExtractExceptions(xmlComment);
            doc.SeeAlso = ExtractSeeAlsoRefs(xmlComment);
        }

        return doc;
    }

    private PropertyDoc? ProcessProperty(PropertyDeclarationSyntax property, SemanticModel semanticModel, TypeDeclarationSyntax parentType)
    {
        var symbol = semanticModel.GetDeclaredSymbol(property);
        if (symbol == null) return null;

        if (symbol.DeclaredAccessibility != Accessibility.Public &&
            symbol.DeclaredAccessibility != Accessibility.Protected)
            return null;

        var parentSymbol = semanticModel.GetDeclaredSymbol(parentType);
        var parentName = parentSymbol?.Name ?? "";

        var doc = new PropertyDoc
        {
            Id = $"{parentName}.{symbol.Name}",
            Name = symbol.Name,
            AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
            IsStatic = symbol.IsStatic,
            Type = FormatTypeName(symbol.Type),
            HasGetter = symbol.GetMethod != null,
            HasSetter = symbol.SetMethod != null
        };

        var xmlComment = GetXmlDocumentation(property);
        if (xmlComment != null)
        {
            doc.Summary = ExtractXmlElement(xmlComment, "summary");
            doc.Remarks = ExtractXmlElement(xmlComment, "remarks");
            doc.Value = ExtractXmlElement(xmlComment, "value");
            doc.SeeAlso = ExtractSeeAlsoRefs(xmlComment);
        }

        return doc;
    }

    private List<FieldDoc> ProcessField(FieldDeclarationSyntax field, SemanticModel semanticModel, TypeDeclarationSyntax parentType)
    {
        var result = new List<FieldDoc>();
        var parentSymbol = semanticModel.GetDeclaredSymbol(parentType);
        var parentName = parentSymbol?.Name ?? "";

        foreach (var variable in field.Declaration.Variables)
        {
            var symbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
            if (symbol == null) continue;

            if (symbol.DeclaredAccessibility != Accessibility.Public &&
                symbol.DeclaredAccessibility != Accessibility.Protected)
                continue;

            var doc = new FieldDoc
            {
                Id = $"{parentName}.{symbol.Name}",
                Name = symbol.Name,
                AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
                IsStatic = symbol.IsStatic,
                IsReadonly = symbol.IsReadOnly,
                IsConst = symbol.IsConst,
                Type = FormatTypeName(symbol.Type)
            };

            var xmlComment = GetXmlDocumentation(field);
            if (xmlComment != null)
            {
                doc.Summary = ExtractXmlElement(xmlComment, "summary");
            }

            result.Add(doc);
        }

        return result;
    }

    private EventDoc? ProcessEvent(EventDeclarationSyntax evt, SemanticModel semanticModel, TypeDeclarationSyntax parentType)
    {
        var symbol = semanticModel.GetDeclaredSymbol(evt);
        if (symbol == null) return null;

        if (symbol.DeclaredAccessibility != Accessibility.Public &&
            symbol.DeclaredAccessibility != Accessibility.Protected)
            return null;

        var parentSymbol = semanticModel.GetDeclaredSymbol(parentType);
        var parentName = parentSymbol?.Name ?? "";

        var doc = new EventDoc
        {
            Id = $"{parentName}.{symbol.Name}",
            Name = symbol.Name,
            AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
            IsStatic = symbol.IsStatic,
            Type = FormatTypeName(symbol.Type)
        };

        var xmlComment = GetXmlDocumentation(evt);
        if (xmlComment != null)
        {
            doc.Summary = ExtractXmlElement(xmlComment, "summary");
        }

        return doc;
    }

    private List<EventDoc> ProcessEventField(EventFieldDeclarationSyntax evtField, SemanticModel semanticModel, TypeDeclarationSyntax parentType)
    {
        var result = new List<EventDoc>();
        var parentSymbol = semanticModel.GetDeclaredSymbol(parentType);
        var parentName = parentSymbol?.Name ?? "";

        foreach (var variable in evtField.Declaration.Variables)
        {
            var symbol = semanticModel.GetDeclaredSymbol(variable) as IEventSymbol;
            if (symbol == null) continue;

            if (symbol.DeclaredAccessibility != Accessibility.Public &&
                symbol.DeclaredAccessibility != Accessibility.Protected)
                continue;

            var doc = new EventDoc
            {
                Id = $"{parentName}.{symbol.Name}",
                Name = symbol.Name,
                AccessModifier = GetAccessModifier(symbol.DeclaredAccessibility),
                IsStatic = symbol.IsStatic,
                Type = FormatTypeName(symbol.Type)
            };

            var xmlComment = GetXmlDocumentation(evtField);
            if (xmlComment != null)
            {
                doc.Summary = ExtractXmlElement(xmlComment, "summary");
            }

            result.Add(doc);
        }

        return result;
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static string GetFullTypeId(INamedTypeSymbol symbol)
    {
        var parts = new List<string>();

        // Добавляем parent типы для nested types
        var current = symbol.ContainingType;
        while (current != null)
        {
            parts.Insert(0, current.Name);
            current = current.ContainingType;
        }

        // Добавляем имя типа с generic параметрами
        parts.Add(FormatTypeNameWithGenerics(symbol));

        return string.Join(".", parts);
    }

    private static string GetFullEnumId(INamedTypeSymbol symbol)
    {
        var parts = new List<string>();

        // Добавляем parent типы для nested enums
        var current = symbol.ContainingType;
        while (current != null)
        {
            parts.Insert(0, current.Name);
            current = current.ContainingType;
        }

        parts.Add(symbol.Name);

        return string.Join(".", parts);
    }

    private static string FormatTypeNameWithGenerics(INamedTypeSymbol symbol)
    {
        if (symbol.TypeParameters.Length == 0)
            return symbol.Name;

        var typeParams = string.Join(", ", symbol.TypeParameters.Select(tp => tp.Name));
        return $"{symbol.Name}<{typeParams}>";
    }

    private static string GetTypeKind(TypeDeclarationSyntax typeDecl)
    {
        return typeDecl switch
        {
            ClassDeclarationSyntax => "class",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            RecordDeclarationSyntax r => r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
            _ => "class"
        };
    }

    private static string GetAccessModifier(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.Private => "private",
            _ => "private"
        };
    }

    private static string FormatTypeName(ITypeSymbol type)
    {
        // Специальные типы
        return type.SpecialType switch
        {
            SpecialType.System_Void => "void",
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Byte => "byte",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_Char => "char",
            SpecialType.System_String => "string",
            SpecialType.System_Object => "object",
            _ => FormatComplexTypeName(type)
        };
    }

    private static string FormatComplexTypeName(ITypeSymbol type)
    {
        // Arrays
        if (type is IArrayTypeSymbol arrayType)
        {
            return $"{FormatTypeName(arrayType.ElementType)}[]";
        }

        // Generics
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var name = namedType.Name;
            var typeArgs = string.Join(", ", namedType.TypeArguments.Select(FormatTypeName));
            return $"{name}<{typeArgs}>";
        }

        // Nullable value types
        if (type.NullableAnnotation == NullableAnnotation.Annotated && type.IsValueType)
        {
            if (type is INamedTypeSymbol nt && nt.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return $"{FormatTypeName(nt.TypeArguments[0])}?";
            }
        }

        return type.Name;
    }

    private static string? GetBaseTypeName(INamedTypeSymbol symbol)
    {
        var baseType = symbol.BaseType;
        if (baseType == null || baseType.SpecialType == SpecialType.System_Object)
            return null;
        if (baseType.Name == "ValueType") // struct
            return null;

        return FormatTypeName(baseType);
    }

    private string GetCategory(string typeName)
    {
        foreach (var (pattern, category) in Categories)
        {
            bool match = pattern[0] == '='
                ? typeName == pattern.Substring(1)
                : typeName.Contains(pattern);
            if (match) return category;
        }
        return "other";
    }

    // ========================================================================
    // XML Documentation Extraction
    // ========================================================================

    private static DocumentationCommentTriviaSyntax? GetXmlDocumentation(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        return trivia;
    }

    private static string? ExtractXmlElement(DocumentationCommentTriviaSyntax doc, string elementName)
    {
        var element = doc.ChildNodes()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(x => x.StartTag.Name.ToString() == elementName);

        if (element == null) return null;

        var text = ExtractTextFromXmlContent(element.Content);
        return NormalizeWhitespace(text);
    }

    /// <summary>
    /// Извлекает блок кода с сохранением форматирования (для example).
    /// </summary>
    private static string? ExtractCodeBlock(DocumentationCommentTriviaSyntax doc, string elementName)
    {
        var element = doc.ChildNodes()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(x => x.StartTag.Name.ToString() == elementName);

        if (element == null) return null;

        var text = ExtractTextFromXmlContent(element.Content);
        return NormalizeCodeBlock(text);
    }

    /// <summary>
    /// Нормализует блок кода: сохраняет переносы строк, убирает общий отступ.
    /// </summary>
    private static string NormalizeCodeBlock(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Декодируем XML entities
        text = System.Net.WebUtility.HtmlDecode(text);

        // Разбиваем на строки
        var lines = text.Split('\n');

        // Убираем пустые строки в начале и конце
        var startIdx = 0;
        while (startIdx < lines.Length && string.IsNullOrWhiteSpace(lines[startIdx]))
            startIdx++;

        var endIdx = lines.Length - 1;
        while (endIdx >= startIdx && string.IsNullOrWhiteSpace(lines[endIdx]))
            endIdx--;

        if (startIdx > endIdx) return string.Empty;

        // Находим минимальный отступ (игнорируя пустые строки)
        var minIndent = int.MaxValue;
        for (var i = startIdx; i <= endIdx; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var indent = 0;
            foreach (var c in line)
            {
                if (c == ' ') indent++;
                else if (c == '\t') indent += 4;
                else break;
            }
            minIndent = Math.Min(minIndent, indent);
        }

        if (minIndent == int.MaxValue) minIndent = 0;

        // Собираем результат с убранным общим отступом
        var sb = new StringBuilder();
        for (var i = startIdx; i <= endIdx; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine();
                continue;
            }

            // Убираем minIndent пробелов/табов
            var removed = 0;
            var charIdx = 0;
            while (charIdx < line.Length && removed < minIndent)
            {
                if (line[charIdx] == ' ') { removed++; charIdx++; }
                else if (line[charIdx] == '\t') { removed += 4; charIdx++; }
                else break;
            }

            sb.Append(line.Substring(charIdx).TrimEnd('\r'));
            if (i < endIdx) sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ExtractTextFromXmlContent(SyntaxList<XmlNodeSyntax> content)
    {
        var sb = new StringBuilder();

        foreach (var node in content)
        {
            switch (node)
            {
                case XmlTextSyntax text:
                    foreach (var token in text.TextTokens)
                    {
                        sb.Append(token.Text);
                    }
                    break;

                case XmlEmptyElementSyntax empty:
                    var tagName = empty.Name.ToString();
                    if (tagName == "see" || tagName == "seealso")
                    {
                        var cref = empty.Attributes
                            .OfType<XmlCrefAttributeSyntax>()
                            .FirstOrDefault();
                        if (cref != null)
                        {
                            // Wrap type references in [[...]] markers for frontend parsing
                            sb.Append($"[[{FormatCref(cref.Cref.ToString())}]]");
                        }
                    }
                    else if (tagName == "paramref" || tagName == "typeparamref")
                    {
                        var nameAttr = empty.Attributes
                            .OfType<XmlNameAttributeSyntax>()
                            .FirstOrDefault();
                        if (nameAttr != null)
                        {
                            sb.Append(nameAttr.Identifier.ToString());
                        }
                    }
                    break;

                case XmlElementSyntax nested:
                    // Рекурсивно обрабатываем вложенные элементы
                    sb.Append(ExtractTextFromXmlContent(nested.Content));
                    break;
            }
        }

        return sb.ToString();
    }

    private static string FormatCref(string cref)
    {
        // Убираем префиксы типа T:, M:, P:, F:
        if (cref.Length > 2 && cref[1] == ':')
        {
            cref = cref.Substring(2);
        }

        // Берём последнюю часть пути
        var lastDot = cref.LastIndexOf('.');
        if (lastDot >= 0)
        {
            cref = cref.Substring(lastDot + 1);
        }

        // Убираем параметры методов
        var parenIndex = cref.IndexOf('(');
        if (parenIndex >= 0)
        {
            cref = cref.Substring(0, parenIndex);
        }

        return cref;
    }

    private static List<ParamDoc> ExtractParams(DocumentationCommentTriviaSyntax doc, IMethodSymbol method)
    {
        var result = new List<ParamDoc>();

        var paramElements = doc.ChildNodes()
            .OfType<XmlElementSyntax>()
            .Where(x => x.StartTag.Name.ToString() == "param");

        foreach (var paramElement in paramElements)
        {
            var nameAttr = paramElement.StartTag.Attributes
                .OfType<XmlNameAttributeSyntax>()
                .FirstOrDefault();

            if (nameAttr != null)
            {
                var paramName = nameAttr.Identifier.ToString();
                var paramSymbol = method.Parameters.FirstOrDefault(p => p.Name == paramName);

                result.Add(new ParamDoc
                {
                    Name = paramName,
                    Type = paramSymbol != null ? FormatTypeName(paramSymbol.Type) : "",
                    Description = NormalizeWhitespace(ExtractTextFromXmlContent(paramElement.Content))
                });
            }
        }

        return result;
    }

    private static List<ExceptionDoc> ExtractExceptions(DocumentationCommentTriviaSyntax doc)
    {
        var result = new List<ExceptionDoc>();

        var exceptionElements = doc.ChildNodes()
            .OfType<XmlElementSyntax>()
            .Where(x => x.StartTag.Name.ToString() == "exception");

        foreach (var element in exceptionElements)
        {
            var crefAttr = element.StartTag.Attributes
                .OfType<XmlCrefAttributeSyntax>()
                .FirstOrDefault();

            if (crefAttr != null)
            {
                result.Add(new ExceptionDoc
                {
                    Type = FormatCref(crefAttr.Cref.ToString()),
                    Description = NormalizeWhitespace(ExtractTextFromXmlContent(element.Content))
                });
            }
        }

        return result;
    }

    private static List<string> ExtractSeeAlsoRefs(DocumentationCommentTriviaSyntax doc)
    {
        var result = new List<string>();

        // <seealso> как отдельный элемент
        var seeAlsoElements = doc.ChildNodes()
            .OfType<XmlEmptyElementSyntax>()
            .Where(x => x.Name.ToString() == "seealso");

        foreach (var element in seeAlsoElements)
        {
            var cref = element.Attributes
                .OfType<XmlCrefAttributeSyntax>()
                .FirstOrDefault();
            if (cref != null)
            {
                result.Add(FormatCref(cref.Cref.ToString()));
            }
        }

        return result;
    }

    private static List<TypeParameterDoc> ExtractTypeParams(DocumentationCommentTriviaSyntax doc)
    {
        var result = new List<TypeParameterDoc>();

        var typeParamElements = doc.ChildNodes()
            .OfType<XmlElementSyntax>()
            .Where(x => x.StartTag.Name.ToString() == "typeparam");

        foreach (var element in typeParamElements)
        {
            var nameAttr = element.StartTag.Attributes
                .OfType<XmlNameAttributeSyntax>()
                .FirstOrDefault();

            if (nameAttr != null)
            {
                result.Add(new TypeParameterDoc
                {
                    Name = nameAttr.Identifier.ToString(),
                    Description = NormalizeWhitespace(ExtractTextFromXmlContent(element.Content))
                });
            }
        }

        return result;
    }

    private static string? ExtractTypeParamDescription(DocumentationCommentTriviaSyntax doc, string paramName)
    {
        var element = doc.ChildNodes()
            .OfType<XmlElementSyntax>()
            .Where(x => x.StartTag.Name.ToString() == "typeparam")
            .FirstOrDefault(x =>
            {
                var nameAttr = x.StartTag.Attributes
                    .OfType<XmlNameAttributeSyntax>()
                    .FirstOrDefault();
                return nameAttr?.Identifier.ToString() == paramName;
            });

        if (element == null) return null;

        return NormalizeWhitespace(ExtractTextFromXmlContent(element.Content));
    }

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Декодируем XML entities
        text = System.Net.WebUtility.HtmlDecode(text);

        // Заменяем множественные пробелы/переносы на один пробел
        var result = new StringBuilder();
        var lastWasSpace = true; // Начинаем с true чтобы trim в начале

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    result.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                result.Append(c);
                lastWasSpace = false;
            }
        }

        var s = result.ToString().TrimEnd();

        // Убираем пробел перед пунктуацией
        s = s.Replace(" .", ".").Replace(" ,", ",").Replace(" ;", ";").Replace(" :", ":");

        return s;
    }
}
