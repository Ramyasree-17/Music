using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TunewaveAPIDB1.Models;

namespace TunewaveAPIDB1.Repositories
{
    public class EnterpriseRepository : IEnterpriseRepository
    {
        private readonly string _conn;

        public EnterpriseRepository(IConfiguration cfg)
        {
            _conn = cfg.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("DefaultConnection missing");
        }

        public async Task<int> CreateEnterpriseAsync(CreateEnterpriseDto dto)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_CreateEnterprise_AutoOwner", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@EnterpriseName", dto.EnterpriseName);
            cmd.Parameters.AddWithValue("@Domain", (object?)dto.Domain ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RevenueShare", dto.RevenueSharePercent);
            cmd.Parameters.AddWithValue("@QCRequired", dto.QCRequired);
            cmd.Parameters.AddWithValue("@OwnerEmail", dto.OwnerEmail);
            cmd.Parameters.AddWithValue("@AgreementStartDate", (object?)dto.AgreementStartDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AgreementEndDate", (object?)dto.AgreementEndDate ?? DBNull.Value);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<EnterpriseResponseDto?> GetEnterpriseByIdAsync(int enterpriseId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_GetEnterpriseById", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new EnterpriseResponseDto
                {
                    EnterpriseId = Convert.ToInt32(reader["EnterpriseID"]),
                    EnterpriseName = reader["EnterpriseName"]?.ToString() ?? string.Empty,
                    Domain = reader["Domain"]?.ToString(),
                    RevenueSharePercent = Convert.ToDecimal(reader["RevenueShare"]),
                    QCRequired = Convert.ToBoolean(reader["QCRequired"]),
                    OwnerUserId = reader["OwnerUserID"] != DBNull.Value ? Convert.ToInt32(reader["OwnerUserID"]) : null,
                    AgreementStartDate = reader["AgreementStartDate"] != DBNull.Value ? Convert.ToDateTime(reader["AgreementStartDate"]) : null,
                    AgreementEndDate = reader["AgreementEndDate"] != DBNull.Value ? Convert.ToDateTime(reader["AgreementEndDate"]) : null,
                    Status = reader["Status"]?.ToString() ?? string.Empty,
                    CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                };
            }

            return null;
        }

        public async Task<bool> UpdateEnterpriseAsync(int enterpriseId, UpdateEnterpriseDto dto)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_UpdateEnterprise", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
            cmd.Parameters.AddWithValue("@Domain", (object?)dto.Domain ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RevenueShare", dto.RevenueSharePercent.HasValue ? (object)dto.RevenueSharePercent.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@QCRequired", dto.QCRequired.HasValue ? (object)dto.QCRequired.Value : DBNull.Value);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<bool> ChangeStatusAsync(int enterpriseId, string status)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_Enterprises_ChangeStatus", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);
            cmd.Parameters.AddWithValue("@Status", status);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }

        public async Task<List<EnterpriseLabelDto>> GetLabelsAsync(int enterpriseId)
        {
            var labels = new List<EnterpriseLabelDto>();

            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_GetEnterpriseLabels", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@EnterpriseId", enterpriseId);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                labels.Add(new EnterpriseLabelDto
                {
                    LabelId = Convert.ToInt32(reader["LabelID"]),
                    LabelName = reader["LabelName"]?.ToString() ?? string.Empty,
                    PlanType = reader["PlanType"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? string.Empty,
                    RevenueSharePercent = Convert.ToDecimal(reader["RevenueShare"])
                });
            }

            return labels;
        }

        public async Task<bool> TransferLabelAsync(int labelId, int toEnterpriseId)
        {
            using var conn = new SqlConnection(_conn);
            using var cmd = new SqlCommand("sp_TransferLabel", conn)
            {
                CommandType = CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@LabelId", labelId);
            cmd.Parameters.AddWithValue("@ToEnterpriseId", toEnterpriseId);

            await conn.OpenAsync();
            var result = await cmd.ExecuteNonQueryAsync();
            return result > 0;
        }
    }
}
