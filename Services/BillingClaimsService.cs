using Medpointe.Models.Billing;
using Medpointe.Repositories;

namespace Medpointe.Services;

public sealed class BillingClaimsService(BillingClaimsRepository billingClaimsRepository)
{
    private static readonly string[] ClaimStatuses =
    [
        "draft",
        "ready_to_bill",
        "submitted",
        "paid",
        "denied",
        "voided"
    ];

    private static readonly string[] BillingStages =
    [
        "charge_entry",
        "coding_review",
        "ready_to_bill",
        "submitted",
        "follow_up",
        "closed"
    ];

    public async Task<List<BillingClaimSummaryModel>> Search(BillingClaimQuery query, CancellationToken cancellationToken)
    {
        BillingClaimQuery normalizedQuery = new()
        {
            Search = BlankToNull(query.Search),
            Status = NormalizeToken(query.Status),
            BillingStage = NormalizeToken(query.BillingStage),
            ServiceDateFrom = query.ServiceDateFrom?.Date,
            ServiceDateTo = query.ServiceDateTo?.Date
        };

        return await billingClaimsRepository.Search(normalizedQuery, cancellationToken);
    }

    public async Task<BillingClaimDetailModel?> GetById(long claimId, CancellationToken cancellationToken)
    {
        if (claimId <= 0)
        {
            return null;
        }

        return await billingClaimsRepository.GetById(claimId, cancellationToken);
    }

    public async Task<CreateBillingClaimResult> Create(CreateBillingClaimRequest request, CancellationToken cancellationToken)
    {
        CreateBillingClaimRequest normalizedRequest = NormalizeCreateRequest(request);
        string? validationError = await ValidateCreateRequest(normalizedRequest, cancellationToken);

        if (validationError is not null)
        {
            return new CreateBillingClaimResult { ErrorMessage = validationError };
        }

        decimal totalCharge = normalizedRequest.Lines.Sum(line => line.ChargeAmount * line.Units);
        long claimId = await billingClaimsRepository.Create(normalizedRequest, totalCharge, cancellationToken);
        BillingClaimDetailModel? claim = await billingClaimsRepository.GetById(claimId, cancellationToken);

        return new CreateBillingClaimResult { Claim = claim };
    }

    private async Task<string?> ValidateCreateRequest(CreateBillingClaimRequest request, CancellationToken cancellationToken)
    {
        if (request.PatientId <= 0)
        {
            return "A patient is required for the claim.";
        }

        if (!await billingClaimsRepository.PatientExists(request.PatientId, cancellationToken))
        {
            return "The selected patient does not exist.";
        }

        if (request.ServiceDate == default)
        {
            return "Service date is required.";
        }

        if (request.ServiceDate.Date > DateTime.UtcNow.Date)
        {
            return "Service date cannot be in the future.";
        }

        if (!ClaimStatuses.Contains(request.Status))
        {
            return "Claim status is not valid.";
        }

        if (!BillingStages.Contains(request.BillingStage))
        {
            return "Billing stage is not valid.";
        }

        if (request.Lines.Count == 0)
        {
            return "At least one claim line is required.";
        }

        foreach (CreateBillingClaimLineRequest line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ProcedureCode))
            {
                return "Each claim line needs a procedure code.";
            }

            if (string.IsNullOrWhiteSpace(line.Description))
            {
                return "Each claim line needs a description.";
            }

            if (line.Units <= 0)
            {
                return "Claim line units must be greater than zero.";
            }

            if (line.ChargeAmount < 0)
            {
                return "Claim line charges cannot be negative.";
            }
        }

        return null;
    }

    private static CreateBillingClaimRequest NormalizeCreateRequest(CreateBillingClaimRequest request)
    {
        DateTime serviceDate = request.ServiceDate == default
            ? DateTime.UtcNow.Date
            : request.ServiceDate.Date;

        return new CreateBillingClaimRequest
        {
            PatientId = request.PatientId,
            VisitId = request.VisitId,
            AppointmentId = request.AppointmentId,
            InsurancePolicyId = request.InsurancePolicyId,
            ServiceDate = serviceDate,
            Status = NormalizeToken(request.Status) ?? "draft",
            BillingStage = NormalizeToken(request.BillingStage) ?? "charge_entry",
            PrimaryDiagnosisCode = BlankToNull(request.PrimaryDiagnosisCode)?.ToUpperInvariant(),
            PrimaryDiagnosisDescription = BlankToNull(request.PrimaryDiagnosisDescription),
            Note = BlankToNull(request.Note),
            Lines = [.. request.Lines.Select(line => new CreateBillingClaimLineRequest
            {
                ServiceDate = line.ServiceDate?.Date,
                ProcedureCode = RequiredTrim(line.ProcedureCode).ToUpperInvariant(),
                Description = RequiredTrim(line.Description),
                Units = line.Units,
                ChargeAmount = line.ChargeAmount,
                DiagnosisPointer = BlankToNull(line.DiagnosisPointer)?.ToUpperInvariant()
            })]
        };
    }

    private static string RequiredTrim(string? value) => value?.Trim() ?? string.Empty;

    private static string? BlankToNull(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeToken(string? value) =>
        BlankToNull(value)?.Replace('-', '_').ToLowerInvariant();
}