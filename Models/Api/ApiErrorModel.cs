using System.Text.Json.Serialization;

namespace Medpointe.Models.Api;
public class ApiError
{
    public string? Title { get; init; }
    public required string Message { get; init; }
    public string? Code { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; init; }
}
