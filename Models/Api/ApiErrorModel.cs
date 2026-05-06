namespace Medpointe.Models.Api;
public class ApiError
{
    public string? Title { get; init; }
    public required string Message { get; init; }
    public string? Code { get; init; }
}