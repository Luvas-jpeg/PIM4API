using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class AuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;

    public AuthService(AppDbContext context, TokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<AuthServiceResult<int>> RegisterAsync(RegistroDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || request.Nome.Length > 120 ||
            string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 ||
            string.IsNullOrWhiteSpace(request.Cpf) || request.Cpf.Length > 14 ||
            string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Length > 20)
        {
            return AuthServiceResult<int>.Fail("Confira os dados informados e tente novamente.");
        }

        var email = request.Email.Trim().ToLower();
        if (!IsValidEmail(email))
        {
            return AuthServiceResult<int>.Fail("E-mail invalido.");
        }

        var emailExists = await _context.Users.AnyAsync(user => user.Email == email);

        if (emailExists)
        {
            return AuthServiceResult<int>.Fail("E-mail já cadastrado.");
        }

        if (!IsValidCpf(request.Cpf))
        {
            return AuthServiceResult<int>.Fail("CPF inválido.");
        }

        if (!IsValidPassword(request.Senha))
        {
            return AuthServiceResult<int>.Fail(
                "A senha deve ter ao menos 12 caracteres, letras maiusculas, minusculas e numeros.");
        }

        var user = new User
        {
            Nome = request.Nome.Trim(),
            Email = email,
            Cpf = request.Cpf.Trim(),
            Phone = request.Phone.Trim(),
            SenhaHash = BCrypt.Net.BCrypt.HashPassword(request.Senha),
            Role = "Cliente"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return AuthServiceResult<int>.Ok(user.ID);
    }

    public async Task<AuthServiceResult<LoginResponse>> LoginAsync(LoginDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 ||
            string.IsNullOrWhiteSpace(request.Senha) || request.Senha.Length > 128)
        {
            return AuthServiceResult<LoginResponse>.Fail("E-mail ou senha invÃ¡lidos.");
        }

        var email = request.Email.Trim().ToLower();
        if (!IsValidEmail(email))
        {
            return AuthServiceResult<LoginResponse>.Fail("E-mail ou senha invÃ¡lidos.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(user => user.Email == email);

        if (user == null)
        {
            return AuthServiceResult<LoginResponse>.Fail("E-mail ou senha inválidos.");
        }

        var now = DateTime.UtcNow;
        if (user.LockoutEnd.HasValue && user.LockoutEnd > now)
        {
            return AuthServiceResult<LoginResponse>.Fail("Credenciais invalidas.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Senha, user.SenhaHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                user.FailedLoginAttempts = 0;
                user.LockoutEnd = now.Add(LockoutDuration);
            }

            await _context.SaveChangesAsync();
            return AuthServiceResult<LoginResponse>.Fail("Credenciais invalidas.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;

        var accessToken = _tokenService.GenerateToken(user);

        // create refresh token
        var refreshTokenValue = GenerateRefreshTokenValue();
        var refresh = new RefreshToken
        {
            Token = HashToken(refreshTokenValue),
            UserId = user.ID,
            ExpiresAt = now.Add(RefreshTokenLifetime)
        };

        _context.RefreshTokens.Add(refresh);
        await _context.SaveChangesAsync();

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresIn = _tokenService.GetAccessTokenLifetimeSeconds(),
            User = ToResponse(user)
        };

        return AuthServiceResult<LoginResponse>.Ok(response);
    }

    public async Task<AuthServiceResult<LoginResponse>> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthServiceResult<LoginResponse>.Fail("Refresh token invalido ou expirado.");
        }

        var tokenHash = HashToken(refreshToken);
        var token = await _context.RefreshTokens.Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == tokenHash || rt.Token == refreshToken);

        if (token == null || !token.IsActive)
        {
            return AuthServiceResult<LoginResponse>.Fail("Refresh token inválido ou expirado.");
        }

        // rotate token: revoke old and create new
        token.Token = tokenHash;
        token.RevokedAt = DateTime.UtcNow;

        var newRefreshValue = GenerateRefreshTokenValue();
        token.ReplacedByToken = HashToken(newRefreshValue);

        var newRefresh = new RefreshToken
        {
            Token = HashToken(newRefreshValue),
            UserId = token.UserId,
            ExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime)
        };

        _context.RefreshTokens.Add(newRefresh);
        await _context.SaveChangesAsync();

        var access = _tokenService.GenerateToken(token.User!);

        var response = new LoginResponse
        {
            AccessToken = access,
            RefreshToken = newRefreshValue,
            ExpiresIn = _tokenService.GetAccessTokenLifetimeSeconds(),
            User = ToResponse(token.User!)
        };

        return AuthServiceResult<LoginResponse>.Ok(response);
    }

    public async Task<bool> LogoutAsync(int userId)
    {
        var tokens = await _context.RefreshTokens.Where(rt => rt.UserId == userId && rt.RevokedAt == null).ToListAsync();

        foreach (var t in tokens)
            t.RevokedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    private static string GenerateRefreshTokenValue()
    {
        var bytes = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public async Task<AuthServiceResult<UserResponseDTO>> UpdateProfileAsync(int userId, UpdateProfileDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || request.Nome.Length > 120 ||
            string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254 ||
            string.IsNullOrWhiteSpace(request.Cpf) || request.Cpf.Length > 14 ||
            string.IsNullOrWhiteSpace(request.Phone) || request.Phone.Length > 20 ||
            (request.Street?.Length ?? 0) > 160 || (request.Number?.Length ?? 0) > 20 ||
            (request.Complement?.Length ?? 0) > 120 || (request.Neighborhood?.Length ?? 0) > 120 ||
            (request.City?.Length ?? 0) > 120 || (request.State?.Length ?? 0) > 2 ||
            (request.ZipCode?.Length ?? 0) > 10)
        {
            return AuthServiceResult<UserResponseDTO>.Fail("Confira os dados informados e tente novamente.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(user => user.ID == userId);

        if (user == null)
        {
            return AuthServiceResult<UserResponseDTO>.Fail("Usuário não encontrado.");
        }

        var email = request.Email.Trim().ToLower();
        if (!IsValidEmail(email))
        {
            return AuthServiceResult<UserResponseDTO>.Fail("E-mail invalido.");
        }

        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(email))
        {
            return AuthServiceResult<UserResponseDTO>.Fail("Nome e e-mail são obrigatórios.");
        }

        if (!IsValidCpf(request.Cpf))
        {
            return AuthServiceResult<UserResponseDTO>.Fail("CPF inválido.");
        }

        var duplicatedEmail = await _context.Users
            .AnyAsync(otherUser => otherUser.ID != userId && otherUser.Email == email);

        if (duplicatedEmail)
        {
            return AuthServiceResult<UserResponseDTO>.Fail("E-mail já cadastrado.");
        }

        user.Nome = request.Nome.Trim();
        user.Email = email;
        user.Cpf = request.Cpf.Trim();
        user.Phone = request.Phone.Trim();
        user.Street = request.Street?.Trim() ?? string.Empty;
        user.Number = request.Number?.Trim() ?? string.Empty;
        user.Complement = request.Complement?.Trim() ?? string.Empty;
        user.Neighborhood = request.Neighborhood?.Trim() ?? string.Empty;
        user.City = request.City?.Trim() ?? string.Empty;
        user.State = request.State?.Trim() ?? string.Empty;
        user.ZipCode = request.ZipCode?.Trim() ?? string.Empty;

        await _context.SaveChangesAsync();

        return AuthServiceResult<UserResponseDTO>.Ok(ToResponse(user));
    }

    private static UserResponseDTO ToResponse(User user)
    {
        return new UserResponseDTO
        {
            Id = user.ID,
            Nome = user.Nome,
            Email = user.Email,
            Cpf = user.Cpf,
            Phone = user.Phone,
            Street = user.Street,
            Number = user.Number,
            Complement = user.Complement,
            Neighborhood = user.Neighborhood,
            City = user.City,
            State = user.State,
            ZipCode = user.ZipCode,
            Role = user.Role
        };
    }

    private static bool IsValidPassword(string password)
    {
        return !string.IsNullOrEmpty(password) && password.Length >= 12 && password.Length <= 128 &&
               password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);
    }

    private static bool IsValidEmail(string email)
    {
        return MailAddress.TryCreate(email, out var parsed) &&
               string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidCpf(string cpf)
    {
        var digits = new string(cpf.Where(char.IsDigit).ToArray());

        if (digits.Length != 11 || digits.Distinct().Count() == 1)
        {
            return false;
        }

        static int CalculateDigit(string value, int factor)
        {
            var total = 0;

            foreach (var digit in value)
            {
                total += (digit - '0') * factor--;
            }

            var rest = (total * 10) % 11;

            return rest == 10 ? 0 : rest;
        }

        var firstDigit = CalculateDigit(digits[..9], 10);
        var secondDigit = CalculateDigit(digits[..10], 11);

        return firstDigit == digits[9] - '0'
            && secondDigit == digits[10] - '0';
    }
}
