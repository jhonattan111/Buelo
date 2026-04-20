using Buelo.Contracts;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class SettingsCascadeTests
{
    private static PageSettings Project() => new() { PageSize = "A4", MarginHorizontal = 2.0f };
    private static PageSettings Template() => new() { PageSize = "Letter", MarginHorizontal = 1.5f };
    private static PageSettings Request() => new() { PageSize = "A3", MarginHorizontal = 1.0f };

    [Fact]
    public void MergeSettings_RequestOverridesTemplate_TemplateOverridesProject()
    {
        var result = TemplateEngine.MergeSettings(Project(), Template(), Request());

        // Request wins over everything.
        Assert.Equal("A3", result.PageSize);
        Assert.Equal(1.0f, result.MarginHorizontal);
    }

    [Fact]
    public void MergeSettings_NullRequest_UsesTemplateSettings()
    {
        var result = TemplateEngine.MergeSettings(Project(), Template(), null);

        // Template wins when request is null.
        Assert.Equal("Letter", result.PageSize);
        Assert.Equal(1.5f, result.MarginHorizontal);
    }

    [Fact]
    public void MergeSettings_AllNull_UsesProjectDefaults()
    {
        var result = TemplateEngine.MergeSettings(Project(), null, null);

        // Project is the fallback.
        Assert.Equal("A4", result.PageSize);
        Assert.Equal(2.0f, result.MarginHorizontal);
    }

    [Fact]
    public void MergeSettings_NullTemplateWithRequest_UsesRequest()
    {
        var result = TemplateEngine.MergeSettings(Project(), null, Request());

        Assert.Equal("A3", result.PageSize);
        Assert.Equal(1.0f, result.MarginHorizontal);
    }
}
