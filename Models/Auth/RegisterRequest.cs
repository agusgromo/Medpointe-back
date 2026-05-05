namespace Medpointe.Models.Auth;
public class RegisterRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}
