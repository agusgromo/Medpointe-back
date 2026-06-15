using Dapper;
using Medpointe.Data;
using Medpointe.Models.Billing;

namespace Medpointe.Repositories;

public sealed class BillingClaimsRepository(DatabaseClient databaseClient)
{
    public async Task<List<BillingClaimSummaryModel>> Search(BillingClaimQuery query, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c."id" AS Id,
                c."claim_number" AS ClaimNumber,
                c."patient_id" AS PatientId,
                CONCAT_WS(' ', p."first_name", p."middle_name", p."last_name") AS PatientName,
                c."service_date" AS ServiceDate,
                c."status" AS Status,
                c."billing_stage" AS BillingStage,
                ic."name" AS PrimaryInsuranceName,
                c."total_charge" AS TotalCharge,
                c."total_paid" AS TotalPaid,
                c."total_adjustment" AS TotalAdjustment,
                c."insurance_balance" AS InsuranceBalance,
                c."patient_balance" AS PatientBalance,
                c."updated_at" AS UpdatedAt
            FROM billing_claims c
            JOIN patients p ON p."id" = c."patient_id"
            LEFT JOIN patient_insurance_policies pip ON pip."id" = c."insurance_policy_id"
            LEFT JOIN insurance_carriers ic ON ic."id" = pip."carrier_id"
            WHERE (@Status IS NULL OR c."status" = @Status)
              AND (@BillingStage IS NULL OR c."billing_stage" = @BillingStage)
              AND (@ServiceDateFrom IS NULL OR c."service_date" >= @ServiceDateFrom)
              AND (@ServiceDateTo IS NULL OR c."service_date" <= @ServiceDateTo)
              AND (
                  @Search IS NULL
                  OR c."claim_number" ILIKE @Search
                  OR CAST(c."patient_id" AS TEXT) = @ExactSearch
                  OR p."first_name" ILIKE @Search
                  OR p."last_name" ILIKE @Search
                  OR CONCAT_WS(' ', p."first_name", p."middle_name", p."last_name") ILIKE @Search
              )
            ORDER BY c."updated_at" DESC, c."id" DESC
            LIMIT 100;
            """;

        string? trimmedSearch = BlankToNull(query.Search);

        return await databaseClient.GetListByQuery<BillingClaimSummaryModel>(
            sql,
            new
            {
                Search = trimmedSearch is null ? null : $"{trimmedSearch}%",
                ExactSearch = trimmedSearch,
                Status = BlankToNull(query.Status),
                BillingStage = BlankToNull(query.BillingStage),
                ServiceDateFrom = query.ServiceDateFrom?.Date,
                ServiceDateTo = query.ServiceDateTo?.Date
            },
            cancellationToken);
    }

    public async Task<BillingClaimDetailModel?> GetById(long claimId, CancellationToken cancellationToken)
    {
        const string claimSql = """
            SELECT
                c."id" AS Id,
                c."claim_number" AS ClaimNumber,
                c."patient_id" AS PatientId,
                c."visit_id" AS VisitId,
                c."appointment_id" AS AppointmentId,
                c."insurance_policy_id" AS InsurancePolicyId,
                CONCAT_WS(' ', p."first_name", p."middle_name", p."last_name") AS PatientName,
                c."service_date" AS ServiceDate,
                c."status" AS Status,
                c."billing_stage" AS BillingStage,
                ic."name" AS PrimaryInsuranceName,
                pr."name" AS ProviderName,
                l."name" AS LocationName,
                c."total_charge" AS TotalCharge,
                c."total_paid" AS TotalPaid,
                c."total_adjustment" AS TotalAdjustment,
                c."insurance_balance" AS InsuranceBalance,
                c."patient_balance" AS PatientBalance,
                c."note" AS Note,
                c."updated_at" AS UpdatedAt
            FROM billing_claims c
            JOIN patients p ON p."id" = c."patient_id"
            LEFT JOIN patient_insurance_policies pip ON pip."id" = c."insurance_policy_id"
            LEFT JOIN insurance_carriers ic ON ic."id" = pip."carrier_id"
            LEFT JOIN providers pr ON pr."id" = c."provider_id"
            LEFT JOIN locations l ON l."id" = c."location_id"
            WHERE c."id" = @ClaimId;
            """;

        BillingClaimDetailModel? claim = await databaseClient.GetOneByQuery<BillingClaimDetailModel>(
            claimSql,
            new { ClaimId = claimId },
            cancellationToken);

        if (claim is null)
        {
            return null;
        }

        const string diagnosesSql = """
            SELECT
                "id" AS Id,
                "sequence" AS Sequence,
                "diagnosis_code" AS DiagnosisCode,
                "description" AS Description
            FROM billing_claim_diagnoses
            WHERE "claim_id" = @ClaimId
            ORDER BY "sequence", "id";
            """;

        const string linesSql = """
            SELECT
                "id" AS Id,
                "service_date" AS ServiceDate,
                "procedure_code" AS ProcedureCode,
                "description" AS Description,
                "units" AS Units,
                "charge_amount" AS ChargeAmount,
                "allowed_amount" AS AllowedAmount,
                "paid_amount" AS PaidAmount,
                "adjustment_amount" AS AdjustmentAmount,
                "patient_responsibility_amount" AS PatientResponsibilityAmount,
                "insurance_balance" AS InsuranceBalance,
                "patient_balance" AS PatientBalance,
                "diagnosis_pointer" AS DiagnosisPointer
            FROM billing_claim_lines
            WHERE "claim_id" = @ClaimId
            ORDER BY "id";
            """;

        claim.Diagnoses.AddRange(await databaseClient.GetListByQuery<BillingClaimDiagnosisModel>(
            diagnosesSql,
            new { ClaimId = claimId },
            cancellationToken));

        claim.Lines.AddRange(await databaseClient.GetListByQuery<BillingClaimLineModel>(
            linesSql,
            new { ClaimId = claimId },
            cancellationToken));

        return claim;
    }

    public async Task<bool> PatientExists(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM patients
                WHERE "id" = @PatientId
            );
            """;

        return await databaseClient.GetOneByQuery<bool>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<long> Create(CreateBillingClaimRequest request, decimal totalCharge, CancellationToken cancellationToken)
    {
        return await databaseClient.ExecuteInTransaction(async (connection, transaction) =>
        {
            const string claimSql = """
                INSERT INTO billing_claims (
                    patient_id,
                    visit_id,
                    appointment_id,
                    insurance_policy_id,
                    service_date,
                    status,
                    billing_stage,
                    total_charge,
                    insurance_balance,
                    patient_balance,
                    note
                )
                VALUES (
                    @PatientId,
                    @VisitId,
                    @AppointmentId,
                    @InsurancePolicyId,
                    @ServiceDate,
                    @Status,
                    @BillingStage,
                    @TotalCharge,
                    @TotalCharge,
                    0,
                    @Note
                )
                RETURNING "id";
                """;

            long claimId = await connection.QuerySingleAsync<long>(new CommandDefinition(
                claimSql,
                new
                {
                    request.PatientId,
                    request.VisitId,
                    request.AppointmentId,
                    request.InsurancePolicyId,
                    ServiceDate = request.ServiceDate.Date,
                    request.Status,
                    request.BillingStage,
                    TotalCharge = totalCharge,
                    request.Note
                },
                transaction,
                cancellationToken: cancellationToken));

            const string lineSql = """
                INSERT INTO billing_claim_lines (
                    claim_id,
                    service_date,
                    procedure_code,
                    description,
                    units,
                    charge_amount,
                    insurance_balance,
                    patient_balance,
                    diagnosis_pointer
                )
                VALUES (
                    @ClaimId,
                    @ServiceDate,
                    @ProcedureCode,
                    @Description,
                    @Units,
                    @ChargeAmount,
                    @LineTotal,
                    0,
                    @DiagnosisPointer
                );
                """;

            foreach (CreateBillingClaimLineRequest line in request.Lines)
            {
                decimal units = line.Units == 0 ? 1 : line.Units;

                await connection.ExecuteAsync(new CommandDefinition(
                    lineSql,
                    new
                    {
                        ClaimId = claimId,
                        ServiceDate = (line.ServiceDate ?? request.ServiceDate).Date,
                        ProcedureCode = line.ProcedureCode,
                        line.Description,
                        Units = units,
                        line.ChargeAmount,
                        LineTotal = line.ChargeAmount * units,
                        line.DiagnosisPointer
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            if (!string.IsNullOrWhiteSpace(request.PrimaryDiagnosisCode))
            {
                const string diagnosisSql = """
                    INSERT INTO billing_claim_diagnoses (
                        claim_id,
                        sequence,
                        diagnosis_code,
                        description
                    )
                    VALUES (
                        @ClaimId,
                        1,
                        @DiagnosisCode,
                        @Description
                    );
                    """;

                await connection.ExecuteAsync(new CommandDefinition(
                    diagnosisSql,
                    new
                    {
                        ClaimId = claimId,
                        DiagnosisCode = request.PrimaryDiagnosisCode,
                        Description = request.PrimaryDiagnosisDescription
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            const string eventSql = """
                INSERT INTO billing_claim_events (
                    claim_id,
                    event_type,
                    to_status,
                    note
                )
                VALUES (
                    @ClaimId,
                    'created',
                    @Status,
                    'Claim created'
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                eventSql,
                new { ClaimId = claimId, request.Status },
                transaction,
                cancellationToken: cancellationToken));

            return claimId;
        }, cancellationToken);
    }

    private static string? BlankToNull(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}