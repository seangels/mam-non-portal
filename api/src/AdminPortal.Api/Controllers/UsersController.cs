using AdminPortal.Application.Common.Models;
using AdminPortal.Application.Common.Mediator;
using AdminPortal.Application.Users;
using AdminPortal.Application.Users.Commands;
using AdminPortal.Application.Users.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminPortal.Api.Controllers;

[ApiController]
[Authorize(Policy = "PortalManagers")]
[Route("api/v1/users")]
public sealed class UsersController(IAppMediator mediator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<UserResponse>>> List(
        [FromQuery] UserListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListUsersQuery(query), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetUserQuery(id), cancellationToken));

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var created = await mediator.Send(new CreateUserCommand(request), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new UpdateUserCommand(id, request), cancellationToken));

    [HttpPut("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        Guid id,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new ChangeUserPasswordCommand(id, request), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteUserCommand(id), cancellationToken);
        return NoContent();
    }
}
