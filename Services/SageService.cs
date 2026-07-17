using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PontBascule.Models;
using System;
using System.Threading.Tasks;

namespace PontBascule.Services
{
    public class SageService : ISageService
    {
        private readonly SageDatabaseConfiguration _config;

        public SageService(IConfiguration configuration)
        {
            _config = configuration.GetSection("Sage").Get<SageDatabaseConfiguration>()
                      ?? new SageDatabaseConfiguration();
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = new SqlConnection(BuildConnectionString());
                await connection.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> SendOperationAsync(WeighingOperation operation)
        {
            using var connection = new SqlConnection(BuildConnectionString());
            await connection.OpenAsync();

            // Replace this with the real Sage-approved table/procedure.
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO WeighbridgeOperations (
                    TicketNumber,
                    EntryType,
                    TruckNumber,
                    Product,
                    TotalNetWeight,
                    CreatedAt,
                    CompletedAt
                )
                VALUES (
                    @TicketNumber,
                    @EntryType,
                    @TruckNumber,
                    @Product,
                    @TotalNetWeight,
                    @CreatedAt,
                    @CompletedAt
                );
            ";

            command.Parameters.AddWithValue("@TicketNumber", operation.TicketNumber);
            command.Parameters.AddWithValue("@EntryType", operation.EntryType.ToString());
            command.Parameters.AddWithValue("@TruckNumber", operation.TruckNumber);
            command.Parameters.AddWithValue("@Product", operation.Product);
            command.Parameters.AddWithValue("@TotalNetWeight", operation.TotalNetWeight);
            command.Parameters.AddWithValue("@CreatedAt", operation.CreatedAt);
            command.Parameters.AddWithValue("@CompletedAt", operation.CompletedAt ?? (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();

            return operation.TicketNumber;
        }

        private string BuildConnectionString()
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = _config.Server,
                InitialCatalog = _config.Database,
                UserID = _config.Username,
                Password = _config.Password,
                TrustServerCertificate = _config.TrustServerCertificate
            };

            return builder.ConnectionString;
        }
    }
}