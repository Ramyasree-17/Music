
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public interface IPermissionRepository
    {
        Task<PermissionResult?> GetPermissionAsync(int userId, string moduleKey);
    }
}
