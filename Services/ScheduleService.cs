using Medpointe.Models.Schedule;
using Medpointe.Repositories;

namespace Medpointe.Services;

public sealed class ScheduleService(ScheduleRepository scheduleRepository)
{
    private static readonly TimeZoneInfo ClinicTimeZone = ResolveClinicTimeZone();

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "scheduled",
        "confirmed",
        "checked_in",
        "triage",
        "with_provider",
        "nurse_order",
        "ready_checkout",
        "checked_out",
        "completed",
        "cancelled",
        "no_show"
    };

    public async Task<List<ScheduleAppointmentModel>> GetAppointments(
        DateTime? date,
        long? providerId,
        long? locationId,
        string? status,
        CancellationToken cancellationToken)
    {
        string? normalizedStatus = NormalizeStatus(status);

        if (normalizedStatus is not null && !ValidStatuses.Contains(normalizedStatus))
        {
            return [];
        }

        DateTime scheduleDate = (date ?? TodayInClinic()).Date;
        ScheduleQuery query = new()
        {
            ScheduleStart = ClinicDateToUtc(scheduleDate),
            ScheduleEnd = ClinicDateToUtc(scheduleDate.AddDays(1)),
            ProviderId = providerId,
            LocationId = locationId,
            Status = normalizedStatus
        };

        return await scheduleRepository.GetAppointments(query, cancellationToken);
    }

    public async Task<ScheduleOptionsModel> GetOptions(CancellationToken cancellationToken)
    {
        return await scheduleRepository.GetOptions(cancellationToken);
    }

    public async Task<ScheduleWriteResult> CreateAppointment(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        CreateAppointmentRequest normalizedRequest = NormalizeCreateRequest(request);
        string? validationError = await ValidateCreateRequest(normalizedRequest, cancellationToken);

        if (validationError is not null)
        {
            return new ScheduleWriteResult { ErrorMessage = validationError };
        }

        if (normalizedRequest.ProviderId is not null
            && await scheduleRepository.HasProviderConflict(
                normalizedRequest.ProviderId.Value,
                normalizedRequest.ScheduledStart,
                normalizedRequest.ScheduledEnd!.Value,
                null,
                cancellationToken))
        {
            return new ScheduleWriteResult { ErrorMessage = "Provider already has an appointment in this time range." };
        }

        if (normalizedRequest.RoomId is not null
            && await scheduleRepository.HasRoomConflict(
                normalizedRequest.RoomId.Value,
                normalizedRequest.ScheduledStart,
                normalizedRequest.ScheduledEnd!.Value,
                null,
                cancellationToken))
        {
            return new ScheduleWriteResult { ErrorMessage = "Room already has an appointment in this time range." };
        }

        long appointmentId = await scheduleRepository.CreateAppointment(normalizedRequest, cancellationToken);
        ScheduleAppointmentModel? appointment = await scheduleRepository.GetAppointment(appointmentId, cancellationToken);

        return new ScheduleWriteResult { Appointment = appointment };
    }

    public async Task<ScheduleWriteResult> UpdateAppointmentStatus(
        long appointmentId,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        UpdateAppointmentStatusRequest normalizedRequest = NormalizeStatusRequest(request);

        if (!ValidStatuses.Contains(normalizedRequest.Status))
        {
            return new ScheduleWriteResult { ErrorMessage = "Appointment status is not valid." };
        }

        long? updatedAppointmentId = await scheduleRepository.UpdateAppointmentStatus(
            appointmentId,
            normalizedRequest,
            cancellationToken);

        if (updatedAppointmentId is null)
        {
            return new ScheduleWriteResult { ErrorMessage = "Appointment not found." };
        }

        ScheduleAppointmentModel? appointment = await scheduleRepository.GetAppointment(updatedAppointmentId.Value, cancellationToken);

        return new ScheduleWriteResult { Appointment = appointment };
    }

    private async Task<string?> ValidateCreateRequest(CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        if (request.PatientId <= 0)
        {
            return "Patient is required.";
        }

        if (!await scheduleRepository.PatientExists(request.PatientId, cancellationToken))
        {
            return "Patient was not found.";
        }

        if (request.ScheduledStart == default)
        {
            return "Scheduled start is required.";
        }

        if (request.ScheduledEnd is null || request.ScheduledEnd <= request.ScheduledStart)
        {
            return "Scheduled end must be after scheduled start.";
        }

        if ((request.ScheduledEnd.Value - request.ScheduledStart).TotalHours > 8)
        {
            return "Appointment duration cannot exceed 8 hours.";
        }

        if (request.AppointmentTypeId is not null
            && !await scheduleRepository.AppointmentTypeExists(request.AppointmentTypeId.Value, cancellationToken))
        {
            return "Appointment type was not found.";
        }

        if (request.ProviderId is not null
            && !await scheduleRepository.ProviderExists(request.ProviderId.Value, cancellationToken))
        {
            return "Provider was not found.";
        }

        if (request.LocationId is not null
            && !await scheduleRepository.LocationExists(request.LocationId.Value, cancellationToken))
        {
            return "Location was not found.";
        }

        if (request.RoomId is not null
            && !await scheduleRepository.RoomExists(request.RoomId.Value, cancellationToken))
        {
            return "Room was not found.";
        }

        return null;
    }

    private static CreateAppointmentRequest NormalizeCreateRequest(CreateAppointmentRequest request)
    {
        DateTime scheduledStart = request.ScheduledStart == default
            ? default
            : ToUtcInstant(request.ScheduledStart);
        int durationMinutes = Math.Clamp(request.DurationMinutes ?? 15, 5, 480);
        DateTime scheduledEnd = request.ScheduledEnd is null
            ? scheduledStart.AddMinutes(durationMinutes)
            : ToUtcInstant(request.ScheduledEnd.Value);

        return new CreateAppointmentRequest
        {
            PatientId = request.PatientId,
            AppointmentTypeId = PositiveOrNull(request.AppointmentTypeId),
            ProviderId = PositiveOrNull(request.ProviderId),
            LocationId = PositiveOrNull(request.LocationId),
            RoomId = PositiveOrNull(request.RoomId),
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            DurationMinutes = durationMinutes,
            Reason = BlankToNull(request.Reason),
            Notes = BlankToNull(request.Notes)
        };
    }

    private static UpdateAppointmentStatusRequest NormalizeStatusRequest(UpdateAppointmentStatusRequest request)
    {
        return new UpdateAppointmentStatusRequest
        {
            Status = NormalizeStatus(request.Status) ?? string.Empty,
            Note = BlankToNull(request.Note)
        };
    }

    private static string? NormalizeStatus(string? status)
    {
        string? normalized = BlankToNull(status)?.ToLowerInvariant().Replace('-', '_');
        return normalized;
    }

    private static long? PositiveOrNull(long? value)
    {
        return value is > 0 ? value : null;
    }

    private static string? BlankToNull(string? value)
    {
        string? trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static DateTime TodayInClinic()
    {
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ClinicTimeZone).Date;
    }

    private static DateTime ClinicDateToUtc(DateTime date)
    {
        DateTime clinicDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(clinicDate, ClinicTimeZone);
    }

    private static DateTime ToUtcInstant(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => TimeZoneInfo.ConvertTimeToUtc(value, ClinicTimeZone)
        };
    }

    private static TimeZoneInfo ResolveClinicTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        }
    }
}