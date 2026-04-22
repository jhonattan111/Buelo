using Buelo.Contracts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Dynamic;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Buelo.Engine;

/// <summary>
/// Core rendering engine. Dynamically compiles C# templates (IDocument) with Roslyn
/// and renders them via QuestPDF (PDF) or data-driven Excel (ClosedXML).
/// </summary>
public class TemplateEngine
{
    private readonly IHelperRegistry _helpers;
    private readonly ITemplateStore? _store;

    public TemplateEngine(
        IHelperRegistry helpers,
        ITemplateStore? store = null,
        IWorkspaceStore? workspaceStore = null,
        IGlobalArtefactStore? _ = null)
    {
        _helpers = helpers;
        _store = store;
    }

    internal static PageSettings MergeSettings(PageSettings? template, PageSettings? request)
        => request ?? template ?? PageSettings.Default();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles and renders a C# IDocument template with the provided data.
    /// </summary>
    public Task<byte[]> RenderAsync(
        string template,
        object data,
        TemplateMode mode = TemplateMode.FullClass,
        PageSettings? pageSettings = null)
    {
        if (mode != TemplateMode.FullClass)
            throw new NotSupportedException($"Mode '{mode}' is not supported. Only FullClass is supported.");

        var effectiveSettings = pageSettings ?? PageSettings.Default();
        var dynData = ConvertToDynamic(data);
        var assembly = CompileTemplate(template);
        var docType = FindDocumentType(assembly);
        var document = CreateDocumentInstance(docType, dynData, effectiveSettings);
        return Task.FromResult(document.GeneratePdf());
    }

    /// <summary>
    /// Renders a stored TemplateRecord with optional data and page settings override.
    /// </summary>
    public Task<byte[]> RenderTemplateAsync(
        TemplateRecord template,
        object? data,
        PageSettings? pageSettings = null)
    {
        var effectiveData = data ?? template.MockData
            ?? throw new InvalidOperationException(
                "No data available for rendering. Provide data in the request or configure MockData on the template.");

        var effectiveSettings = MergeSettings(template.PageSettings, pageSettings);
        var dynData = ConvertToDynamic(effectiveData);
        var assembly = CompileTemplate(template.Template);
        var docType = FindDocumentType(assembly);
        var document = CreateDocumentInstance(docType, dynData, effectiveSettings);
        return Task.FromResult(document.GeneratePdf());
    }

    /// <summary>
    /// Validates a C# template using Roslyn compilation diagnostics.
    /// Returns actual compiler errors with line/column information.
    /// </summary>
    public Task<ValidationResult> ValidateAsync(
        string template,
        TemplateMode mode = TemplateMode.FullClass)
    {
        if (mode != TemplateMode.FullClass)
            throw new NotSupportedException($"Mode '{mode}' is not supported. Only FullClass is supported.");

        var syntaxTree = CSharpSyntaxTree.ParseText(template);
        var compilation = CSharpCompilation.Create(
            "BueloValidation",
            new[] { syntaxTree },
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d =>
            {
                var span = d.Location.GetLineSpan();
                return new ValidationError(
                    d.GetMessage(CultureInfo.InvariantCulture),
                    span.StartLinePosition.Line + 1,
                    span.StartLinePosition.Character + 1);
            })
            .ToList();

        return Task.FromResult(new ValidationResult
        {
            Valid = errors.Count == 0,
            Errors = errors
        });
    }

    // ── Roslyn helpers ────────────────────────────────────────────────────────

    private static Assembly CompileTemplate(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            assemblyName: $"BueloTemplate_{Guid.NewGuid():N}",
            syntaxTrees: new[] { syntaxTree },
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d =>
                {
                    var span = d.Location.GetLineSpan();
                    return $"  Line {span.StartLinePosition.Line + 1}: {d.GetMessage(CultureInfo.InvariantCulture)}";
                });
            throw new InvalidOperationException(
                $"Template compilation failed:\n{string.Join("\n", errors)}");
        }

        ms.Position = 0;
        return Assembly.Load(ms.ToArray());
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        // Ensure Microsoft.CSharp is loaded — required for dynamic dispatch
        // (CSharpArgumentInfo.Create). It may not be loaded in AppDomain yet.
        _ = typeof(Microsoft.CSharp.RuntimeBinder.Binder);

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
    }

    private static Type FindDocumentType(Assembly assembly)
    {
        var iDocType = typeof(IDocument);
        return assembly.GetTypes()
            .FirstOrDefault(t => iDocType.IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            ?? throw new InvalidOperationException(
                "Template must contain a non-abstract class implementing QuestPDF.Infrastructure.IDocument.");
    }

    private static IDocument CreateDocumentInstance(Type type, object data, PageSettings? pageSettings = null)
    {
        // Prefer single-parameter constructor (receives dynamic data)
        var ctor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var parameters = ctor.GetParameters();
        var args = parameters.Length switch
        {
            0 => Array.Empty<object?>(),
            1 => new object?[] { data },
            _ => parameters
                .Select(p =>
                {
                    if (p.ParameterType == typeof(PageSettings))
                        return (object?)(pageSettings ?? PageSettings.Default());
                    if (p.ParameterType == typeof(object))
                        return data;
                    return null;
                })
                .ToArray()
        };

        return (IDocument)ctor.Invoke(args);
    }

    // ── Data conversion ───────────────────────────────────────────────────────

    public static object ConvertToDynamic(object data)
    {
        if (data is JsonElement jsonElement)
            return JsonElementToExpando(jsonElement);
        return data;
    }

    public static object JsonElementToExpando(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var expando = (IDictionary<string, object>)new ExpandoObject();
                foreach (var prop in element.EnumerateObject())
                    expando[prop.Name] = JsonElementToExpando(prop.Value);
                return expando;

            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(JsonElementToExpando)
                    .ToList();

            case JsonValueKind.String:
                if (element.TryGetDateTime(out var dt)) return dt;
                return element.GetString() ?? string.Empty;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) return l;
                return element.GetDouble();

            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            case JsonValueKind.Null: return null!;
            default: return element.ToString();
        }
    }
}
