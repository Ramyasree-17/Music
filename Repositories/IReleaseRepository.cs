using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public interface IReleaseRepository
    {
        Task<int> CreateReleaseAsync(CreateReleaseDto dto);
        Task<ReleaseResponseDto?> GetReleaseByIdAsync(int releaseId);
        Task<bool> UpdateReleaseAsync(int releaseId, UpdateReleaseDto dto);
        Task<bool> DeleteReleaseAsync(int releaseId);
        Task<bool> SubmitReleaseAsync(int releaseId);
        Task<bool> TakedownReleaseAsync(int releaseId, string reason);
    }
}


