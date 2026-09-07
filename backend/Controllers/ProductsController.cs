using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Services;

namespace EquipamentosMedicosApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly ProductService _productService;

        public ProductsController(ProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? tipo)
        {
            var products = await _productService.GetAllAsync(tipo);
            return Ok(products);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _productService.GetByIdAsync(id);

            if (product == null)
                return NotFound(new { message = "Produto não encontrado." });

            return Ok(product);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] ProductRequestDTO request)
        {
            var validation = _productService.ValidateRequest(request);

            if (validation != null)
                return BadRequest(new { message = validation });

            var product = await _productService.CreateAsync(request);

            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(int id, [FromBody] ProductRequestDTO request)
        {
            var validation = _productService.ValidateRequest(request);

            if (validation != null)
                return BadRequest(new { message = validation });

            var product = await _productService.UpdateAsync(id, request);

            if (product == null)
                return NotFound(new { message = "Produto não encontrado." });

            return Ok(product);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            if (await _productService.HasFinancialHistoryAsync(id))
            {
                return Conflict(new
                {
                    message = "Produto com historico nao pode ser excluido. Arquive o curso em vez de remove-lo."
                });
            }

            var deleted = await _productService.DeleteAsync(id);

            if (!deleted)
                return NotFound(new { message = "Produto não encontrado." });

            return NoContent();
        }
    }
}