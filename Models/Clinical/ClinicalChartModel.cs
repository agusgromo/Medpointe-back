using Medpointe.Models.Patients;
using Medpointe.Models.Schedule;

namespace Medpointe.Models.Clinical;

public sealed class ClinicalChartModel
{
    public required PatientActivityHeader Patient { get; init; }
    public PatientContactSummary? Contact { get; init; }
    public List<PatientPharmacySummary> Pharmacies { get; init; } = [];
    public List<InsurancePolicySummary> InsurancePolicies { get; init; } = [];
    public ScheduleAppointmentModel? Appointment { get; init; }
    public VisitSummary? CurrentVisit { get; init; }
    public List<VisitSummary> Encounters { get; init; } = [];
    public List<VisitDiagnosisSummary> EncounterDiagnoses { get; init; } = [];
    public List<PatientProblemSummary> Problems { get; init; } = [];
    public List<PatientAllergySummary> Allergies { get; init; } = [];
    public List<PatientMedicationSummary> Medications { get; init; } = [];
    public List<ClinicalOrderSummary> Orders { get; init; } = [];
    public List<ClinicalNoteEntry> ClinicalNotes { get; init; } = [];
    public List<PatientNoteSummary> PatientNotes { get; init; } = [];
    public List<EncounterFormSummary> EncounterForms { get; init; } = [];
}

public sealed class VisitDiagnosisSummary
{
    public long Id { get; init; }
    public long VisitId { get; init; }
    public short Sequence { get; init; }
    public long? PatientProblemId { get; init; }
    public string? DiagnosisCode { get; init; }
    public string? Description { get; init; }
}

public sealed class ClinicalNoteEntry
{
    public long Id { get; init; }
    public long? VisitId { get; init; }
    public required string NoteType { get; init; }
    public string? Title { get; init; }
    public required string Body { get; init; }
    public required string Status { get; init; }
    public DateTime? SignedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class EncounterFormSummary
{
    public long Id { get; init; }
    public long VisitId { get; init; }
    public required string FormCode { get; init; }
    public string? Section { get; init; }
    public bool Completed { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? DataPreview { get; init; }
}
