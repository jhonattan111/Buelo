namespace Buelo.Contracts;

/// <summary>
/// Defines how the template string should be interpreted by the engine.
/// </summary>
public enum TemplateMode
{
    /// <summary>
    /// The template is composed of up to four named blocks declared at the top level:
    /// an optional <c>page =&gt; { … }</c> page-configuration block, and up to three
    /// content sections identified by <c>page.Header()</c>, <c>page.Content()</c>, and
    /// <c>page.Footer()</c>.  The engine assembles the QuestPDF <c>Document.Create</c>
    /// scaffolding automatically.
    /// <para>
    /// Shared fragments (headers, footers) stored as <see cref="Partial"/> records can be
    /// injected via <c>@import &lt;slot&gt; from "&lt;nameOrGuid&gt;"</c> directives placed
    /// at the top of the template.
    /// </para>
    /// <para>
    /// Inside every block the following variables are available:
    /// <list type="bullet">
    ///   <item><description><c>ctx</c> – the full <see cref="ReportContext"/></description></item>
    ///   <item><description><c>data</c> – shorthand for <c>ctx.Data</c></description></item>
    ///   <item><description><c>helpers</c> – shorthand for <c>ctx.Helpers</c></description></item>
    /// </list>
    /// </para>
    /// </summary>
    Sections = 0,

    /// <summary>
    /// The template contains only the fluent chain that follows a <c>page.Header()</c>,
    /// <c>page.Content()</c>, or <c>page.Footer()</c> call — i.e., the body fragment
    /// that a <see cref="Sections"/> template imports via <c>@import</c>.
    /// <para>
    /// Partial records are not directly renderable; they are resolved and injected into
    /// <see cref="Sections"/> templates at wrap time.
    /// </para>
    /// </summary>
    Partial = 1,

    /// <summary>
    /// The template is authored in the YAML-like <c>.buelo</c> component DSL.
    /// The engine parses the component tree and compiles it to a <see cref="Sections"/>-mode
    /// C# source before rendering.
    /// </summary>
    BueloDsl = 2,

    [Obsolete("Use Sections or Partial instead.")]
    FullClass = 10,

    [Obsolete("Use Sections instead.")]
    Builder = 11,
}
