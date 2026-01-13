using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public interface ILabelRepository
    {
        Task<int> CreateLabelAsync(CreateLabelDto dto);
        Task<LabelResponseDto?> GetLabelByIdAsync(int labelId);
        Task<bool> UpdateLabelAsync(int labelId, UpdateLabelDto dto);
        Task<bool> ChangeStatusAsync(int labelId, string status);
        Task<bool> AssignRoleAsync(int labelId, int userId, string role);
        Task<bool> RemoveRoleAsync(int labelId, int userId, string role);
        Task<bool> UpdateBrandingAsync(int labelId, UpdateBrandingDto dto);
    }
}


