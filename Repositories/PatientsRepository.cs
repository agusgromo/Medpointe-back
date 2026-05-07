using Medpointe.Data;
using Medpointe.Models.Patients;

namespace Medpointe.Repositories;

public class PatientsRepository(DatabaseClient databaseClient)
{
    public async Task<List<PatientModel>> Search(string search, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p."id" AS Id,
                p."first_name" AS FirstName,
                p."middle_name" AS MiddleName,
                p."last_name" AS LastName,
                p."date_of_birth" AS DateOfBirth,
                p."sex_at_birth" AS SexAtBirth,
                pr."name" AS PrimaryProviderName,
                l."name" AS PrimaryLocationName,
                pc."mobile_phone" AS MobilePhone,
                pc."email" AS Email
            FROM patients
            p
            LEFT JOIN providers pr ON pr."id" = p."primary_provider_id"
            LEFT JOIN locations l ON l."id" = p."primary_location_id"
            LEFT JOIN LATERAL (
                SELECT "mobile_phone", "email"
                FROM patient_contacts
                WHERE "patient_id" = p."id"
                ORDER BY "id"
                LIMIT 1
            ) pc ON TRUE
            WHERE
                CAST(p."id" AS TEXT) = @ExactSearch
                OR p."first_name" ILIKE @Search
                OR p."last_name" ILIKE @Search
                OR CONCAT_WS(' ', p."first_name", p."middle_name", p."last_name") ILIKE @Search
            ORDER BY p."last_name", p."first_name"
            LIMIT 25;
            """;

        return await databaseClient.GetListByQuery<PatientModel>(
            sql,
            new { ExactSearch = search.Trim(), Search = $"{search.Trim()}%" },
            cancellationToken);
    }

    public async Task<List<PatientModel>> FindPotentialDuplicates(CreatePatientRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p."id" AS Id,
                p."first_name" AS FirstName,
                p."middle_name" AS MiddleName,
                p."last_name" AS LastName,
                p."date_of_birth" AS DateOfBirth,
                p."sex_at_birth" AS SexAtBirth,
                pr."name" AS PrimaryProviderName,
                l."name" AS PrimaryLocationName,
                pc."mobile_phone" AS MobilePhone,
                pc."email" AS Email
            FROM patients p
            LEFT JOIN providers pr ON pr."id" = p."primary_provider_id"
            LEFT JOIN locations l ON l."id" = p."primary_location_id"
            LEFT JOIN LATERAL (
                SELECT "mobile_phone", "email"
                FROM patient_contacts
                WHERE "patient_id" = p."id"
                ORDER BY "id"
                LIMIT 1
            ) pc ON TRUE
            WHERE lower(p."last_name") = lower(@LastName)
              AND lower(p."first_name") = lower(@FirstName)
              AND p."date_of_birth" = @DateOfBirth
              AND (
                  p."sex_at_birth" = @SexAtBirth
                  OR @SexAtBirth = 'unknown'
                  OR p."sex_at_birth" = 'unknown'
              )
              AND (
                  COALESCE(@MiddleName, '') = ''
                  OR COALESCE(p."middle_name", '') = ''
                  OR lower(p."middle_name") = lower(@MiddleName)
              )
            ORDER BY p."last_name", p."first_name", p."id"
            LIMIT 10;
            """;

        return await databaseClient.GetListByQuery<PatientModel>(
            sql,
            new
            {
                request.FirstName,
                request.MiddleName,
                request.LastName,
                DateOfBirth = request.DateOfBirth.Date,
                request.SexAtBirth
            },
            cancellationToken);
    }

