using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public class LabelRepository : ILabelRepository
    {
        private readonly string _conn;

        public LabelRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection missing");
        }

        public async Task<int> CreateLabelAsync(CreateLabelDto dto)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_CreateLabel", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelName", dto.LabelName);
            cmd.Parameters.AddWithValue("@EnterpriseId", dto.EnterpriseId);
            cmd.Parameters.AddWithValue("@PlanTypeId", dto.PlanTypeId);
            cmd.Parameters.AddWithValue("@RevenueSharePercent", dto.RevenueSharePercent);
            cmd.Parameters.AddWithValue("@Domain", (object?)dto.Domain ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@QCRequired", dto.QCRequired);
            cmd.Parameters.AddWithValue("@AgreementStartDate", (object?)dto.AgreementStartDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AgreementEndDate", (object?)dto.AgreementEndDate ?? DBNull.Value);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<LabelResponseDto?> GetLabelByIdAsync(int labelId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_GetLabelById", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", labelId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var label = new LabelResponseDto
                {
                    LabelId = Convert.ToInt32(reader["LabelID"]),
                    LabelName = reader["LabelName"]?.ToString() ?? string.Empty,
                    EnterpriseId = Convert.ToInt32(reader["EnterpriseID"]),
                    PlanTypeId = Convert.ToInt32(reader["PlanTypeID"]),
                    PlanTypeName = reader["PlanTypeName"]?.ToString() ?? string.Empty,
                    RevenueSharePercent = Convert.ToDecimal(reader["RevenueSharePercent"]),
                    Domain = reader["Domain"]?.ToString(),
                    SubDomain = reader["SubDomain"]?.ToString(),
                    QCRequired = Convert.ToBoolean(reader["QCRequired"]),
                    AgreementStartDate = reader["AgreementStartDate"] != DBNull.Value ? Convert.ToDateTime(reader["AgreementStartDate"]) : null,
                    AgreementEndDate = reader["AgreementEndDate"] != DBNull.Value ? Convert.ToDateTime(reader["AgreementEndDate"]) : null,
                    Status = reader["Status"]?.ToString() ?? string.Empty
                };

                if (reader.NextResult() && await reader.ReadAsync())
                {
                    label.Branding = new BrandingDto
                    {
                        LogoUrl = reader["LogoUrl"]?.ToString(),
                        FaviconUrl = reader["FaviconUrl"]?.ToString(),
                        PrimaryColor = reader["PrimaryColor"]?.ToString(),
                        SecondaryColor = reader["SecondaryColor"]?.ToString(),
                        FooterText = reader["FooterText"]?.ToString(),
                        EmailTemplateJson = reader["EmailTemplateJson"]?.ToString()
                    };
                }

                return label;
            }

            return null;
        }

        public async Task<bool> UpdateLabelAsync(int labelId, UpdateLabelDto dto)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand(@"
                UPDATE Labels
                SET RevenueSharePercent = CASE 
                        WHEN @RevenueSharePercent IS NOT NULL THEN @RevenueSharePercent 
                        ELSE RevenueSharePercent 
                    END,
                    Domain = CASE 
                        WHEN @Domain IS NOT NULL THEN @Domain 
                        ELSE Domain 
                    END,
                    OwnerEmail = CASE 
                        WHEN @OwnerEmail IS NOT NULL THEN @OwnerEmail 
                        ELSE OwnerEmail 
                    END,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE LabelID = @LabelId
                  AND (IsDeleted = 0 OR IsDeleted IS NULL)", conn);

            cmd.Parameters.AddWithValue("@LabelId", labelId);
            cmd.Parameters.AddWithValue("@RevenueSharePercent", dto.RevenueSharePercent.HasValue ? (object)dto.RevenueSharePercent.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@Domain", string.IsNullOrWhiteSpace(dto.Domain) ? DBNull.Value : dto.Domain);
            cmd.Parameters.AddWithValue("@OwnerEmail", string.IsNullOrWhiteSpace(dto.OwnerEmail) ? DBNull.Value : dto.OwnerEmail);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> ChangeStatusAsync(int labelId, string status)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_ChangeLabelStatus", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", labelId);
            cmd.Parameters.AddWithValue("@Status", status);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> AssignRoleAsync(int labelId, int userId, string role)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_AssignLabelRole", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", labelId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Role", role);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> RemoveRoleAsync(int labelId, int userId, string role)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_RemoveLabelRole", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", labelId);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Role", role);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> UpdateBrandingAsync(int labelId, UpdateBrandingDto dto)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_UpdateLabelBranding", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", labelId);
            cmd.Parameters.AddWithValue("@LogoUrl", (object?)dto.LogoUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FaviconUrl", (object?)dto.FaviconUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PrimaryColor", (object?)dto.PrimaryColor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SecondaryColor", (object?)dto.SecondaryColor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FooterText", (object?)dto.FooterText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EmailTemplateJson", (object?)dto.EmailTemplateJson ?? DBNull.Value);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }
    }
}

