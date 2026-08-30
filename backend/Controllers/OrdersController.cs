using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Services;

namespace EquipamentosMedicosApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly OrderService _orderService;

        public OrdersController(OrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDTO request)
        {
            var userId = GetAuthenticatedUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
            var result = await _orderService.CreateAsync(userId.Value, request, idempotencyKey);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.OrderId }, result.Data);
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetAuthenticatedUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var result = await _orderService.GetMyOrdersAsync(userId.Value);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _orderService.GetByIdAsync(userId.Value, id);
            return result.Success ? Ok(result.Data) : NotFound(new { message = result.Error });
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _orderService.CancelAsync(userId.Value, id);
            if (!result.Success)
                return BadRequest(new { message = result.Error });

            return Ok(result.Data);
        }

        [HttpGet("{id}/payment")]
        public async Task<IActionResult> GetPayment(int id)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _orderService.GetByIdAsync(userId.Value, id);
            if (!result.Success)
                return NotFound(new { message = result.Error });

            return Ok(new
            {
                orderId = result.Data!.Id,
                status = result.Data.PaymentStatus,
                paymentMethod = result.Data.PaymentMethod,
                gatewayPaymentId = result.Data.GatewayPaymentId,
                paidAt = result.Data.PaidAt,
                total = result.Data.Total
            });
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _orderService.GetAllAsync();

            return Ok(orders);
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateOrderStatusDTO request)
        {
            var result = await _orderService.UpdateStatusAsync(id, request);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Data);
        }

        private int? GetAuthenticatedUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)
                           ?? User.FindFirst("sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return null;
            }

            return userId;
        }
    }
}
