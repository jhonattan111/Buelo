using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine.Validators;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class ValidateControllerTests
{
    private static ValidateController CreateController()
    {
        var registry = new FileValidatorRegistry(
        [
            new BueloDslValidator(),
            new JsonFileValidator(),
            new CsharpFileValidator()
        ]);
        return new ValidateController(registry);
    }

    [Fact]
    public async Task PostValidate_BueloExtension_RoutesToBueloDslValidator()
    {
        var controller = CreateController();

        var result = await controller.Validate(new FileValidateRequest
        {
            Extension = ".buelo",
            Content = "report title:\n  text: Hello"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<FileValidationResult>(ok.Value);
        Assert.True(validation.Valid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public async Task PostValidate_UnknownExtension_ReturnsInfoWarning()
    {
        var controller = CreateController();

        var result = await controller.Validate(new FileValidateRequest
        {
            Extension = ".xyz",
            Content = "anything"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<FileValidationResult>(ok.Value);
        Assert.True(validation.Valid); // no errors, just a warning
        Assert.Contains(validation.Warnings, w => w.Severity == "info");
    }

    [Fact]
    public async Task PostValidate_Always200_EvenWithErrors()
    {
        var controller = CreateController();

        // Invalid JSON — should still return 200 OK with errors in body.
        var result = await controller.Validate(new FileValidateRequest
        {
            Extension = ".json",
            Content = "{ invalid }"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<FileValidationResult>(ok.Value);
        Assert.False(validation.Valid);
        Assert.NotEmpty(validation.Errors);
    }
}
