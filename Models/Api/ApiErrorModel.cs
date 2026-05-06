namespace Medpointe.Models.Api;
public class ApiErrorModel
{
    public required string Title { get; init; }
    public string? Message { get; init; }
    public string? Code { get; init; }
}
