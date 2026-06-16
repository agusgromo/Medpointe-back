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
    public string? HomePhone { get; init; }
    public string? WorkPhone { get; init; }
    public string? MobilePhone { get; init; }
    public string? Email { get; init; }
    public string? BillingStatus { get; init; }
    public DateTime? LastVisitDate { get; init; }
    public DateTime? LastViewedAt { get; init; }
}

public sealed class PatientSearchRequest
{
    public string? Search { get; init; }
    public string? Account { get; init; }
    public string? LastName { get; init; }
    public string? FirstName { get; init; }
    public string? History { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public DateTime? LastTreatmentDate { get; init; }
    public string? HomePhone { get; init; }
    public string? WorkPhone { get; init; }
    public string? CellPhone { get; init; }
    public string? InsurancePlan { get; init; }
    public string? InsuranceCarrier { get; init; }
    public long? ProviderId { get; init; }
    public string? BillingStatus { get; init; }
}

public sealed class PatientSearchOptionsModel
{
    public List<PatientLookupOptionModel> Providers { get; init; } = [];
    public List<PatientStatusOptionModel> BillingStatuses { get; init; } = [];
}

public sealed class PatientLookupOptionModel
{
    public long Id { get; init; }
    public required string Name { get; init; }
}

public sealed class PatientStatusOptionModel
{
    public required string Value { get; init; }
    public required string Label { get; init; }
}

public sealed class UpdatePatientAlertRequest
{
    public string? Alert { get; init; }
}
