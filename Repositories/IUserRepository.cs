using System;
using System.Collections.Generic;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public interface IUserRepository
    {
        Task<UserMeResponseDto?> GetUserProfileAsync(int userId);
        Task<bool> UpdateProfileAsync(int userId, UpdateProfileRequestDto dto);
        Task<bool> ChangePasswordAsync(int userId, string currentPasswordHash, string newPasswordHash);
        Task<object> GetUserEntitiesAsync(int userId);
        Task<bool> UserHasAccessToEntityAsync(int userId, string entityType, int entityId);
        Task<IEnumerable<ActivityLogDto>> GetActivityLogsAsync(int targetUserId, DateTime? fromUtc, DateTime? toUtc);
        Task LogAuditAsync(int actorUserId, string actionType, string description, string? targetType = null, string? targetId = null, string? ipAddress = null);
    }
}


