using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using UDI_SSW_Prototype.Models;

namespace UDI_SSW_Prototype.Data
{
    public interface ICitizenRepository
    {
        Task<IEnumerable<Citizen>> GetAllAsync();
        Task<Citizen?> GetByDidAsync(string did);
        Task<int> AddAsync(Citizen citizen);
        Task<int> ToggleActiveAsync(string did, bool isActive);
        Task<int> UpdateLastActiveTimeAsync(string did);
        Task<int> UpdateLicenseNumberAsync(string did, string licenseNumber);
    }

    public class CitizenRepository : ICitizenRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public CitizenRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<Citizen>> GetAllAsync()
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "SELECT Did, Name, Birthdate, LicenseNumber, PublicKeyPem, PrivateKeyPem, IsActive, LastActiveTime FROM Citizens";
            return await conn.QueryAsync<Citizen>(sql);
        }

        public async Task<Citizen?> GetByDidAsync(string did)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "SELECT Did, Name, Birthdate, LicenseNumber, PublicKeyPem, PrivateKeyPem, IsActive, LastActiveTime FROM Citizens WHERE Did = @Did";
            return (await conn.QueryAsync<Citizen>(sql, new { Did = did })).FirstOrDefault();
        }

        public async Task<int> AddAsync(Citizen citizen)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = @"
                INSERT INTO Citizens (Did, Name, Birthdate, LicenseNumber, PublicKeyPem, PrivateKeyPem, IsActive, LastActiveTime)
                VALUES (@Did, @Name, @Birthdate, @LicenseNumber, @PublicKeyPem, @PrivateKeyPem, @IsActive, @LastActiveTime)";
            return await conn.ExecuteAsync(sql, citizen);
        }

        public async Task<int> ToggleActiveAsync(string did, bool isActive)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "UPDATE Citizens SET IsActive = @IsActive WHERE Did = @Did";
            return await conn.ExecuteAsync(sql, new { Did = did, IsActive = isActive });
        }

        public async Task<int> UpdateLastActiveTimeAsync(string did)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "UPDATE Citizens SET LastActiveTime = GETUTCDATE() WHERE Did = @Did";
            return await conn.ExecuteAsync(sql, new { Did = did });
        }

        public async Task<int> UpdateLicenseNumberAsync(string did, string licenseNumber)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "UPDATE Citizens SET LicenseNumber = @LicenseNumber WHERE Did = @Did";
            return await conn.ExecuteAsync(sql, new { Did = did, LicenseNumber = licenseNumber });
        }
    }
}
