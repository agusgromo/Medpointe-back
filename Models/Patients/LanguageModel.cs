namespace Medpointe.Models.Patients;

public sealed class LanguageModel
{
    public long Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? Hl7Code { get; init; }
}
