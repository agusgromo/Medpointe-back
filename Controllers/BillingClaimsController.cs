using Medpointe.Models.Api;
using Medpointe.Models.Billing;
using Medpointe.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Medpointe.Controllers;

[ApiController]
[Authorize]
[Route("billing/claims")]
public sealed class BillingClaimsController(BillingClaimsService billingClaimsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BillingClaimSummaryModel>>> Search(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? billingStage,
        [FromQuery] DateTime? serviceDateFrom,
        [FromQuery] DateTime? serviceDateTo,
        CancellationToken cancellationToken)
    {
        List<BillingClaimSummaryModel> claims = await billingClaimsService.Search(
            new BillingClaimQuery
            {
                Search = search,
                Status = status,
                BillingStage = billingStage,
                ServiceDateFrom = serviceDateFrom,
                ServiceDateTo = serviceDateTo
            },
            cancellationToken);

        return Ok(claims);
    }

    [HttpGet("{claimId:long}")]
    public async Task<ActionResult<BillingClaimDetailModel>> Get(long claimId, CancellationToken cancellationToken)
    {
        BillingClaimDetailModel? claim = await billingClaimsService.GetById(claimId, cancellationToken);

        if (claim is null)
        {
            return NotFound(new ApiError
            {
                Title = "Claim not found",
                Message = "No billing claim exists with the provided identifier.",
                Code = "billing_claim_not_found"
            });
        }

        return Ok(claim);
    }

    [HttpPost]
    public async Task<ActionResult<BillingClaimDetailModel>> Create(
        [FromBody] CreateBillingClaimRequest request,
        CancellationToken cancellationToken)
    {
        CreateBillingClaimResult result = await billingClaimsService.Create(request, cancellationToken);

        if (result.Created)
        {
            return CreatedAtAction(nameof(Get), new { claimId = result.Claim!.Id }, result.Claim);
        }

        return BadRequest(new ApiError
        {
            Title = "Invalid claim",
            Message = result.ErrorMessage ?? "The claim could not be created.",
            Code = "invalid_billing_claim"
        });
    }
}