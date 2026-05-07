using Medpointe.Models.Patients;
using Medpointe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medpointe.Controllers;

[ApiController]
[Route("languages")]
public sealed class LanguagesController(LanguagesService languagesService) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<LanguageModel>>> GetActive(CancellationToken cancellationToken)
    {
        List<LanguageModel> response = await languagesService.GetActive(cancellationToken);
        return Ok(response);
    }
}