    public async Task<PatientModel> Create(CreatePatientRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            WITH inserted_patient AS (
                INSERT INTO patients (
                    first_name,
                    middle_name,
                    last_name,
                    suffix,
                    nickname,
                    date_of_birth,
                    sex_at_birth,
                    gender_identity,
                    pronouns,
                    marital_status,
                    employment_status,
                    preferred_language_id,
                    ethnicity,
                    status,
                    classification,
                    category,
                    stage
                )
                VALUES (
                    @FirstName,
                    @MiddleName,
                    @LastName,
                    @Suffix,
                    @Nickname,
                    @DateOfBirth,
                    @SexAtBirth,
                    @GenderIdentity,
                    @Pronouns,
                    @MaritalStatus,
                    @EmploymentStatus,
                    @PreferredLanguageId,
                    @Ethnicity,
                    'active',
                    @Classification,
                    @Category,
                    @Stage
                )
                RETURNING *
            ),
            inserted_contact AS (
                INSERT INTO patient_contacts (
                    patient_id,
                    address_line1,
                    address_line2,
                    city,
                    state,
                    postal_code,
                    home_phone,
                    work_phone,
                    mobile_phone,
                    email,
                    communication_preference
                )
                SELECT
                    id,
                    @AddressLine1,
                    @AddressLine2,
                    @City,
                    @State,
                    @PostalCode,
                    @HomePhone,
                    @WorkPhone,
                    @MobilePhone,
                    @Email,
                    @CommunicationPreference
                FROM inserted_patient
                RETURNING patient_id
            ),
            inserted_case AS (
                INSERT INTO patient_cases (patient_id, name, status, start_date)
                SELECT id, 'Treatment', 'active', CURRENT_DATE
                FROM inserted_patient
                RETURNING id
            )
            SELECT
                p."id" AS Id,
                p."first_name" AS FirstName,
                p."middle_name" AS MiddleName,
                p."last_name" AS LastName,
                p."date_of_birth" AS DateOfBirth,
                p."sex_at_birth" AS SexAtBirth,
                NULL AS PrimaryProviderName,
                NULL AS PrimaryLocationName,
                @MobilePhone AS MobilePhone,
                @Email AS Email
            FROM inserted_patient p;
            """;

        PatientModel? patient = await databaseClient.GetOneByQuery<PatientModel>(
            sql,
            new
            {
                request.FirstName,
                request.MiddleName,
                request.LastName,
                request.Suffix,
                request.Nickname,
                DateOfBirth = request.DateOfBirth.Date,
                request.SexAtBirth,
                request.GenderIdentity,
                request.Pronouns,
                request.MaritalStatus,
                request.EmploymentStatus,
                request.PreferredLanguageId,
                request.Ethnicity,
                request.Classification,
                request.Category,
                request.Stage,
                request.AddressLine1,
                request.AddressLine2,
                request.City,
                request.State,
                request.PostalCode,
                request.HomePhone,
                request.WorkPhone,
                request.MobilePhone,
                request.Email,
                request.CommunicationPreference
            },
            cancellationToken);

        return patient ?? throw new InvalidOperationException("Unable to create patient.");
    }

    public async Task<PatientActivityHeader?> GetActivityHeader(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p."id" AS Id,
                p."first_name" AS FirstName,
                p."middle_name" AS MiddleName,
                p."last_name" AS LastName,
                p."suffix" AS Suffix,
                p."nickname" AS Nickname,
                p."date_of_birth" AS DateOfBirth,
                p."sex_at_birth" AS SexAtBirth,
                p."gender_identity" AS GenderIdentity,
                p."pronouns" AS Pronouns,
                p."marital_status" AS MaritalStatus,
                p."employment_status" AS EmploymentStatus,
                p."preferred_language_id" AS PreferredLanguageId,
                lang."name" AS PreferredLanguage,
                p."ethnicity" AS Ethnicity,
                p."status" AS Status,
                p."classification" AS Classification,
                p."category" AS Category,
                p."stage" AS Stage,
                pr."name" AS PrimaryProviderName,
                l."name" AS PrimaryLocationName,
                (
                    SELECT MAX(v."visit_date")
                    FROM visits v
                    WHERE v."patient_id" = p."id"
                ) AS LastVisitDate,
                (
                    SELECT MIN(a."scheduled_start")
                    FROM appointments a
                    WHERE a."patient_id" = p."id"
                      AND a."scheduled_start" >= NOW()
                      AND a."status" NOT IN ('cancelled', 'no_show')
                ) AS NextAppointmentStart
            FROM patients p
            LEFT JOIN providers pr ON pr."id" = p."primary_provider_id"
            LEFT JOIN locations l ON l."id" = p."primary_location_id"
            LEFT JOIN languages lang ON lang."id" = p."preferred_language_id"
            WHERE p."id" = @PatientId;
            """;

        return await databaseClient.GetOneByQuery<PatientActivityHeader>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<PatientContactSummary?> GetContact(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                "address_line1" AS AddressLine1,
                "address_line2" AS AddressLine2,
                "city" AS City,
                "state" AS State,
                "postal_code" AS PostalCode,
                "country" AS Country,
                "home_phone" AS HomePhone,
                "work_phone" AS WorkPhone,
                "mobile_phone" AS MobilePhone,
                "email" AS Email,
                "communication_preference" AS CommunicationPreference
            FROM patient_contacts
            WHERE "patient_id" = @PatientId
            ORDER BY "id"
            LIMIT 1;
            """;

        return await databaseClient.GetOneByQuery<PatientContactSummary>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<List<InsurancePolicySummary>> GetInsurancePolicies(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                pip."id" AS Id,
                pip."priority" AS Priority,
                ic."name" AS CarrierName,
                ic."payer_id" AS PayerId,
                pip."member_id" AS MemberId,
                pip."group_number" AS GroupNumber,
                pip."group_name" AS GroupName,
                CONCAT_WS(' ', pip."subscriber_first_name", pip."subscriber_middle_name", pip."subscriber_last_name") AS SubscriberName,
                pip."subscriber_date_of_birth" AS SubscriberDateOfBirth,
                pip."relationship_to_patient" AS RelationshipToPatient,
                pip."effective_date" AS EffectiveDate,
                pip."expiration_date" AS ExpirationDate,
                pip."copay" AS Copay,
                pip."is_active" AS IsActive
            FROM patient_insurance_policies pip
            LEFT JOIN insurance_carriers ic ON ic."id" = pip."carrier_id"
            WHERE pip."patient_id" = @PatientId
            ORDER BY pip."priority", pip."id";
            """;

        return await databaseClient.GetListByQuery<InsurancePolicySummary>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<List<AppointmentSummary>> GetAppointments(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                a."id" AS Id,
                a."scheduled_start" AS ScheduledStart,
                a."scheduled_end" AS ScheduledEnd,
                a."status" AS Status,
                a."reason" AS Reason,
                a."notes" AS Notes,
                at."name" AS AppointmentTypeName,
                pr."name" AS ProviderName,
                l."name" AS LocationName,
                r."name" AS RoomName
            FROM appointments a
            LEFT JOIN appointment_types at ON at."id" = a."appointment_type_id"
            LEFT JOIN providers pr ON pr."id" = a."provider_id"
            LEFT JOIN locations l ON l."id" = a."location_id"
            LEFT JOIN rooms r ON r."id" = a."room_id"
            WHERE a."patient_id" = @PatientId
            ORDER BY a."scheduled_start" DESC
            LIMIT 30;
            """;

        return await databaseClient.GetListByQuery<AppointmentSummary>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<List<VisitSummary>> GetVisits(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                v."id" AS Id,
                v."visit_date" AS VisitDate,
                v."visit_type" AS VisitType,
                v."status" AS Status,
                v."chief_complaint" AS ChiefComplaint,
                pr."name" AS ProviderName,
                nurse."name" AS NurseName,
                l."name" AS LocationName,
                v."smoking_status" AS SmokingStatus,
                vs."systolic_bp" AS SystolicBp,
                vs."diastolic_bp" AS DiastolicBp,
                vs."heart_rate" AS HeartRate,
                vs."respiratory_rate" AS RespiratoryRate,
                vs."temperature_c" AS TemperatureC,
                vs."pulse_ox" AS PulseOx,
                vs."height_cm" AS HeightCm,
                vs."weight_kg" AS WeightKg,
                vs."bmi" AS Bmi,
                vs."pain_score" AS PainScore
            FROM visits v
            LEFT JOIN providers pr ON pr."id" = v."provider_id"
            LEFT JOIN providers nurse ON nurse."id" = v."nurse_id"
            LEFT JOIN locations l ON l."id" = v."location_id"
            LEFT JOIN LATERAL (
                SELECT *
                FROM vital_signs
                WHERE "visit_id" = v."id"
                ORDER BY "recorded_at" DESC, "id" DESC
                LIMIT 1
            ) vs ON TRUE
            WHERE v."patient_id" = @PatientId
            ORDER BY v."visit_date" DESC, v."id" DESC
            LIMIT 20;
            """;

        return await databaseClient.GetListByQuery<VisitSummary>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<List<PatientProblemSummary>> GetProblems(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                "id" AS Id,
                "diagnosis_code" AS DiagnosisCode,
                "description" AS Description,
                "status" AS Status,
                "onset_date" AS OnsetDate,
                "resolved_date" AS ResolvedDate,
                "note" AS Note
            FROM patient_problems
            WHERE "patient_id" = @PatientId
            ORDER BY
                CASE "status" WHEN 'active' THEN 0 ELSE 1 END,
                COALESCE("onset_date", "created_at"::date) DESC,
                "id" DESC;
            """;

        return await databaseClient.GetListByQuery<PatientProblemSummary>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<List<PatientAllergySummary>> GetAllergies(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                "id" AS Id,
                "allergen" AS Allergen,
                "allergen_type" AS AllergenType,
                "reaction" AS Reaction,
                "severity" AS Severity,
                "status" AS Status,
                "note" AS Note
            FROM patient_allergies
            WHERE "patient_id" = @PatientId
            ORDER BY
                CASE "status" WHEN 'active' THEN 0 ELSE 1 END,
                "allergen";
            """;

        return await databaseClient.GetListByQuery<PatientAllergySummary>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<List<PatientMedicationSummary>> GetMedications(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                pm."id" AS Id,
                v."visit_type" AS VisitType,
                pm."medication_name" AS MedicationName,
                pm."strength" AS Strength,
                pm."dose" AS Dose,
                pm."route" AS Route,
                pm."frequency" AS Frequency,
                pm."start_date" AS StartDate,
                pm."end_date" AS EndDate,
                pm."refills" AS Refills,
                pm."controlled" AS Controlled,
                pm."status" AS Status,
                pm."instructions" AS Instructions,
                pm."note" AS Note
            FROM patient_medications pm
            LEFT JOIN visits v ON v."id" = pm."visit_id"
            WHERE pm."patient_id" = @PatientId
            ORDER BY
                CASE pm."status" WHEN 'active' THEN 0 ELSE 1 END,
                COALESCE(pm."start_date", pm."created_at"::date) DESC,
                pm."id" DESC
            LIMIT 40;
            """;

        return await databaseClient.GetListByQuery<PatientMedicationSummary>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<List<ClinicalOrderSummary>> GetOrders(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                co."id" AS Id,
                v."visit_type" AS VisitType,
                pr."name" AS OrderedByProviderName,
                co."order_type" AS OrderType,
                co."code" AS Code,
                co."description" AS Description,
                co."diagnosis_code" AS DiagnosisCode,
                co."priority" AS Priority,
                co."status" AS Status,
                co."ordered_at" AS OrderedAt,
                co."completed_at" AS CompletedAt,
                co."note" AS Note
            FROM clinical_orders co
            LEFT JOIN visits v ON v."id" = co."visit_id"
            LEFT JOIN providers pr ON pr."id" = co."ordered_by_provider_id"
            WHERE co."patient_id" = @PatientId
            ORDER BY co."ordered_at" DESC, co."id" DESC
            LIMIT 40;
            """;

        return await databaseClient.GetListByQuery<ClinicalOrderSummary>(sql, new { PatientId = patientId }, cancellationToken);
    }

    public async Task<List<PatientNoteSummary>> GetNotes(long patientId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                "id" AS Id,
                "note_type" AS NoteType,
                "body" AS Body,
                "created_at" AS CreatedAt
            FROM patient_notes
            WHERE "patient_id" = @PatientId
            ORDER BY "created_at" DESC, "id" DESC
            LIMIT 10;
            """;

        return await databaseClient.GetListByQuery<PatientNoteSummary>(sql, new { PatientId = patientId }, cancellationToken);
    }
}
