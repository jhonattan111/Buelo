using Buelo.Engine.Validators;

namespace Buelo.Tests.Engine;

public class BueloDslValidatorTests
{
    private readonly BueloDslValidator _validator = new();

    [Fact]
    public async Task Validate_ValidBueloDsl_ReturnsNoErrors()
    {
        var source = """
            report title:
              text: Hello World
            """;

        var result = await _validator.ValidateAsync(source);

        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Validate_UnrecognizedComponent_ReturnsWarning()
    {
        var source = """
            unknownKeyword: foo
            report title:
              text: Hello
            """;

        var result = await _validator.ValidateAsync(source);

        Assert.Contains(result.Warnings, w => w.Severity == "warning" &&
            w.Message.Contains("Unrecognized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validate_DuplicatePageHeader_ReturnsError()
    {
        var source = """
            page header:
              text: First Header
            page header:
              text: Second Header
            """;

        var result = await _validator.ValidateAsync(source);

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate") &&
            e.Message.Contains("page header"));
    }

    [Fact]
    public async Task Validate_DataBlockWithoutDataDirective_ReturnsWarning()
    {
        var source = """
            data:
              text: {{ item.Name }}
            """;

        var result = await _validator.ValidateAsync(source);

        Assert.Contains(result.Warnings, w => w.Message.Contains("@data from"));
    }

    [Fact]
    public async Task Validate_UnbalancedExpression_ReturnsError()
    {
        var source = """
            report title:
              text: Hello {{ name
            """;

        var result = await _validator.ValidateAsync(source);

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Severity == "error");
    }
}
