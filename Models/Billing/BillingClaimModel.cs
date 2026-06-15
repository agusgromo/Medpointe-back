namespace Medpointe.Models.Billing;

public sealed class BillingClaimQuery
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? BillingStage { get; init; }
    public DateTime? ServiceDateFrom { get; init; }
    public DateTime? ServiceDateTo { get; init; }
}

public class BillingClaimSummaryModel
{
    public long Id { get; init; }
    public required string ClaimNumber { get; init; }
    public long PatientId { get; init; }
    public required string PatientName { get; init; }
    public DateTime ServiceDate { get; init; }
    public required string Status { get; init; }
    public required string BillingStage { get; init; }
    public string? PrimaryInsuranceName { get; init; }
    public decimal TotalCharge { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal TotalAdjustment { get; init; }
    public decimal InsuranceBalance { get; init; }
    public decimal PatientBalance { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class BillingClaimDetailModel : BillingClaimSummaryModel
{
    public long? VisitId { get; init; }
    public long? AppointmentId { get; init; }
    public long? InsurancePolicyId { get; init; }
    public string? ProviderName { get; init; }
    public string? LocationName { get; init; }
    public string? Note { get; init; }
    public List<BillingClaimDiagnosisModel> Diagnoses { get; init; } = [];
    public List<BillingClaimLineModel> Lines { get; init; } = [];
}

public sealed class BillingClaimDiagnosisModel
{
    public long Id { get; init; }
    public short Sequence { get; init; }
    public required string DiagnosisCode { get; init; }
    public string? Description { get; init; }
}

public sealed class BillingClaimLineModel
{
    public long Id { get; init; }
    public DateTime ServiceDate { get; init; }
    public required string ProcedureCode { get; init; }
    public required string Description { get; init; }
    public decimal Units { get; init; }
    public decimal ChargeAmount { get; init; }
    public decimal AllowedAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal AdjustmentAmount { get; init; }
    public decimal PatientResponsibilityAmount { get; init; }
    public decimal InsuranceBalance { get; init; }
    public decimal PatientBalance { get; init; }
    public string? DiagnosisPointer { get; init; }
}

public sealed class CreateBillingClaimRequest
{
    public long PatientId { get; init; }
    public long? VisitId { get; init; }
    public long? AppointmentId { get; init; }
    public long? InsurancePolicyId { get; init; }
    public DateTime ServiceDate { get; init; }
    public string? Status { get; init; }
    public string? BillingStage { get; init; }
    public string? PrimaryDiagnosisCode { get; init; }
    public string? PrimaryDiagnosisDescription { get; init; }
    public string? Note { get; init; }
    public List<CreateBillingClaimLineRequest> Lines { get; init; } = [];
}

public sealed class CreateBillingClaimLineRequest
{
    public DateTime? ServiceDate { get; init; }
    public required string ProcedureCode { get; init; }
    public required string Description { get; init; }
    public decimal Units { get; init; } = 1;
    public decimal ChargeAmount { get; init; }
    public string? DiagnosisPointer { get; init; }
}

public sealed class CreateBillingClaimResult
{
    public BillingClaimDetailModel? Claim { get; init; }
    public string? ErrorMessage { get; init; }
    public bool Created => Claim is not null;
}