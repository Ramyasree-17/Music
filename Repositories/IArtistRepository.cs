using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public interface IArtistRepository
    {
        Task<int> CreateArtistAsync(CreateArtistDto dto);
        Task<ArtistResponseDto?> GetArtistByIdAsync(int artistId);
        Task<bool> UpdateArtistAsync(int artistId, UpdateArtistDto dto);
        Task<bool> ClaimArtistAsync(int artistId, int userId, string reason);
        Task<bool> ReviewClaimAsync(int artistId, int claimId, bool approved, string? comments);
        Task<bool> GrantAccessAsync(int artistId, int userId, string role);
        Task<bool> RevokeAccessAsync(int artistId, int userId);
    }
}


