using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public interface IEnterpriseRepository
    {
        Task<int> CreateEnterpriseAsync(CreateEnterpriseDto dto);
        Task<EnterpriseResponseDto?> GetEnterpriseByIdAsync(int enterpriseId);
        Task<bool> UpdateEnterpriseAsync(int enterpriseId, UpdateEnterpriseDto dto);
        Task<bool> ChangeStatusAsync(int enterpriseId, string status);
        Task<List<EnterpriseLabelDto>> GetLabelsAsync(int enterpriseId);
        Task<bool> TransferLabelAsync(int labelId, int toEnterpriseId);
    }
}


