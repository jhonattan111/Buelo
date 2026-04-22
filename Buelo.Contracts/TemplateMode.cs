namespace Buelo.Contracts;

/// <summary>
/// Defines how the template source should be interpreted by the engine.
/// </summary>
public enum TemplateMode
{
    /// <summary>
    /// The template is a complete C# class implementing the QuestPDF <c>IDocument</c> interface.
    /// The class must be properly formatted and compilable C# code.
    /// </summary>
    FullClass = 1,
}
