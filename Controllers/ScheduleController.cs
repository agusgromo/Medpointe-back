using Medpointe.Models.Api;
using Medpointe.Models.Schedule;
using Medpointe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medpointe.Controllers;

[ApiController]
[Route("schedule")]
public sealed class ScheduleController(ScheduleService scheduleService) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<ScheduleAppointmentModel>>> GetAppointments(
        DateTime? date,
        long? providerId,
        long? locationId,
        string? status,
        CancellationToken cancellationToken)
    {
        List<ScheduleAppointmentModel> appointments = await scheduleService.GetAppointments(
            date,
            providerId,
            locationId,
            status,
            cancellationToken);

        return Ok(appointments);
    }

    [Authorize]
    [HttpGet("options")]
    public async Task<ActionResult<ScheduleOptionsModel>> GetOptions(CancellationToken cancellationToken)
    {
        ScheduleOptionsModel options = await scheduleService.GetOptions(cancellationToken);
        return Ok(options);
    }

    [Authorize]
    [HttpPost("appointments")]
    public async Task<ActionResult<ScheduleAppointmentModel>> CreateAppointment(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        ScheduleWriteResult result = await scheduleService.CreateAppointment(request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ApiError
            {
                Title = "Invalid appointment",
                Message = result.ErrorMessage ?? "Appointment could not be created.",
                Code = "invalid_appointment"
            });
        }

        return CreatedAtAction(
            nameof(GetAppointments),
            new { date = result.Appointment!.ScheduledStart.Date },
            result.Appointment);
    }

    [Authorize]
    [HttpPatch("appointments/{appointmentId}/status")]
    public async Task<ActionResult<ScheduleAppointmentModel>> UpdateAppointmentStatus(
        long appointmentId,
        UpdateAppointmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        ScheduleWriteResult result = await scheduleService.UpdateAppointmentStatus(
            appointmentId,
            request,
            cancellationToken);

        if (!result.Success)
        {
            string errorMessage = result.ErrorMessage ?? "Appointment could not be updated.";

            if (errorMessage == "Appointment not found.")
            {
                return NotFound(new ApiError
                {
                    Title = "Appointment not found",
                    Message = errorMessage,
                    Code = "appointment_not_found"
                });
            }

            return BadRequest(new ApiError
            {
                Title = "Invalid appointment",
                Message = errorMessage,
                Code = "invalid_appointment"
            });
        }

        return Ok(result.Appointment);
    }
}