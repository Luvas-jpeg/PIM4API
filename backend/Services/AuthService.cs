using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class AuthService
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;

    public AuthService(AppDbContext context, TokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<AuthServiceResult<int>> RegisterAsync(RegistroDTO request)
    {
        var email = request.Email.Trim().ToLower();

        var emailExists = await _context.Users.AnyAsync(user => user.Email == email);

        if (emailExists)
        {
            return AuthServiceResult<int>.Fail("E-mail já cadastrado.");
        }

        if (!IsValidCpf(request.Cpf))
        {
            return AuthServiceResult<int>.Fail("CPF inválido.");
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
        var email = request.Email.Trim().ToLower();

        var user = await _context.Users.FirstOrDefaultAsync(user => user.Email == email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Senha, user.SenhaHash))
        {
            return AuthServiceResult<LoginResponse>.Fail("E-mail ou senha inválidos.");
        }

        var accessToken = _tokenService.GenerateToken(user);

        // create refresh token
        var refreshTokenValue = GenerateRefreshTokenValue();
        var refresh = new RefreshToken
        {
            Token = refreshTokenValue,
            UserId = user.ID,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _context.RefreshTokens.Add(refresh);
        await _context.SaveChangesAsync();

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresIn = (int)TimeSpan.FromHours(24).TotalSeconds,
            User = ToResponse(user)
        };

        return AuthServiceResult<LoginResponse>.Ok(response);
    }

    public async Task<AuthServiceResult<LoginResponse>> RefreshAsync(string refreshToken)
    {
        var token = await _context.RefreshTokens.Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (token == null || !token.IsActive)
        {
            return AuthServiceResult<LoginResponse>.Fail("Refresh token inválido ou expirado.");
        }

        // rotate token: revoke old and create new
        token.RevokedAt = DateTime.UtcNow;

        var newRefreshValue = GenerateRefreshTokenValue();
        token.ReplacedByToken = newRefreshValue;

        var newRefresh = new RefreshToken
        {
            Token = newRefreshValue,
            UserId = token.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _context.RefreshTokens.Add(newRefresh);
        await _context.SaveChangesAsync();

        var access = _tokenService.GenerateToken(token.User!);

        var response = new LoginResponse
        {
            AccessToken = access,
            RefreshToken = newRefreshValue,
            ExpiresIn = (int)TimeSpan.FromHours(24).TotalSeconds,
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

    public async Task<AuthServiceResult<UserResponseDTO>> UpdateProfileAsync(int userId, UpdateProfileDTO request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(user => user.ID == userId);

        if (user == null)
        {
            return AuthServiceResult<UserResponseDTO>.Fail("Usuário não encontrado.");
        }

        var email = request.Email.Trim().ToLower();

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
        user.Street = request.Street.Trim();
        user.Number = request.Number.Trim();
        user.Complement = request.Complement.Trim();
        user.Neighborhood = request.Neighborhood.Trim();
        user.City = request.City.Trim();
        user.State = request.State.Trim();
        user.ZipCode = request.ZipCode.Trim();

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