using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using UDI_SSW_Prototype.Models;

namespace UDI_SSW_Prototype.Data
{
    public interface IKeyRegistryRepository
    {
        Task<IEnumerable<KeyRegistry>> GetAllAsync();
        Task<KeyRegistry?> GetByIssuerNameAsync(string issuerName);
        Task<int> AddAsync(KeyRegistry registry);
        Task<int> ToggleActiveAsync(string issuerName, bool isActive);
    }

    public class KeyRegistryRepository : IKeyRegistryRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public KeyRegistryRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<KeyRegistry>> GetAllAsync()
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "SELECT IssuerName, PublicKeyPem, PrivateKeyPem, IsActive FROM Keys";
            return await conn.QueryAsync<KeyRegistry>(sql);
        }

        public async Task<KeyRegistry?> GetByIssuerNameAsync(string issuerName)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "SELECT IssuerName, PublicKeyPem, PrivateKeyPem, IsActive FROM Keys WHERE IssuerName = @IssuerName";
            return (await conn.QueryAsync<KeyRegistry>(sql, new { IssuerName = issuerName })).FirstOrDefault();
        }

        public async Task<int> AddAsync(KeyRegistry registry)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = @"
                INSERT INTO Keys (IssuerName, PublicKeyPem, PrivateKeyPem, IsActive)
                VALUES (@IssuerName, @PublicKeyPem, @PrivateKeyPem, @IsActive)";
            return await conn.ExecuteAsync(sql, registry);
        }

        public async Task<int> ToggleActiveAsync(string issuerName, bool isActive)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "UPDATE Keys SET IsActive = @IsActive WHERE IssuerName = @IssuerName";
            return await conn.ExecuteAsync(sql, new { IssuerName = issuerName, IsActive = isActive });
        }
    }
}
