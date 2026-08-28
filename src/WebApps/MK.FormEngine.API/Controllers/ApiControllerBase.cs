using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result);
        }

        var status = result.Errors.FirstOrDefault()?.HttpStatusCode
            ?? ApiErrorHttpMapper.GetHttpStatusCode(result.Errors.FirstOrDefault()?.Code);
        return StatusCode(status, result);
    }

    protected IActionResult RouteIdMismatch()
    {
        return ToActionResult(Result<object>.Fail(
            "Route identifier does not match the request body.",
            ApiErrorCodes.ValidationError,
            httpStatusCode: 400));
    }
}
