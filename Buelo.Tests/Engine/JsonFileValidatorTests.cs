using Buelo.Engine.Validators;

namespace Buelo.Tests.Engine;

public class JsonFileValidatorTests
{
    private readonly JsonFileValidator _validator = new();

    [Fact]
    public async Task Validate_ValidJson_ReturnsNoErrors()
    {
        var result = await _validator.ValidateAsync("""{ "name": "Alice", "age": 30 }""");

        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Validate_InvalidJson_ReturnsErrorWithLineNumber()
    {
        var result = await _validator.ValidateAsync("{ name: Alice }");

        Assert.False(result.Valid);
        Assert.Single(result.Errors);
        Assert.Equal("error", result.Errors[0].Severity);
        Assert.True(result.Errors[0].Line >= 1);
    }

    [Fact]
    public async Task Validate_EmptyString_ReturnsError()
    {
        var result = await _validator.ValidateAsync(string.Empty);

        Assert.False(result.Valid);
        Assert.Single(result.Errors);
        Assert.Equal("error", result.Errors[0].Severity);
    }
}
