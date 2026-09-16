using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
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
        private readonly AppDbContext _context;

        public PaymentsController(OrderService orderService, IConfiguration config, AppDbContext context)
        {
            _orderService = orderService;
            _config = config;
            _context = context;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            var sigHeader = Request.Headers["X-Webhook-Signature"].FirstOrDefault();
            var expected = _config["Payments:WebhookSecret"];
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(expected))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Webhook secret is not configured." });
            }

            if (string.IsNullOrWhiteSpace(sigHeader) ||
                !IsValidSignature(rawBody, sigHeader, expected))
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

            if (string.IsNullOrWhiteSpace(payload.EventId))
                return BadRequest(new { message = "EventId is required." });

            if (!payload.OrderId.HasValue)
                return BadRequest(new { message = "OrderId is required." });

            var result = await _orderService.ProcessPaymentWebhookAsync(
                payload.EventId,
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

        [HttpGet("webhook-events")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetWebhookEvents([FromQuery] int? orderId = null)
        {
            var query = _context.PaymentWebhookEvents.AsNoTracking();

            if (orderId.HasValue)
            {
                query = query.Where(webhookEvent => webhookEvent.OrderId == orderId.Value);
            }

            var events = await query
                .OrderByDescending(webhookEvent => webhookEvent.ReceivedAt)
                .Take(100)
                .Select(webhookEvent => new PaymentWebhookEventResponseDTO
                {
                    Id = webhookEvent.Id,
                    EventId = webhookEvent.EventId,
                    OrderId = webhookEvent.OrderId,
                    PaymentId = webhookEvent.PaymentId,
                    Status = webhookEvent.Status,
                    ReceivedAt = webhookEvent.ReceivedAt,
                    ProcessedAt = webhookEvent.ProcessedAt
                })
                .ToListAsync();

            return Ok(events);
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
