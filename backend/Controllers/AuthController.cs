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
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("cadastrar")]
        public async Task<IActionResult> Cadastrar([FromBody] RegistroDTO request)
        {
            var result = await _authService.RegisterAsync(request);

            if (!result.Success)
            {
                if (IsConflict(result.Error))
                {
                    return Conflict(new { message = result.Error });
                }

                return BadRequest(new { message = result.Error });
            }

            return Ok(new
            {
                message = "Usuario cadastrado com sucesso!",
                userId = result.Data
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO request)
        {
            var result = await _authService.LoginAsync(request);

            if (!result.Success)
            {
                return Unauthorized(new { message = result.Error });
            }

            return Ok(result.Data);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] DTOs.RefreshTokenRequestDTO request)
        {
            var result = await _authService.RefreshAsync(request.RefreshToken);

            if (!result.Success)
                return Unauthorized(new { message = result.Error });

            return Ok(result.Data);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = GetAuthenticatedUserId();

            if (userId == null)
                return Unauthorized();

            await _authService.LogoutAsync(userId.Value);

            return NoContent();
        }

        [HttpPut("perfil")]
        [Authorize]
        public async Task<IActionResult> AtualizarPerfil([FromBody] UpdateProfileDTO request)
        {
            var userId = GetAuthenticatedUserId();

            if (userId == null)
            {
                return Unauthorized();
            }

            var result = await _authService.UpdateProfileAsync(userId.Value, request);

            if (!result.Success)
            {
                if (IsConflict(result.Error))
                {
                    return Conflict(new { message = result.Error });
                }

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

        private static bool IsConflict(string? error)
        {
            return error?.Contains("cadastrado", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
