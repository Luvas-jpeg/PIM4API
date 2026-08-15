using EquipamentosMedicosApi.DTOs;

namespace EquipamentosMedicosApi.Services;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }

    public UserResponseDTO User { get; set; } = new();
}