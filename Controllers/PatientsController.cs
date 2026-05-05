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
            return NotFound(new { message = "Patient was not found." });
        }

        return Ok(response);
    }
}
