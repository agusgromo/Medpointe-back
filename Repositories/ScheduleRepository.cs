using Dapper;
using Medpointe.Data;
using Medpointe.Models.Schedule;

namespace Medpointe.Repositories;

public sealed class ScheduleRepository(DatabaseClient databaseClient)
{
    private const string AppointmentSelect = """
        SELECT
            a."id" AS Id,
            a."patient_id" AS PatientId,
            CONCAT_WS(' ', p."first_name", p."middle_name", p."last_name") AS PatientName,
            p."date_of_birth" AS DateOfBirth,
            pc."mobile_phone" AS MobilePhone,
            a."appointment_type_id" AS AppointmentTypeId,
            at."name" AS AppointmentTypeName,
            at."color" AS AppointmentTypeColor,
            a."provider_id" AS ProviderId,
            pr."name" AS ProviderName,
            a."location_id" AS LocationId,
            l."name" AS LocationName,
            a."room_id" AS RoomId,
            r."name" AS RoomName,
            a."scheduled_start" AS ScheduledStart,
            a."scheduled_end" AS ScheduledEnd,
            a."status" AS Status,
            a."reason" AS Reason,
            a."notes" AS Notes,
            a."confirmed_at" AS ConfirmedAt,
            a."checked_in_at" AS CheckedInAt,
            a."triaged_at" AS TriagedAt,
            a."provider_started_at" AS ProviderStartedAt,
            a."encounter_closed_at" AS EncounterClosedAt,
            a."checked_out_at" AS CheckedOutAt,
            a."signed_at" AS SignedAt,
            bc."id" AS BillingClaimId,
            bc."status" AS BillingStatus,
            bc."billing_stage" AS BillingStage
        FROM appointments a
        LEFT JOIN patients p ON p."id" = a."patient_id"
        LEFT JOIN LATERAL (
            SELECT "mobile_phone"
            FROM patient_contacts
            WHERE "patient_id" = a."patient_id"
            ORDER BY "id"
            LIMIT 1
        ) pc ON TRUE
        LEFT JOIN appointment_types at ON at."id" = a."appointment_type_id"
        LEFT JOIN providers pr ON pr."id" = a."provider_id"
        LEFT JOIN locations l ON l."id" = a."location_id"
        LEFT JOIN rooms r ON r."id" = a."room_id"
        LEFT JOIN LATERAL (
            SELECT "id", "status", "billing_stage"
            FROM billing_claims
            WHERE "appointment_id" = a."id"
            ORDER BY "id" DESC
            LIMIT 1
        ) bc ON TRUE
        """;

    public async Task<List<ScheduleAppointmentModel>> GetAppointments(ScheduleQuery query, CancellationToken cancellationToken)
    {
        string sql = $"""
            {AppointmentSelect}
            WHERE a."scheduled_start" >= @ScheduleStart
              AND a."scheduled_start" < @ScheduleEnd
              AND (@ProviderId IS NULL OR a."provider_id" = @ProviderId)
              AND (@LocationId IS NULL OR a."location_id" = @LocationId)
              AND (@Status IS NULL OR a."status" = @Status)
            ORDER BY a."scheduled_start", pr."name", p."last_name", p."first_name", a."id";
            """;

        return await databaseClient.GetListByQuery<ScheduleAppointmentModel>(
            sql,
            new
            {
                query.ScheduleStart,
                query.ScheduleEnd,
                query.ProviderId,
                query.LocationId,
                query.Status
            },
            cancellationToken);
    }

    public async Task<ScheduleAppointmentModel?> GetAppointment(long appointmentId, CancellationToken cancellationToken)
    {
        string sql = $"""
            {AppointmentSelect}
            WHERE a."id" = @AppointmentId;
            """;

        return await databaseClient.GetOneByQuery<ScheduleAppointmentModel>(
            sql,
            new { AppointmentId = appointmentId },
            cancellationToken);
    }

