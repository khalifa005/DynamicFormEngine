using KH.Application.Fsms.FieldCatalog.Models;
using KH.Application.Fsms.FieldCatalog.Queries.GetFieldCatalog;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

[Authorize]
[Route("api/v1/fsms/field-catalog")]
public sealed class FsmsFieldCatalogController : ApiControllerBase
{
    /// <summary>Searchable global field catalog for the form-builder Data Name autocomplete.</summary>
    [HttpGet]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<IReadOnlyList<FieldCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFieldCatalog([FromQuery] GetFieldCatalogQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }
}
