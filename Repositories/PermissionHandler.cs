using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TunewaveAPIDB1.Services;
using TunewaveAPIDB1.Authorization;

namespace TunewaveAPIDB1.Authorization
{
    public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionService _permissionService;

        public PermissionHandler(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null)
                return;

            var userId = int.Parse(userIdClaim.Value);

            var hasPermission = await _permissionService.HasPermission(
                userId,
                requirement.Module,
                requirement.Action
            );

            if (hasPermission)
                context.Succeed(requirement);
        }
    }
}
