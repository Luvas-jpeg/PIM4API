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
        public async Task<IActionResult> Webhook([FromBody] PaymentWebhookDTO payload)
        {
            var sigHeader = Request.Headers["X-Webhook-Signature"].FirstOrDefault();
            var expected = _config["Payments:WebhookSecret"];
            if (!string.IsNullOrEmpty(expected) && string.IsNullOrEmpty(sigHeader))
            {
                return BadRequest(new { message = "Missing signature" });
            }

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
    }
}
