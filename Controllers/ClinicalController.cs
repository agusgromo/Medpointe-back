using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Medpointe.Models.Api;
using Medpointe.Models.Clinical;
using Medpointe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medpointe.Controllers;

[ApiController]
[Route("clinical")]
public sealed class ClinicalController(ClinicalService clinicalService) : ControllerBase
{
    [Authorize]
    [HttpGet("patient/{patientId}")]
    public async Task<ActionResult<ClinicalChartModel>> GetChart(
        long patientId,
        long? appointmentId,
        CancellationToken cancellationToken)
    {
        ClinicalChartModel? response = await clinicalService.GetChart(
            patientId,
            appointmentId,
            GetCurrentUsername(),
            cancellationToken);

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

    private string GetCurrentUsername()
    {
        return User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? string.Empty;
    }
}
