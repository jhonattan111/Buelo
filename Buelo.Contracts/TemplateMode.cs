namespace Buelo.Contracts;

/// <summary>
/// Defines how the template source should be interpreted by the engine.
/// </summary>
public enum TemplateMode
{
    /// <summary>
    /// The template is authored in the YAML-like <c>.buelo</c> component DSL.
    /// </summary>
    BueloDsl = 3,
}
