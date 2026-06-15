namespace Medpointe.Models.Schedule;

public sealed class ScheduleQuery
{
    public DateTime ScheduleStart { get; init; }
    public DateTime ScheduleEnd { get; init; }
    public long? ProviderId { get; init; }
    public long? LocationId { get; init; }
    public string? Status { get; init; }
}

public sealed class ScheduleAppointmentModel
{
    public long Id { get; init; }
    public long? PatientId { get; init; }
    public string? PatientName { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? MobilePhone { get; init; }
    public long? AppointmentTypeId { get; init; }
    public string? AppointmentTypeName { get; init; }
    public string? AppointmentTypeColor { get; init; }
    public long? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public long? LocationId { get; init; }
    public string? LocationName { get; init; }
    public long? RoomId { get; init; }
    public string? RoomName { get; init; }
    public DateTime ScheduledStart { get; init; }
    public DateTime ScheduledEnd { get; init; }
    public required string Status { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? CheckedInAt { get; init; }
    public DateTime? TriagedAt { get; init; }
    public DateTime? ProviderStartedAt { get; init; }
    public DateTime? EncounterClosedAt { get; init; }
    public DateTime? CheckedOutAt { get; init; }
    public DateTime? SignedAt { get; init; }
    public long? BillingClaimId { get; init; }
    public string? BillingStatus { get; init; }
    public string? BillingStage { get; init; }
}

public sealed class ScheduleOptionModel
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public string? Secondary { get; init; }
    public long? LocationId { get; init; }
    public int? DefaultDurationMinutes { get; init; }
    public string? Color { get; init; }
}

public sealed class ScheduleOptionsModel
{
    public List<ScheduleOptionModel> Providers { get; init; } = [];
    public List<ScheduleOptionModel> Locations { get; init; } = [];
    public List<ScheduleOptionModel> Rooms { get; init; } = [];
    public List<ScheduleOptionModel> AppointmentTypes { get; init; } = [];
}

public sealed class CreateAppointmentRequest
{
    public long PatientId { get; init; }
    public long? AppointmentTypeId { get; init; }
    public long? ProviderId { get; init; }
    public long? LocationId { get; init; }
    public long? RoomId { get; init; }
    public DateTime ScheduledStart { get; init; }
    public DateTime? ScheduledEnd { get; init; }
    public int? DurationMinutes { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateAppointmentStatusRequest
{
    public required string Status { get; init; }
    public string? Note { get; init; }
}

public sealed class ScheduleWriteResult
{
    public ScheduleAppointmentModel? Appointment { get; init; }
    public string? ErrorMessage { get; init; }

    public bool Success => Appointment is not null;
}