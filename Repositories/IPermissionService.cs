namespace TunewaveAPIDB1.Services
{
    public interface IPermissionService
    {
        Task<bool> HasPermission(int userId, string module, string action);
    }
}
