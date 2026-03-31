namespace Buelo.Contracts;

/// <summary>
/// Defines how the template string should be interpreted by the engine.
/// </summary>
public enum TemplateMode
{
    /// <summary>
    /// The template is a complete C# class that implements <see cref="IReport"/>.
    /// This is the original mode and gives full control over the generated class.
    /// </summary>
    FullClass,

    /// <summary>
    /// The template is only the return expression (body) of the <c>GenerateReport</c> method.
    /// The engine automatically wraps the expression inside the required class/method scaffolding.
    /// <para>
    /// Inside the expression the following variables are available:
    /// <list type="bullet">
    ///   <item><description><c>ctx</c> – the full <see cref="ReportContext"/></description></item>
    ///   <item><description><c>data</c> – shorthand for <c>ctx.Data</c></description></item>
    ///   <item><description><c>helpers</c> – shorthand for <c>ctx.Helpers</c></description></item>
    /// </list>
    /// </para>
    /// </summary>
    Builder
}
