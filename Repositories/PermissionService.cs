using TunewaveAPIDB1.Repositories;

namespace TunewaveAPIDB1.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly IPermissionRepository _repo;

        public PermissionService(IPermissionRepository repo)
        {
            _repo = repo;
        }

        public async Task<bool> HasPermission(int userId, string module, string action)
        {
            var permission = await _repo.GetPermissionAsync(userId, module);

            if (permission == null)
                return false;

            return action.ToLower() switch
            {
                "view" => permission.CanView,
                "create" => permission.CanCreate,
                "edit" => permission.CanEdit,
                "delete" => permission.CanDelete,
                _ => false
            };
        }
    }
}
