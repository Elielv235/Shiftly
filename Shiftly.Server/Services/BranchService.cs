using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Shiftly.Server.Data;
using Shiftly.Shared.DTOs;

namespace Shiftly.Server.Services
{
    /// <summary>
    /// Microsoft Access (.accdb / System.Data.OleDb) data service for Cycle 2
    /// Dashboard Statistics and Worker Availabilities (keeping all DB code out of controllers).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class BranchService
    {
        private readonly AccessConnectionFactory _connectionFactory;

        public BranchService(AccessConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public Task<DashboardStatsDTO> GetDashboardStatsAsync()
        {
            using var connection = _connectionFactory.CreateOpenConnection();

            var stats = new DashboardStatsDTO
            {
                ActiveWorkersCount = ExecuteCount(connection, "SELECT COUNT(*) FROM [Users] WHERE [IsActive] = True"),
                PendingAvailabilitiesCount = ExecuteCount(connection, "SELECT COUNT(*) FROM [Availabilities] WHERE [Status] = 'Pending'"),
                MorningRequestsCount = ExecuteCount(connection, "SELECT COUNT(*) FROM [Availabilities] WHERE [CanWorkMorning] = True"),
                EveningRequestsCount = ExecuteCount(connection, "SELECT COUNT(*) FROM [Availabilities] WHERE [CanWorkEvening] = True"),
                DefinedShiftsCount = ExecuteCount(connection, "SELECT COUNT(*) FROM [Shifts]")
            };

            return Task.FromResult(stats);
        }

        public Task<int> SubmitAvailabilityAsync(WorkerAvailabilityDTO dto)
        {
            using var connection = _connectionFactory.CreateOpenConnection();

            DateTime cleanDate = new DateTime(dto.TargetDate.Year, dto.TargetDate.Month, dto.TargetDate.Day);

            using (var cmd = new OleDbCommand(
                @"INSERT INTO [Availabilities] ([UserId], [TargetDate], [CanWorkMorning], [CanWorkEvening], [Notes], [Status])
                  VALUES (?, ?, ?, ?, ?, ?)",
                connection))
            {
                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer,  Value = dto.UserId });
                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Date,     Value = cleanDate });
                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Boolean,  Value = dto.CanWorkMorning });
                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Boolean,  Value = dto.CanWorkEvening });
                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = string.IsNullOrWhiteSpace(dto.Notes) ? DBNull.Value : dto.Notes.Trim() });
                cmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = "Pending" });
                cmd.ExecuteNonQuery();
            }

            using var idCmd = new OleDbCommand("SELECT @@IDENTITY", connection);
            int newId = Convert.ToInt32(idCmd.ExecuteScalar());
            return Task.FromResult(newId);
        }

        public Task<List<AvailabilityViewDTO>> GetBranchAvailabilitiesAsync()
        {
            var list = new List<AvailabilityViewDTO>();
            using var connection = _connectionFactory.CreateOpenConnection();

            using var cmd = new OleDbCommand(
                @"SELECT a.[AvailabilityId], a.[UserId], u.[FullName], a.[TargetDate], a.[CanWorkMorning], a.[CanWorkEvening], a.[Notes], a.[Status]
                  FROM [Availabilities] a INNER JOIN [Users] u ON a.[UserId] = u.[UserId]
                  ORDER BY a.[TargetDate] ASC",
                connection);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new AvailabilityViewDTO
                {
                    AvailabilityId = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    WorkerName = reader.GetString(2),
                    TargetDate = reader.GetDateTime(3),
                    CanWorkMorning = reader.GetBoolean(4),
                    CanWorkEvening = reader.GetBoolean(5),
                    Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Status = reader.GetString(7)
                });
            }

            return Task.FromResult(list);
        }

        private static int ExecuteCount(OleDbConnection connection, string sql)
        {
            using var cmd = new OleDbCommand(sql, connection);
            object? result = cmd.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }
    }
}
