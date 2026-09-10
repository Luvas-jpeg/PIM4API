using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Services;

namespace EquipamentosMedicosApi.Controllers
{
    [Route("api/payments")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly IConfiguration _config;

        public PaymentsController(OrderService orderService, IConfiguration config)
        {
            _orderService = orderService;
            _config = config;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            var sigHeader = Request.Headers["X-Webhook-Signature"].FirstOrDefault();
            var expected = _config["Payments:WebhookSecret"];
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            if (!string.IsNullOrEmpty(expected) &&
                (string.IsNullOrWhiteSpace(sigHeader) ||
                 !IsValidSignature(rawBody, sigHeader, expected)))
            {
                return Unauthorized(new { message = "Invalid webhook signature" });
            }

            PaymentWebhookDTO? payload;
            try
            {
                payload = System.Text.Json.JsonSerializer.Deserialize<PaymentWebhookDTO>(rawBody);
            }
            catch (System.Text.Json.JsonException)
            {
                return BadRequest(new { message = "Invalid webhook payload." });
            }

            if (payload == null)
                return BadRequest(new { message = "Invalid webhook payload." });

            if (!payload.OrderId.HasValue)
                return BadRequest(new { message = "OrderId is required." });

            var result = await _orderService.ProcessPaymentWebhookAsync(
                payload.OrderId.Value,
                payload.PaymentId,
                payload.Status);

            if (!result.Success)
            {
                if (result.Error == "Pedido nao encontrado.")
                    return NotFound(new { message = result.Error });

                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Data);
        }

        private static bool IsValidSignature(string rawBody, string received, string secret)
        {
            var provided = received.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
                ? received["sha256=".Length..]
                : received;
            var expected = Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody)))
                .ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided.Trim().ToLowerInvariant()));
        }
    }
}
