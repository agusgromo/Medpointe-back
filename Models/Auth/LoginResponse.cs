namespace Medpointe.Models.Auth;
public class LoginResponse
{
    public required string AccessToken { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public required string Username { get; init; }
}