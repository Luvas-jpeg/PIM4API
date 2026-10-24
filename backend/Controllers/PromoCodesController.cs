using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Services;

namespace EquipamentosMedicosApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PromoCodesController : ControllerBase
    {
        private readonly PromoCodeService _promoCodeService;

        public PromoCodesController(PromoCodeService promoCodeService)
        {
            _promoCodeService = promoCodeService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var promoCodes = await _promoCodeService.GetAllAsync();

            return Ok(promoCodes);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetById(int id)
        {
            var promoCode = await _promoCodeService.GetByIdAsync(id);

            if (promoCode == null)
            {
                return NotFound(new { message = "Codigo promocional nao encontrado." });
            }

            return Ok(promoCode);
        }

        [HttpGet("validate")]
        public async Task<IActionResult> Validate([FromQuery] string code)
        {
            var result = await _promoCodeService.ValidateAsync(code);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Data);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] PromoCodeRequestDTO request)
        {
            var result = await _promoCodeService.CreateAsync(request);

            if (!result.Success)
            {
                if (IsConflict(result.Error))
                {
                    return Conflict(new { message = result.Error });
                }

                return BadRequest(new { message = result.Error });
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] PromoCodeRequestDTO request)
        {
            var result = await _promoCodeService.UpdateAsync(id, request);

            if (!result.Success)
            {
                if (IsNotFound(result.Error))
                {
                    return NotFound(new { message = result.Error });
                }

                if (IsConflict(result.Error))
                {
                    return Conflict(new { message = result.Error });
                }

                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Data);
        }
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _promoCodeService.DeleteAsync(id);

            if (!deleted)
            {
                return await _promoCodeService.GetByIdAsync(id) == null
                    ? NotFound(new { message = "Codigo promocional nao encontrado." })
                    : Conflict(new { message = "Cupom utilizado em pedidos nao pode ser excluido. Desative o cupom." });
            }

            return NoContent();
        }

        private static bool IsConflict(string? error)
        {
            return error?.Contains("cadastrado", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool IsNotFound(string? error)
        {
            return error?.Contains("nao encontrado", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
