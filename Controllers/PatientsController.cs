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
    public async Task<ActionResult> Search(string search, CancellationToken cancellationToken )
    {
        List<PatientModel> response = await patientService.Search(search, cancellationToken);
        return Ok(response);
    }
}
