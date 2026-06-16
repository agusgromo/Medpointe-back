namespace Medpointe.Models.Patients;

public sealed class PatientActivityModel
{
    public required PatientActivityHeader Patient { get; init; }
    public PatientContactSummary? Contact { get; init; }
    public List<PatientPharmacySummary> Pharmacies { get; init; } = [];
    public List<InsurancePolicySummary> InsurancePolicies { get; init; } = [];
    public List<AppointmentSummary> Appointments { get; init; } = [];
    public List<VisitSummary> Visits { get; init; } = [];
    public List<PatientProblemSummary> Problems { get; init; } = [];
    public List<PatientAllergySummary> Allergies { get; init; } = [];
    public List<PatientMedicationSummary> Medications { get; init; } = [];
    public List<ClinicalOrderSummary> Orders { get; init; } = [];
    public List<PatientNoteSummary> Notes { get; init; } = [];
    public List<PatientTimelineItem> Timeline { get; init; } = [];
}

public sealed class PatientActivityHeader
{
    public long Id { get; init; }
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
    public long? PreferredLanguageId { get; init; }
    public string? PreferredLanguage { get; init; }
    public string? Ethnicity { get; init; }
    public required string Status { get; init; }
    public string? BillingStatus { get; init; }
    public string? Classification { get; init; }
    public string? Category { get; init; }
    public string? Stage { get; init; }
    public string? Alert { get; init; }
    public string? PrimaryProviderName { get; init; }
    public string? PrimaryLocationName { get; init; }
    public DateTime? LastVisitDate { get; init; }
    public DateTime? NextAppointmentStart { get; init; }
}

public sealed class PatientContactSummary
{
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? HomePhone { get; init; }
    public string? WorkPhone { get; init; }
    public string? MobilePhone { get; init; }
    public string? Email { get; init; }
    public string? CommunicationPreference { get; init; }
}

public sealed class PatientPharmacySummary
{
    public long Id { get; init; }
    public long PharmacyId { get; init; }
    public required string Type { get; init; }
    public short Priority { get; init; }
    public required string Name { get; init; }
    public string? DisplayName { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? PostalCode { get; init; }
    public string? Phone { get; init; }
}

public sealed class InsurancePolicySummary
{
    public long Id { get; init; }
    public short Priority { get; init; }
    public string? CarrierName { get; init; }
    public string? PayerId { get; init; }
    public string? MemberId { get; init; }
    public string? GroupNumber { get; init; }
    public string? GroupName { get; init; }
    public string? SubscriberName { get; init; }
    public DateTime? SubscriberDateOfBirth { get; init; }
    public string? RelationshipToPatient { get; init; }
    public DateTime? EffectiveDate { get; init; }
    public DateTime? ExpirationDate { get; init; }
    public decimal? Copay { get; init; }
    public bool IsActive { get; init; }
}

public sealed class AppointmentSummary
{
    public long Id { get; init; }
    public DateTime ScheduledStart { get; init; }
    public DateTime ScheduledEnd { get; init; }
    public required string Status { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public string? AppointmentTypeName { get; init; }
    public string? ProviderName { get; init; }
    public string? LocationName { get; init; }
    public string? RoomName { get; init; }
}

public sealed class VisitSummary
{
    public long Id { get; init; }
    public DateTime VisitDate { get; init; }
    public string? VisitType { get; init; }
    public required string Status { get; init; }
    public string? ChiefComplaint { get; init; }
    public string? ProviderName { get; init; }
    public string? NurseName { get; init; }
    public string? LocationName { get; init; }
    public string? SmokingStatus { get; init; }
    public decimal? SystolicBp { get; init; }
    public decimal? DiastolicBp { get; init; }
    public decimal? HeartRate { get; init; }
    public decimal? RespiratoryRate { get; init; }
    public decimal? TemperatureC { get; init; }
    public decimal? PulseOx { get; init; }
    public decimal? HeightCm { get; init; }
    public decimal? WeightKg { get; init; }
    public decimal? Bmi { get; init; }
    public short? PainScore { get; init; }
}

public sealed class PatientProblemSummary
{
    public long Id { get; init; }
    public string? DiagnosisCode { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public DateTime? OnsetDate { get; init; }
    public DateTime? ResolvedDate { get; init; }
    public string? Note { get; init; }
}

public sealed class PatientAllergySummary
{
    public long Id { get; init; }
    public required string Allergen { get; init; }
    public string? AllergenType { get; init; }
    public string? Reaction { get; init; }
    public string? Severity { get; init; }
    public required string Status { get; init; }
    public string? Note { get; init; }
}

public sealed class PatientMedicationSummary
{
    public long Id { get; init; }
    public string? VisitType { get; init; }
    public required string MedicationName { get; init; }
    public string? Strength { get; init; }
    public string? Dose { get; init; }
    public string? Route { get; init; }
    public string? Frequency { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public int? Refills { get; init; }
    public bool Controlled { get; init; }
    public required string Status { get; init; }
    public string? Instructions { get; init; }
    public string? Note { get; init; }
}

public sealed class ClinicalOrderSummary
{
    public long Id { get; init; }
    public long? VisitId { get; init; }
    public string? VisitType { get; init; }
    public string? OrderedByProviderName { get; init; }
    public required string OrderType { get; init; }
    public string? Code { get; init; }
    public required string Description { get; init; }
    public string? DiagnosisCode { get; init; }
    public string? Priority { get; init; }
    public required string Status { get; init; }
    public DateTime OrderedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? Note { get; init; }
}

public sealed class PatientNoteSummary
{
    public long Id { get; init; }
    public required string NoteType { get; init; }
    public required string Body { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class PatientTimelineItem
{
    public required string Type { get; init; }
    public required string Title { get; init; }
    public string? Detail { get; init; }
    public DateTime OccurredAt { get; init; }
    public string? Status { get; init; }
}
