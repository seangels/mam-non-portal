using System.ComponentModel.DataAnnotations;
using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Assessments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AdminPortal.Application.GoogleSheets;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/google-sheets")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
public sealed class GoogleSheetsController(IGoogleSheetsService googleSheetsService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("credential-smoke")]
    [ProducesResponseType<GoogleSheetsCredentialSmokeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<ActionResult<GoogleSheetsCredentialSmokeResponse>> SmokeTestCredential(
        [FromQuery(Name = "return")] string? returnUrl,
        CancellationToken cancellationToken)
    {
        var response = await googleSheetsService.SmokeTestCredentialAsync(cancellationToken);
        if (response.Success && TryGetSafeReturnUrl(returnUrl, out var safeReturnUrl))
        {
            return Redirect(safeReturnUrl);
        }

        return Ok(response);
    }

    private static bool TryGetSafeReturnUrl(string? returnUrl, out string safeReturnUrl)
    {
        safeReturnUrl = string.Empty;
        if (!string.IsNullOrEmpty(returnUrl)
            && returnUrl[0] == '/'
            && (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\')))
        {
            safeReturnUrl = returnUrl;
            return true;
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        safeReturnUrl = uri.AbsoluteUri;
        return true;
    }

    [HttpPost("sync-assessments")]
    [ProducesResponseType<SyncAssessmentsFromGoogleSheetsResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SyncAssessmentsFromGoogleSheetsResponse>> SyncAssessmentsFromGoogleSheets(
        SyncAssessmentsFromGoogleSheetsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await googleSheetsService.SyncAssessmentsAsync(request, cancellationToken));
}