    public async Task<ScheduleOptionsModel> GetOptions(CancellationToken cancellationToken)
    {
        const string providersSql = """
            SELECT
                "id" AS Id,
                "name" AS Name,
                "title" AS Secondary
            FROM providers
            WHERE "active" = TRUE
            ORDER BY "name";
            """;

        const string locationsSql = """
            SELECT
                "id" AS Id,
                "name" AS Name
            FROM locations
            WHERE "active" = TRUE
            ORDER BY "name";
            """;

        const string roomsSql = """
            SELECT
                r."id" AS Id,
                r."name" AS Name,
                l."name" AS Secondary,
                r."location_id" AS LocationId
            FROM rooms r
            LEFT JOIN locations l ON l."id" = r."location_id"
            WHERE r."active" = TRUE
            ORDER BY l."name", r."name";
            """;

        const string appointmentTypesSql = """
            SELECT
                "id" AS Id,
                "name" AS Name,
                "visit_type" AS Secondary,
                "default_duration_minutes" AS DefaultDurationMinutes,
                "color" AS Color
            FROM appointment_types
            WHERE "active" = TRUE
            ORDER BY "name";
            """;

        return new ScheduleOptionsModel
        {
            Providers = await databaseClient.GetListByQuery<ScheduleOptionModel>(providersSql, cancellationToken: cancellationToken),
            Locations = await databaseClient.GetListByQuery<ScheduleOptionModel>(locationsSql, cancellationToken: cancellationToken),
            Rooms = await databaseClient.GetListByQuery<ScheduleOptionModel>(roomsSql, cancellationToken: cancellationToken),
            AppointmentTypes = await databaseClient.GetListByQuery<ScheduleOptionModel>(appointmentTypesSql, cancellationToken: cancellationToken)
        };
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

    public async Task<bool> ProviderExists(long providerId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM providers
                WHERE "id" = @ProviderId
                  AND "active" = TRUE
            );
            """;

        return await databaseClient.GetOneByQuery<bool>(sql, new { ProviderId = providerId }, cancellationToken);
    }

    public async Task<bool> LocationExists(long locationId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM locations
                WHERE "id" = @LocationId
                  AND "active" = TRUE
            );
            """;

        return await databaseClient.GetOneByQuery<bool>(sql, new { LocationId = locationId }, cancellationToken);
    }

    public async Task<bool> RoomExists(long roomId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM rooms
                WHERE "id" = @RoomId
                  AND "active" = TRUE
            );
            """;

        return await databaseClient.GetOneByQuery<bool>(sql, new { RoomId = roomId }, cancellationToken);
    }

    public async Task<bool> AppointmentTypeExists(long appointmentTypeId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM appointment_types
                WHERE "id" = @AppointmentTypeId
                  AND "active" = TRUE
            );
            """;

