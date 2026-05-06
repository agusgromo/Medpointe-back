using Medpointe.Models.Api;
using Medpointe.Models.Patients;
using Medpointe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medpointe.Controllers;

[ApiController]
[Route("patient")]
public class PatientsController(PatientsService patientService) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<PatientModel>> Create(CreatePatientRequest request, CancellationToken cancellationToken)
    {
        CreatePatientResult result = await patientService.Create(request, cancellationToken);

        if (result.Created)
        {
            return CreatedAtAction(
                nameof(GetActivity),
                new { patientId = result.Patient!.Id },
                result.Patient);
        }

        if (result.HasDuplicates)
        {
            return Conflict(new ApiError
            {
                Title = "Duplicate patient",
                Message = result.ErrorMessage ?? "A patient with matching demographics already exists.",
                Code = "duplicate_patient"
            });
        }

        return BadRequest(new ApiError
        {
            Title = "Invalid patient",
            Message = result.ErrorMessage ?? "The patient could not be created.",
            Code = "invalid_patient"
        });
    }

    [Authorize]
    [HttpGet("search")]
    public async Task<ActionResult<List<PatientModel>>> Search(string search, CancellationToken cancellationToken)
    {
        List<PatientModel> response = await patientService.Search(search, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpGet("{patientId}/activity")]
    public async Task<ActionResult<PatientActivityModel>> GetActivity(long patientId, CancellationToken cancellationToken)
    {
        PatientActivityModel? response = await patientService.GetActivity(patientId, cancellationToken);

        if (response is null)
        {
            return NotFound(new ApiError
            {
                Title = "Patient not found",
                Message = "No patient exists with the provided identifier.",
                Code = "patient_not_found"
            });
        }

        return Ok(response);
    }
}
