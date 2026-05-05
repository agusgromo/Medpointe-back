using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Medpointe.Models.Auth;
using Medpointe.Repositories;
using Microsoft.IdentityModel.Tokens;

namespace Medpointe.Services;

public sealed class AuthService(IConfiguration configuration, AuthRepository authRepository)
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        LoginRequest? user = await authRepository.GetByUsername(NormalizeUsername(request.Username), cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            return null;
        }

        return CreateToken(user.Username);
    }

    public async Task<LoginResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(request.Username);
        var existingUser = await authRepository.GetByUsername(normalizedUsername, cancellationToken);

        if (existingUser is not null)
        {
            return null;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        await authRepository.CreateUserAsync(normalizedUsername, passwordHash, cancellationToken);

        return CreateToken(normalizedUsername);
    }

    private LoginResponse CreateToken(string username)
    {
        IConfigurationSection jwtSection = configuration.GetSection("Jwt");
        string issuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
        string audience = jwtSection["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing.");
        string key = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");
        int expiresMinutes = int.TryParse(jwtSection["ExpiresMinutes"], out var minutes) ? minutes : 60;
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);

        Claim[]? claims =
        [
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new LoginResponse()
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAt,
            Username = username
        };
    }

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
}
