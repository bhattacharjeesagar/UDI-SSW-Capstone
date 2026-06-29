using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using UDI_SSW_Prototype.Models;

namespace UDI_SSW_Prototype.Data
{
    public interface ICredentialRepository
    {
        Task<IEnumerable<VerifiableCredential>> GetBySubjectDidAsync(string subjectDid);
        Task<IEnumerable<VerifiableCredential>> GetAllAsync();
        Task<VerifiableCredential?> GetByIdAsync(int id);
        Task<int> AddAsync(VerifiableCredential credential);
        Task<int> RevokeAsync(int id);
    }

    public class CredentialRepository : ICredentialRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public CredentialRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<VerifiableCredential>> GetBySubjectDidAsync(string subjectDid)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "SELECT Id, SubjectDid, IssuerName, ClaimName, ClaimValue, SignedPayload, IsRevoked, IssuedAt FROM Credentials WHERE SubjectDid = @SubjectDid";
            return await conn.QueryAsync<VerifiableCredential>(sql, new { SubjectDid = subjectDid });
        }

        public async Task<IEnumerable<VerifiableCredential>> GetAllAsync()
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "SELECT Id, SubjectDid, IssuerName, ClaimName, ClaimValue, SignedPayload, IsRevoked, IssuedAt FROM Credentials";
            return await conn.QueryAsync<VerifiableCredential>(sql);
        }

        public async Task<VerifiableCredential?> GetByIdAsync(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "SELECT Id, SubjectDid, IssuerName, ClaimName, ClaimValue, SignedPayload, IsRevoked, IssuedAt FROM Credentials WHERE Id = @Id";
            return (await conn.QueryAsync<VerifiableCredential>(sql, new { Id = id })).FirstOrDefault();
        }

        public async Task<int> AddAsync(VerifiableCredential credential)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = @"
                INSERT INTO Credentials (SubjectDid, IssuerName, ClaimName, ClaimValue, SignedPayload, IsRevoked, IssuedAt)
                VALUES (@SubjectDid, @IssuerName, @ClaimName, @ClaimValue, @SignedPayload, @IsRevoked, @IssuedAt)";
            return await conn.ExecuteAsync(sql, credential);
        }

        public async Task<int> RevokeAsync(int id)
        {
            using var conn = _connectionFactory.CreateConnection();
            string sql = "UPDATE Credentials SET IsRevoked = 1 WHERE Id = @Id";
            return await conn.ExecuteAsync(sql, new { Id = id });
        }
    }
}