        return await databaseClient.GetOneByQuery<bool>(sql, new { AppointmentTypeId = appointmentTypeId }, cancellationToken);
    }

    public async Task<bool> HasProviderConflict(
        long providerId,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        long? exceptAppointmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM appointments
                WHERE "provider_id" = @ProviderId
                  AND "status" NOT IN ('cancelled', 'no_show')
                  AND "scheduled_start" < @ScheduledEnd
                  AND "scheduled_end" > @ScheduledStart
                  AND (@ExceptAppointmentId IS NULL OR "id" <> @ExceptAppointmentId)
            );
            """;

        return await databaseClient.GetOneByQuery<bool>(
            sql,
            new
            {
                ProviderId = providerId,
                ScheduledStart = scheduledStart,
                ScheduledEnd = scheduledEnd,
                ExceptAppointmentId = exceptAppointmentId
            },
            cancellationToken);
    }

    public async Task<bool> HasRoomConflict(
        long roomId,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        long? exceptAppointmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM appointments
                WHERE "room_id" = @RoomId
                  AND "status" NOT IN ('cancelled', 'no_show')
                  AND "scheduled_start" < @ScheduledEnd
                  AND "scheduled_end" > @ScheduledStart
                  AND (@ExceptAppointmentId IS NULL OR "id" <> @ExceptAppointmentId)
            );
            """;

        return await databaseClient.GetOneByQuery<bool>(
            sql,
            new
            {
                RoomId = roomId,
                ScheduledStart = scheduledStart,
                ScheduledEnd = scheduledEnd,
                ExceptAppointmentId = exceptAppointmentId
            },
            cancellationToken);
    }

    public async Task<long> CreateAppointment(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        return await databaseClient.ExecuteInTransaction(async (connection, transaction) =>
        {
            const string insertAppointmentSql = """
                INSERT INTO appointments (
                    patient_id,
                    appointment_type_id,
                    provider_id,
                    location_id,
                    room_id,
                    scheduled_start,
                    scheduled_end,
                    status,
                    reason,
                    notes
                )
                VALUES (
                    @PatientId,
                    @AppointmentTypeId,
                    @ProviderId,
                    @LocationId,
                    @RoomId,
                    @ScheduledStart,
                    @ScheduledEnd,
                    'scheduled',
                    @Reason,
                    @Notes
                )
                RETURNING "id";
                """;

            long appointmentId = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    insertAppointmentSql,
                    request,
                    transaction,
                    cancellationToken: cancellationToken));

            const string insertHistorySql = """
                INSERT INTO appointment_status_history (
                    appointment_id,
                    from_status,
                    to_status,
                    note
                )
                VALUES (
                    @AppointmentId,
                    NULL,
                    'scheduled',
                    'Appointment created'
                );
                """;

            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertHistorySql,
                    new { AppointmentId = appointmentId },
                    transaction,
                    cancellationToken: cancellationToken));

            return appointmentId;
        }, cancellationToken);
    }

    public async Task<long?> UpdateAppointmentStatus(
        long appointmentId,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        return await databaseClient.ExecuteInTransaction<long?>(async (connection, transaction) =>
        {
            const string currentStatusSql = """
                SELECT "status"
                FROM appointments
                WHERE "id" = @AppointmentId
                FOR UPDATE;
                """;

            string? currentStatus = await connection.QueryFirstOrDefaultAsync<string>(
                new CommandDefinition(
                    currentStatusSql,
                    new { AppointmentId = appointmentId },
                    transaction,
                    cancellationToken: cancellationToken));

            if (currentStatus is null)
            {
                return null;
            }

            const string updateAppointmentSql = """
                UPDATE appointments
                SET
                    "status" = @Status,
                    "updated_at" = now(),
                    "confirmed_at" = CASE WHEN @Status = 'confirmed' THEN coalesce("confirmed_at", now()) ELSE "confirmed_at" END,
                    "checked_in_at" = CASE WHEN @Status = 'checked_in' THEN coalesce("checked_in_at", now()) ELSE "checked_in_at" END,
                    "triaged_at" = CASE WHEN @Status = 'triage' THEN coalesce("triaged_at", now()) ELSE "triaged_at" END,
                    "provider_started_at" = CASE WHEN @Status = 'with_provider' THEN coalesce("provider_started_at", now()) ELSE "provider_started_at" END,
                    "encounter_closed_at" = CASE WHEN @Status = 'ready_checkout' THEN coalesce("encounter_closed_at", now()) ELSE "encounter_closed_at" END,
                    "checked_out_at" = CASE WHEN @Status = 'checked_out' THEN coalesce("checked_out_at", now()) ELSE "checked_out_at" END
                WHERE "id" = @AppointmentId;
                """;

            await connection.ExecuteAsync(
                new CommandDefinition(
                    updateAppointmentSql,
                    new { AppointmentId = appointmentId, request.Status },
                    transaction,
                    cancellationToken: cancellationToken));

            const string insertHistorySql = """
                INSERT INTO appointment_status_history (
                    appointment_id,
                    from_status,
                    to_status,
                    note
                )
                VALUES (
                    @AppointmentId,
                    @FromStatus,
                    @ToStatus,
                    @Note
                );
                """;

            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertHistorySql,
                    new
                    {
                        AppointmentId = appointmentId,
                        FromStatus = currentStatus,
                        ToStatus = request.Status,
                        request.Note
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            return appointmentId;
        }, cancellationToken);
    }
}