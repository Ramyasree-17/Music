using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public interface ITeamRepository
    {
        Task<int> CreateTeamAsync(CreateTeamDto dto, int createdBy);
        Task AddTeamMemberAsync(AddTeamMemberDto dto);
    }
}
