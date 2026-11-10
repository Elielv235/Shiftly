using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Shiftly.Server.Data;
using Shiftly.Server.Security;
using Shiftly.Shared.DTOs;
using Shiftly.Shared.Enums;
using Shiftly.Shared.Interfaces;

namespace Shiftly.Server.Services
{
    /// <summary>
    /// Microsoft Access (.accdb / System.Data.OleDb) implementation of IAuthService.
    /// Uses SHA-256 + unique cryptographic salt (PasswordHasher) and parameterized OleDbCommand queries.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class AuthService : IAuthService
    {
        private readonly AccessConnectionFactory _connectionFactory;

        public AuthService(AccessConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public Task<LoginResponseDTO?> AuthenticateAsync(LoginRequestDTO request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return Task.FromResult<LoginResponseDTO?>(null);
            }

            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();
                using var cmd = new OleDbCommand(
                    @"SELECT u.[UserId], u.[Username], u.[PasswordHash], u.[Salt], u.[FullName], u.[IsActive], r.[RoleName]
                      FROM [Users] u INNER JOIN [Roles] r ON u.[RoleId] = r.[RoleId]
                      WHERE LCase(u.[Username]) = ?",
                    connection);

                cmd.Parameters.Add(new OleDbParameter
                {
                    OleDbType = OleDbType.VarWChar,
                    Value = request.Username.Trim().ToLowerInvariant()
                });

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return Task.FromResult<LoginResponseDTO?>(null);

                bool isActive = reader.GetBoolean(5);
                if (!isActive)
                    return Task.FromResult<LoginResponseDTO?>(null);

                string storedHash = reader.GetString(2);
                string storedSalt = reader.GetString(3);

                if (!PasswordHasher.VerifyPassword(request.Password, storedHash, storedSalt))
                    return Task.FromResult<LoginResponseDTO?>(null);

                var response = new LoginResponseDTO
                {
                    UserId = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    FullName = reader.GetString(4),
                    RoleName = reader.GetString(6)
                };

                return Task.FromResult<LoginResponseDTO?>(response);
            }
            catch
            {
                return Task.FromResult<LoginResponseDTO?>(null);
            }
        }

        public Task<bool> RegisterUserAsync(UserRegisterDTO request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                request.Password.Length < 6)
            {
                return Task.FromResult(false);
            }

            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();

                string normalizedUsername = request.Username.Trim();
                if (normalizedUsername.Equals("guest", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(false); // SEC-M07 / SEC-M10: Guest cannot be registered in Users

                using (var existsCmd = new OleDbCommand("SELECT COUNT(*) FROM [Users] WHERE LCase([Username]) = ?", connection))
                {
                    existsCmd.Parameters.Add(new OleDbParameter
                    {
                        OleDbType = OleDbType.VarWChar,
                        Value = normalizedUsername.ToLowerInvariant()
                    });
                    if (Convert.ToInt32(existsCmd.ExecuteScalar()) > 0)
                        return Task.FromResult(false);
                }

                // SEC-M11: Always assign Worker role on registration
                int workerRoleId;
                using (var roleCmd = new OleDbCommand("SELECT [RoleId] FROM [Roles] WHERE [RoleName] = ?", connection))
                {
                    roleCmd.Parameters.Add(new OleDbParameter
                    {
                        OleDbType = OleDbType.VarWChar,
                        Value = nameof(UserRoleType.Worker)
                    });
                    object? roleObj = roleCmd.ExecuteScalar();
                    if (roleObj == null || roleObj == DBNull.Value)
                        return Task.FromResult(false);
                    workerRoleId = Convert.ToInt32(roleObj);
                }

                var (passwordHash, salt) = PasswordHasher.CreateHash(request.Password);

                using var insertCmd = new OleDbCommand(
                    @"INSERT INTO [Users]
                        ([Username], [PasswordHash], [Salt], [FullName], [Email], [PhoneNumber], [RoleId], [IsActive])
                      VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                    connection);

                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = normalizedUsername });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = passwordHash });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = salt });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = request.FullName?.Trim() ?? string.Empty });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.VarWChar, Value = request.Email?.Trim() ?? string.Empty });
                insertCmd.Parameters.Add(new OleDbParameter
                {
                    OleDbType = OleDbType.VarWChar,
                    Value = string.IsNullOrWhiteSpace(request.PhoneNumber) ? DBNull.Value : request.PhoneNumber.Trim()
                });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = workerRoleId });
                insertCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Boolean, Value = true });

                insertCmd.ExecuteNonQuery();
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<IEnumerable<UserListDTO>> GetAllUsersAsync()
        {
            var users = new List<UserListDTO>();
            using var connection = _connectionFactory.CreateOpenConnection();
            using var cmd = new OleDbCommand(
                @"SELECT u.[UserId], u.[Username], u.[FullName], u.[Email], r.[RoleName], u.[IsActive]
                  FROM [Users] u INNER JOIN [Roles] r ON u.[RoleId] = r.[RoleId]
                  ORDER BY u.[UserId] ASC",
                connection);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new UserListDTO
                {
                    UserId = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    FullName = reader.GetString(2),
                    Email = reader.GetString(3),
                    RoleName = reader.GetString(4),
                    IsActive = reader.GetBoolean(5)
                });
            }

            return Task.FromResult<IEnumerable<UserListDTO>>(users);
        }

        public Task<bool> UpdateUserRoleAsync(int targetUserId, int newRoleId)
        {
            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();

                using (var roleCheckCmd = new OleDbCommand("SELECT COUNT(*) FROM [Roles] WHERE [RoleId] = ?", connection))
                {
                    roleCheckCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = newRoleId });
                    if (Convert.ToInt32(roleCheckCmd.ExecuteScalar()) == 0)
                        return Task.FromResult(false);
                }

                using var updateCmd = new OleDbCommand("UPDATE [Users] SET [RoleId] = ? WHERE [UserId] = ?", connection);
                updateCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = newRoleId });
                updateCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = targetUserId });

                return Task.FromResult(updateCmd.ExecuteNonQuery() > 0);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> UpdateUserStatusAsync(int targetUserId, bool isActive)
        {
            try
            {
                using var connection = _connectionFactory.CreateOpenConnection();
                using var updateCmd = new OleDbCommand("UPDATE [Users] SET [IsActive] = ? WHERE [UserId] = ?", connection);
                updateCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Boolean, Value = isActive });
                updateCmd.Parameters.Add(new OleDbParameter { OleDbType = OleDbType.Integer, Value = targetUserId });

                return Task.FromResult(updateCmd.ExecuteNonQuery() > 0);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}
