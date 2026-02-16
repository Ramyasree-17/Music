using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public interface IAddressRepository
    {
        Task<bool> ExistsAsync(int ownerId);
        Task<AddressResponseDto?> GetByOwnerAsync(int ownerId);
        Task<int> UpsertAsync(int ownerId, AddressUpsertRequestDto dto);
    }
}


