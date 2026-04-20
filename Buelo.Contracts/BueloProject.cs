namespace Buelo.Contracts;

public class BueloProject
{
    public string Name { get; set; } = "Buelo Project";
    public string? Description { get; set; }
    public string Version { get; set; } = "1.0.0";
    public PageSettings PageSettings { get; set; } = new();
    public object? MockData { get; set; }
    public string DefaultOutputFormat { get; set; } = "pdf";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
