using KH.Application.Fsms.FieldCatalog.Models;
using KH.Application.Fsms.FieldCatalog.Queries.GetFieldCatalogPaged;
using KH.Application.Fsms.Lookups.Cbus;
using KH.Application.Fsms.Lookups.Clusters;
using KH.Application.Fsms.Lookups.Departments;
using KH.Application.Fsms.Lookups.CustomerTypes;
using KH.Application.Fsms.Lookups.Contractors;
using KH.Application.Fsms.Lookups.FaTypes;
using KH.Application.Fsms.Lookups.OperationAreas;
using KH.Application.Fsms.Lookups.ReturnReasons;
using KH.Application.Fsms.Lookups.TaskTypes;
using KH.Application.Fsms.Lookups.Statuses;
using KH.Application.Fsms.Lookups.Branches;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Common;

namespace MK.FormEngine.API.Controllers;

/// <summary>FSMS shared lookup reference data (Departments, Branches, FA Types, Field Catalog).</summary>
[Authorize]
[Route("api/v1/fsms/lookups")]
public sealed class FsmsLookupsController : ApiControllerBase
{
    /// <summary>Paginated list of departments.</summary>
    [HttpGet("departments")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsDepartmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartments(
        [FromQuery] GetDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Create a new department.</summary>
    [HttpPost("departments")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDepartment(
        [FromBody] CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Update an existing department (including toggling IsActive for soft delete).</summary>
    [HttpPut("departments/{id:int}")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateDepartment(
        int id, [FromBody] UpdateDepartmentCommand command, CancellationToken cancellationToken)
    {
        // The route is the single source of truth for which row is being edited, so the body id is
        // stamped rather than compared — a client that omits it still targets the right record.
        var result = await Mediator.Send(command with { Id = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of clusters — the top of the org hierarchy.</summary>
    [HttpGet("clusters")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsClusterDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClusters(
        [FromQuery] GetClustersQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of CBUs — drives the cascading dropdown under a cluster.</summary>
    [HttpGet("cbus")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsCbuDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCbus(
        [FromQuery] GetCbusQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Create a new CBU.</summary>
    [HttpPost("cbus")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCbu(
        [FromBody] CreateCbuCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Update an existing CBU (including toggling IsActive for soft delete).</summary>
    [HttpPut("cbus/{id:int}")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCbu(
        int id, [FromBody] UpdateCbuCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { Id = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of branches / CBU branches.</summary>
    [HttpGet("branches")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsBranchDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBranches(
        [FromQuery] GetBranchesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Create a new branch.</summary>
    [HttpPost("branches")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBranch(
        [FromBody] CreateBranchCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Update an existing branch (including toggling IsActive for soft delete).</summary>
    [HttpPut("branches/{id:long}")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBranch(
        long id, [FromBody] UpdateBranchCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { Id = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of operation areas — a leaf of the org hierarchy, under a CBU and beside a branch.</summary>
    [HttpGet("operation-areas")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsOperationAreaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOperationAreas(
        [FromQuery] GetOperationAreasQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Create a new operation area.</summary>
    [HttpPost("operation-areas")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateOperationArea(
        [FromBody] CreateOperationAreaCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Update an existing operation area (including toggling IsActive for soft delete).</summary>
    [HttpPut("operation-areas/{id:long}")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOperationArea(
        long id, [FromBody] UpdateOperationAreaCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { Id = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of FA types / task types.</summary>
    [HttpGet("fa-types")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsFaTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFaTypes(
        [FromQuery] GetFaTypesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Create a new FA type.</summary>
    [HttpPost("fa-types")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateFaType(
        [FromBody] CreateFaTypeCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Update an existing FA type (including toggling IsActive for soft delete).</summary>
    [HttpPut("fa-types/{id:long}")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFaType(
        long id, [FromBody] UpdateFaTypeCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { Id = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of task types.</summary>
    [HttpGet("task-types")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsTaskTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTaskTypes(
        [FromQuery] GetTaskTypesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Create a new task type.</summary>
    [HttpPost("task-types")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTaskType(
        [FromBody] CreateTaskTypeCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Update an existing task type (including toggling IsActive for soft delete).</summary>
    [HttpPut("task-types/{id:long}")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTaskType(
        long id, [FromBody] UpdateTaskTypeCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { Id = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of customer types.</summary>
    [HttpGet("customer-types")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsCustomerTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerTypes(
        [FromQuery] GetCustomerTypesQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Create a new customer type.</summary>
    [HttpPost("customer-types")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCustomerType(
        [FromBody] CreateCustomerTypeCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Update an existing customer type (including toggling IsActive for soft delete).</summary>
    [HttpPut("customer-types/{id:long}")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateCustomerType(
        long id, [FromBody] UpdateCustomerTypeCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { Id = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of contractors / blanket POs.</summary>
    [HttpGet("contractors")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsContractorDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContractors(
        [FromQuery] GetContractorsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Create a new contractor.</summary>
    [HttpPost("contractors")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<int>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateContractor(
        [FromBody] CreateContractorCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Update an existing contractor (including toggling IsActive for soft delete).</summary>
    [HttpPut("contractors/{id:int}")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateContractor(
        int id, [FromBody] UpdateContractorCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { Id = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Every survey and assignment status code the system supports, with English and Arabic labels.
    /// Static reference data compiled into the domain's transition rules, so it is served from
    /// constants rather than an editable lookup table.
    /// </summary>
    [HttpGet("survey-statuses")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<SurveyStatusCatalogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSurveyStatuses(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSurveyStatusCatalogQuery(), cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of survey return reasons — the causes a reviewer can send work back for.</summary>
    [HttpGet("return-reasons")]
    [Authorize(Policy = FsmsPolicies.ViewSurveys)]
    [ProducesResponseType(typeof(Result<PagedResult<FsmsReturnReasonDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReturnReasons(
        [FromQuery] GetReturnReasonsQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Create a new return reason.</summary>
    [HttpPost("return-reasons")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateReturnReason(
        [FromBody] CreateReturnReasonCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Update an existing return reason (including toggling IsActive for soft delete).</summary>
    [HttpPut("return-reasons/{id:long}")]
    [Authorize(Policy = FsmsPolicies.ManageLookups)]
    [ProducesResponseType(typeof(Result<long>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateReturnReason(
        long id, [FromBody] UpdateReturnReasonCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command with { Id = id }, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>Paginated list of field catalog entries.</summary>
    [HttpGet("field-catalog")]
    [Authorize(Policy = FsmsPolicies.ViewTemplates)]
    [ProducesResponseType(typeof(Result<PagedResult<FieldCatalogItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFieldCatalog(
        [FromQuery] GetFieldCatalogPagedQuery query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return ToActionResult(result);
    }
}
