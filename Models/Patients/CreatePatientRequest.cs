namespace Medpointe.Models.Patients;

public sealed class CreatePatientRequest
{
    public required string FirstName { get; init; }
    public string? MiddleName { get; init; }
    public required string LastName { get; init; }
    public string? Suffix { get; init; }
    public string? Nickname { get; init; }

    public DateTime DateOfBirth { get; init; }
    public required string SexAtBirth { get; init; }
    public string? GenderIdentity { get; init; }
    public string? Pronouns { get; init; }

    public string? MaritalStatus { get; init; }
    public string? EmploymentStatus { get; init; }
    public string? PreferredLanguage { get; init; }
    public string? Ethnicity { get; init; }

    public string? Classification { get; init; }
    public string? Category { get; init; }
    public string? Stage { get; init; }

    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? HomePhone { get; init; }
    public string? WorkPhone { get; init; }
    public string? MobilePhone { get; init; }
    public string? Email { get; init; }
    public string? CommunicationPreference { get; init; }
}

public sealed class CreatePatientResult
{
    public PatientModel? Patient { get; init; }
    public List<PatientModel> Duplicates { get; init; } = [];
    public string? ErrorMessage { get; init; }

    public bool Created => Patient is not null;
    public bool HasDuplicates => Duplicates.Count > 0;
}
