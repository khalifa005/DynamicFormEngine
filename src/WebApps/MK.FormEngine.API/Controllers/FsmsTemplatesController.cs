using KH.Application.Fsms.Templates.Commands.ArchiveTemplate;
using KH.Application.Fsms.Templates.Commands.CloneTemplate;
using KH.Application.Fsms.Templates.Commands.CreateTemplate;
using KH.Application.Fsms.Templates.Commands.DeprecateTemplate;
using KH.Application.Fsms.Templates.Commands.PublishTemplate;
using KH.Application.Fsms.Templates.Commands.SaveTemplateDefinition;
using KH.Application.Fsms.Templates.Commands.UpdateTemplate;
using KH.Application.Fsms.Templates.Models;
using KH.Application.Fsms.Templates.Queries.GetTemplateById;
using KH.Application.Fsms.Templates.Queries.GetTemplateVersions;
using KH.Application.Fsms.Templates.Queries.GetTemplates;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

[Authorize]
[Route("api/v1/fsms/templates")]
public sealed class FsmsTemplatesController : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<TemplateListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates([FromQuery] GetTemplatesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<TemplateDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplateById(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetTemplateByIdQuery { TemplateId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{id:long}/versions")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<IReadOnlyList<TemplateVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplateVersions(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetTemplateVersionsQuery { TemplateId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = FsmsPolicies.ManageTemplates)]
    [ProducesResponseType(typeof(Result<TemplateDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = FsmsPolicies.ManageTemplates)]
    [ProducesResponseType(typeof(Result<TemplateDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTemplate(long id, [FromBody] UpdateTemplateCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { TemplateId = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Persists the form-builder definition JSON for a template (design/save).</summary>
    [HttpPut("{id:long}/definition")]
    [Authorize(Policy = FsmsPolicies.ManageTemplates)]
    [ProducesResponseType(typeof(Result<TemplateDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveDefinition(long id, [FromBody] SaveTemplateDefinitionCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { TemplateId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:long}/clone")]
    [Authorize(Policy = FsmsPolicies.ManageTemplates)]
    [ProducesResponseType(typeof(Result<TemplateDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloneTemplate(long id, [FromBody] CloneTemplateCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { TemplateId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = FsmsPolicies.ManageTemplates)]
    [ProducesResponseType(typeof(Result<TemplateDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Publish(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new PublishTemplateCommand { TemplateId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:long}/deprecate")]
    [Authorize(Policy = FsmsPolicies.ManageTemplates)]
    [ProducesResponseType(typeof(Result<TemplateDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deprecate(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeprecateTemplateCommand { TemplateId = id }, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id:long}/archive")]
    [Authorize(Policy = FsmsPolicies.ManageTemplates)]
    [ProducesResponseType(typeof(Result<TemplateDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Archive(long id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ArchiveTemplateCommand { TemplateId = id }, cancellationToken);
        return ToActionResult(result);
    }
}
