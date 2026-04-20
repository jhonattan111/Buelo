using Buelo.Engine.Validators;

namespace Buelo.Tests.Engine;

public class CsharpFileValidatorTests
{
    private readonly CsharpFileValidator _validator = new();

    [Fact]
    public async Task Validate_ValidCsharpClass_ReturnsNoErrors()
    {
        var source = """
            public class Foo
            {
                public int Bar { get; set; }
            }
            """;

        var result = await _validator.ValidateWithExtensionAsync(source, ".cs");

        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Validate_ValidCsxScript_ReturnsNoErrors()
    {
        var source = """
            var x = 42;
            Console.WriteLine(x);
            """;

        var result = await _validator.ValidateWithExtensionAsync(source, ".csx");

        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Validate_MissingSemicolon_ReturnsError()
    {
        // Missing semicolon after return.
        var source = """
            public class Foo
            {
                public int Bar() { return 42 }
            }
            """;

        var result = await _validator.ValidateWithExtensionAsync(source, ".cs");

        Assert.False(result.Valid);
        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, e => Assert.Equal("error", e.Severity));
    }

    [Fact]
    public async Task Validate_InvalidSyntax_ReturnsErrorWithPosition()
    {
        var source = "class { }";

        var result = await _validator.ValidateWithExtensionAsync(source, ".cs");

        Assert.False(result.Valid);
        var error = result.Errors[0];
        Assert.True(error.Line >= 1);
        Assert.True(error.Column >= 1);
    }
}
