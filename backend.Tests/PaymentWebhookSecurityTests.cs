using System.Text;
using EquipamentosMedicosApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace backend.Tests;

public sealed class PaymentWebhookSecurityTests
{
    [Fact]
    public async Task WebhookRejectsRequestWhenSecretIsNotConfigured()
    {
        var controller = CreateController(new Dictionary<string, string?>(), "{}", null);

        var result = await controller.Webhook();

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=invalid-signature")]
    public async Task WebhookRejectsMissingOrInvalidSignature(string? signature)
    {
        var controller = CreateController(
            new Dictionary<string, string?> { ["Payments:WebhookSecret"] = "webhook-test-secret" },
            "{\"eventId\":\"evt-1\",\"orderId\":1,\"paymentId\":\"pay-1\",\"status\":\"paid\"}",
            signature);

        var result = await controller.Webhook();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private static PaymentsController CreateController(
        Dictionary<string, string?> settings,
        string body,
        string? signature)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (signature != null)
        {
            httpContext.Request.Headers["X-Webhook-Signature"] = signature;
        }

        return new PaymentsController(null!, configuration, null!)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }
}