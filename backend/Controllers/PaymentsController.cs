using Microsoft.AspNetCore.Mvc;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Controllers
{
    [Route("api/payments")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public PaymentsController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook([FromBody] PaymentWebhookDTO payload)
        {
            // Optionally validate signature header
            var sigHeader = Request.Headers["X-Webhook-Signature"].FirstOrDefault();
            var expected = _config["Payments:WebhookSecret"];
            if (!string.IsNullOrEmpty(expected) && string.IsNullOrEmpty(sigHeader))
            {
                return BadRequest(new { message = "Missing signature" });
            }

            // Find order
            var order = (Order?)null;

            if (payload.OrderId.HasValue)
            {
                order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == payload.OrderId.Value);
            }

            if (order == null && !string.IsNullOrEmpty(payload.PaymentId))
            {
                order = await _context.Orders.FirstOrDefaultAsync(o => o.GatewayPaymentId == payload.PaymentId);
            }

            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            // Map status
            order.PaymentStatus = payload.Status.ToLower();
            if (order.PaymentStatus == "paid")
            {
                order.PaidAt = DateTime.UtcNow;
                order.Status = "processing";
            }

            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
