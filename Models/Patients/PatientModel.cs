namespace Medpointe.Models.Patients;
public class PatientModel
{
    public long Id { get; init; }
    public required string FirstName { get; init; }
    public string? MiddleName { get; init; }
    public required string LastName { get; init; }
    public DateTime DateOfBirth { get; init; }
    public required string SexAtBirth { get; init; }
    public string? PrimaryProviderName { get; init; }
    public string? PrimaryLocationName { get; init; }
    public string? MobilePhone { get; init; }
    public string? Email { get; init; }
}
