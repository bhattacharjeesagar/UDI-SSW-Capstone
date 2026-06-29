using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using UDI_SSW_Prototype.Models;

namespace UDI_SSW_Prototype.Data
{
    public interface IUserRepository
    {
        Task<UserMaster?> GetByUsernameAsync(string username);
        Task<UserMaster?> AuthenticateAsync(string username, string password);
        Task<int> AddAsync(UserMaster user);
        Task<int> LinkCitizenDidAsync(string username, string did);
    }

    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<UserMaster?> GetByUsernameAsync(string username)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "SELECT UserId, Username, PasswordHash, RoleId, FullName, AssociatedDid, IsActive FROM UserMaster WHERE Username = @Username";
            return (await conn.QueryAsync<UserMaster>(sql, new { Username = username })).FirstOrDefault();
        }

        public async Task<UserMaster?> AuthenticateAsync(string username, string password)
        {
            var user = await GetByUsernameAsync(username);
            if (user == null || !user.IsActive) return null;

            string hashedPwd = HashPassword(password);
            if (user.PasswordHash.Equals(hashedPwd, StringComparison.OrdinalIgnoreCase))
            {
                return user;
            }
            return null;
        }

        public async Task<int> AddAsync(UserMaster user)
        {
            user.PasswordHash = HashPassword(user.PasswordHash); // Hash before insert
            using var conn = _connectionFactory.CreateConnection();
            string sql = @"
                INSERT INTO UserMaster (Username, PasswordHash, RoleId, FullName, AssociatedDid, IsActive)
                VALUES (@Username, @PasswordHash, @RoleId, @FullName, @AssociatedDid, @IsActive)";
            return await conn.ExecuteAsync(sql, user);
        }

        public async Task<int> LinkCitizenDidAsync(string username, string did)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "UPDATE UserMaster SET AssociatedDid = @Did WHERE Username = @Username";
            return await conn.ExecuteAsync(sql, new { Did = did, Username = username });
        }

        public static string HashPassword(string password)
        {
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = SHA256.HashData(bytes);
            var sb = new StringBuilder();
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
