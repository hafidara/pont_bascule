using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PontBascule.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PontBascule.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            var config = configuration.GetSection("Database").Get<SageDatabaseConfiguration>()
                         ?? new SageDatabaseConfiguration();

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = config.Server,
                InitialCatalog = config.Database,
                UserID = config.Username,
                Password = config.Password,
                TrustServerCertificate = config.TrustServerCertificate,
                MultipleActiveResultSets = true
            };

            _connectionString = builder.ConnectionString;
        }

        public async Task InitializeDatabaseAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                IF OBJECT_ID('dbo.Weighings', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Weighings (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Timestamp DATETIME2 NOT NULL,
                        TruckNumber NVARCHAR(100) NOT NULL,
                        Transporter NVARCHAR(200) NULL,
                        Product NVARCHAR(200) NULL,
                        Weight DECIMAL(18,3) NOT NULL,
                        WeighingType INT NOT NULL,
                        SapDocumentNumber NVARCHAR(100) NULL,
                        SentToSap BIT NOT NULL DEFAULT 0,
                        Notes NVARCHAR(MAX) NULL
                    );
                END;

                IF OBJECT_ID('dbo.WeighingOperations', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.WeighingOperations (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        TicketNumber NVARCHAR(100) NOT NULL,
                        EntryType INT NOT NULL,
                        Status INT NOT NULL,
                        TruckNumber NVARCHAR(100) NOT NULL,
                        Transporter NVARCHAR(200) NULL,
                        Product NVARCHAR(200) NULL,
                        SageID VARCHAR(100) NULL,
                        CustomerCode NVARCHAR(100) NULL,
                        SupplierCode NVARCHAR(100) NULL,
                        DriverName NVARCHAR(200) NULL,
                        SimpleWeight DECIMAL(18,3) NULL,
                        TotalNetWeight DECIMAL(18,3) NOT NULL DEFAULT 0,
                        SageSyncStatus INT NOT NULL DEFAULT 0,
                        SageDocumentNumber NVARCHAR(100) NULL,
                        DigitalTicketPath NVARCHAR(1000) NULL,
                        DigitalTicketGeneratedAt DATETIME2 NULL,
                        PaperlessDocumentId NVARCHAR(100) NULL,
                        PaperlessDocumentUrl NVARCHAR(1000) NULL,
                        PaperlessUploadedAt DATETIME2 NULL,
                        Notes NVARCHAR(MAX) NULL,
                        CreatedAt DATETIME2 NOT NULL,
                        CompletedAt DATETIME2 NULL
                    );
                END;

                IF OBJECT_ID('dbo.WeighingCycles', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.WeighingCycles (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        OperationId INT NOT NULL,
                        CycleNumber INT NOT NULL,
                        EntryWeight DECIMAL(18,3) NULL,
                        EntryTime DATETIME2 NULL,
                        ExitWeight DECIMAL(18,3) NULL,
                        ExitTime DATETIME2 NULL,
                        NetWeight DECIMAL(18,3) NOT NULL DEFAULT 0,
                        CONSTRAINT FK_WeighingCycles_WeighingOperations
                            FOREIGN KEY (OperationId) REFERENCES dbo.WeighingOperations(Id)
                    );
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Weighings_Timestamp' AND object_id = OBJECT_ID('dbo.Weighings'))
                    CREATE INDEX IX_Weighings_Timestamp ON dbo.Weighings(Timestamp DESC);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Weighings_TruckNumber' AND object_id = OBJECT_ID('dbo.Weighings'))
                    CREATE INDEX IX_Weighings_TruckNumber ON dbo.Weighings(TruckNumber);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WeighingOperations_TruckNumber' AND object_id = OBJECT_ID('dbo.WeighingOperations'))
                    CREATE INDEX IX_WeighingOperations_TruckNumber ON dbo.WeighingOperations(TruckNumber);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WeighingOperations_Status' AND object_id = OBJECT_ID('dbo.WeighingOperations'))
                    CREATE INDEX IX_WeighingOperations_Status ON dbo.WeighingOperations(Status);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WeighingOperations_CreatedAt' AND object_id = OBJECT_ID('dbo.WeighingOperations'))
                    CREATE INDEX IX_WeighingOperations_CreatedAt ON dbo.WeighingOperations(CreatedAt DESC);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WeighingCycles_OperationId' AND object_id = OBJECT_ID('dbo.WeighingCycles'))
                    CREATE INDEX IX_WeighingCycles_OperationId ON dbo.WeighingCycles(OperationId);

                IF COL_LENGTH('dbo.WeighingOperations', 'SageID') IS NULL
                    ALTER TABLE dbo.WeighingOperations ADD SageID VARCHAR(100) NULL;
            ";

            await command.ExecuteNonQueryAsync();
        }

        public async Task<int> SaveWeighingAsync(Weighing weighing)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO dbo.Weighings (Timestamp, TruckNumber, Transporter, Product, Weight, WeighingType, SapDocumentNumber, SentToSap, Notes)
                OUTPUT INSERTED.Id
                VALUES (@Timestamp, @TruckNumber, @Transporter, @Product, @Weight, @WeighingType, @SapDocumentNumber, @SentToSap, @Notes);
            ";

            AddWeighingParameters(command, weighing);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<Weighing?> GetWeighingByIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM dbo.Weighings WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapWeighing(reader) : null;
        }

        public async Task<List<Weighing>> GetRecentWeighingsAsync(int count = 50)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT TOP (@Count) *
                FROM dbo.Weighings
                ORDER BY Timestamp DESC;
            ";
            command.Parameters.AddWithValue("@Count", count);

            var weighings = new List<Weighing>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                weighings.Add(MapWeighing(reader));
            }

            return weighings;
        }

        public async Task UpdateWeighingAsync(Weighing weighing)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE dbo.Weighings
                SET SapDocumentNumber = @SapDocumentNumber,
                    SentToSap = @SentToSap,
                    Notes = @Notes
                WHERE Id = @Id;
            ";

            command.Parameters.AddWithValue("@Id", weighing.Id);
            command.Parameters.AddWithValue("@SapDocumentNumber", ToDbValue(weighing.SapDocumentNumber));
            command.Parameters.AddWithValue("@SentToSap", weighing.SentToSap);
            command.Parameters.AddWithValue("@Notes", ToDbValue(weighing.Notes));

            await command.ExecuteNonQueryAsync();
        }

        public async Task<int> CreateOperationAsync(WeighingOperation operation)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO dbo.WeighingOperations (
                    TicketNumber, EntryType, Status, TruckNumber, Transporter, Product,
                    SageID, CustomerCode, SupplierCode, DriverName, SimpleWeight, TotalNetWeight,
                    SageSyncStatus, SageDocumentNumber, DigitalTicketPath, DigitalTicketGeneratedAt,
                    PaperlessDocumentId, PaperlessDocumentUrl, PaperlessUploadedAt, Notes, CreatedAt, CompletedAt
                )
                OUTPUT INSERTED.Id
                VALUES (
                    @TicketNumber, @EntryType, @Status, @TruckNumber, @Transporter, @Product,
                    @SageID, @CustomerCode, @SupplierCode, @DriverName, @SimpleWeight, @TotalNetWeight,
                    @SageSyncStatus, @SageDocumentNumber, @DigitalTicketPath, @DigitalTicketGeneratedAt,
                    @PaperlessDocumentId, @PaperlessDocumentUrl, @PaperlessUploadedAt, @Notes, @CreatedAt, @CompletedAt
                );
            ";

            AddOperationParameters(command, operation);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateOperationAsync(WeighingOperation operation)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE dbo.WeighingOperations
                SET TicketNumber = @TicketNumber,
                    EntryType = @EntryType,
                    Status = @Status,
                    TruckNumber = @TruckNumber,
                    Transporter = @Transporter,
                    Product = @Product,
                    SageID = @SageID,
                    CustomerCode = @CustomerCode,
                    SupplierCode = @SupplierCode,
                    DriverName = @DriverName,
                    SimpleWeight = @SimpleWeight,
                    TotalNetWeight = @TotalNetWeight,
                    SageSyncStatus = @SageSyncStatus,
                    SageDocumentNumber = @SageDocumentNumber,
                    DigitalTicketPath = @DigitalTicketPath,
                    DigitalTicketGeneratedAt = @DigitalTicketGeneratedAt,
                    PaperlessDocumentId = @PaperlessDocumentId,
                    PaperlessDocumentUrl = @PaperlessDocumentUrl,
                    PaperlessUploadedAt = @PaperlessUploadedAt,
                    Notes = @Notes,
                    CreatedAt = @CreatedAt,
                    CompletedAt = @CompletedAt
                WHERE Id = @Id;
            ";

            AddOperationParameters(command, operation);
            command.Parameters.AddWithValue("@Id", operation.Id);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<WeighingOperation?> GetOperationByIdAsync(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM dbo.WeighingOperations WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapOperation(reader) : null;
        }

        public async Task<WeighingOperation?> GetOpenOperationByTruckAsync(string truckNumber)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT TOP (1) *
                FROM dbo.WeighingOperations
                WHERE TruckNumber = @TruckNumber
                  AND Status IN (@Open, @PendingExit)
                ORDER BY CreatedAt DESC;
            ";
            command.Parameters.AddWithValue("@TruckNumber", truckNumber);
            command.Parameters.AddWithValue("@Open", (int)OperationStatus.Open);
            command.Parameters.AddWithValue("@PendingExit", (int)OperationStatus.PendingExit);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapOperation(reader) : null;
        }

        public async Task<List<WeighingOperation>> GetRecentOperationsAsync(int count = 50)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT TOP (@Count) *
                FROM dbo.WeighingOperations
                ORDER BY CreatedAt DESC;
            ";
            command.Parameters.AddWithValue("@Count", count);

            var operations = new List<WeighingOperation>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                operations.Add(MapOperation(reader));
            }

            return operations;
        }

        public async Task<int> CreateCycleAsync(WeighingCycle cycle)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO dbo.WeighingCycles (OperationId, CycleNumber, EntryWeight, EntryTime, ExitWeight, ExitTime, NetWeight)
                OUTPUT INSERTED.Id
                VALUES (@OperationId, @CycleNumber, @EntryWeight, @EntryTime, @ExitWeight, @ExitTime, @NetWeight);
            ";

            AddCycleParameters(command, cycle);
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateCycleAsync(WeighingCycle cycle)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE dbo.WeighingCycles
                SET OperationId = @OperationId,
                    CycleNumber = @CycleNumber,
                    EntryWeight = @EntryWeight,
                    EntryTime = @EntryTime,
                    ExitWeight = @ExitWeight,
                    ExitTime = @ExitTime,
                    NetWeight = @NetWeight
                WHERE Id = @Id;
            ";

            AddCycleParameters(command, cycle);
            command.Parameters.AddWithValue("@Id", cycle.Id);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<WeighingCycle>> GetCyclesByOperationIdAsync(int operationId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT *
                FROM dbo.WeighingCycles
                WHERE OperationId = @OperationId
                ORDER BY CycleNumber ASC;
            ";
            command.Parameters.AddWithValue("@OperationId", operationId);

            var cycles = new List<WeighingCycle>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                cycles.Add(MapCycle(reader));
            }

            return cycles;
        }

        public async Task<WeighingCycle?> GetLastOpenCycleAsync(int operationId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT TOP (1) *
                FROM dbo.WeighingCycles
                WHERE OperationId = @OperationId
                  AND EntryWeight IS NOT NULL
                  AND ExitWeight IS NULL
                ORDER BY CycleNumber DESC;
            ";
            command.Parameters.AddWithValue("@OperationId", operationId);

            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? MapCycle(reader) : null;
        }

        private static void AddWeighingParameters(SqlCommand command, Weighing weighing)
        {
            command.Parameters.AddWithValue("@Timestamp", weighing.Timestamp);
            command.Parameters.AddWithValue("@TruckNumber", weighing.TruckNumber);
            command.Parameters.AddWithValue("@Transporter", ToDbValue(weighing.Transporter));
            command.Parameters.AddWithValue("@Product", ToDbValue(weighing.Product));
            command.Parameters.AddWithValue("@Weight", weighing.Weight);
            command.Parameters.AddWithValue("@WeighingType", (int)weighing.WeighingType);
            command.Parameters.AddWithValue("@SapDocumentNumber", ToDbValue(weighing.SapDocumentNumber));
            command.Parameters.AddWithValue("@SentToSap", weighing.SentToSap);
            command.Parameters.AddWithValue("@Notes", ToDbValue(weighing.Notes));
        }

        private static void AddOperationParameters(SqlCommand command, WeighingOperation operation)
        {
            command.Parameters.AddWithValue("@TicketNumber", operation.TicketNumber);
            command.Parameters.AddWithValue("@EntryType", (int)operation.EntryType);
            command.Parameters.AddWithValue("@Status", (int)operation.Status);
            command.Parameters.AddWithValue("@TruckNumber", operation.TruckNumber);
            command.Parameters.AddWithValue("@Transporter", ToDbValue(operation.Transporter));
            command.Parameters.AddWithValue("@Product", ToDbValue(operation.Product));
            command.Parameters.AddWithValue("@SageID", ToDbValue(operation.SageID));
            command.Parameters.AddWithValue("@CustomerCode", ToDbValue(operation.CustomerCode));
            command.Parameters.AddWithValue("@SupplierCode", ToDbValue(operation.SupplierCode));
            command.Parameters.AddWithValue("@DriverName", ToDbValue(operation.DriverName));
            command.Parameters.AddWithValue("@SimpleWeight", ToDbValue(operation.SimpleWeight));
            command.Parameters.AddWithValue("@TotalNetWeight", operation.TotalNetWeight);
            command.Parameters.AddWithValue("@SageSyncStatus", (int)operation.SageSyncStatus);
            command.Parameters.AddWithValue("@SageDocumentNumber", ToDbValue(operation.SageDocumentNumber));
            command.Parameters.AddWithValue("@DigitalTicketPath", ToDbValue(operation.DigitalTicketPath));
            command.Parameters.AddWithValue("@DigitalTicketGeneratedAt", ToDbValue(operation.DigitalTicketGeneratedAt));
            command.Parameters.AddWithValue("@PaperlessDocumentId", ToDbValue(operation.PaperlessDocumentId));
            command.Parameters.AddWithValue("@PaperlessDocumentUrl", ToDbValue(operation.PaperlessDocumentUrl));
            command.Parameters.AddWithValue("@PaperlessUploadedAt", ToDbValue(operation.PaperlessUploadedAt));
            command.Parameters.AddWithValue("@Notes", ToDbValue(operation.Notes));
            command.Parameters.AddWithValue("@CreatedAt", operation.CreatedAt);
            command.Parameters.AddWithValue("@CompletedAt", ToDbValue(operation.CompletedAt));
        }

        private static void AddCycleParameters(SqlCommand command, WeighingCycle cycle)
        {
            command.Parameters.AddWithValue("@OperationId", cycle.OperationId);
            command.Parameters.AddWithValue("@CycleNumber", cycle.CycleNumber);
            command.Parameters.AddWithValue("@EntryWeight", ToDbValue(cycle.EntryWeight));
            command.Parameters.AddWithValue("@EntryTime", ToDbValue(cycle.EntryTime));
            command.Parameters.AddWithValue("@ExitWeight", ToDbValue(cycle.ExitWeight));
            command.Parameters.AddWithValue("@ExitTime", ToDbValue(cycle.ExitTime));
            command.Parameters.AddWithValue("@NetWeight", cycle.NetWeight);
        }

        private static Weighing MapWeighing(SqlDataReader reader)
        {
            return new Weighing
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp")),
                TruckNumber = reader.GetString(reader.GetOrdinal("TruckNumber")),
                Transporter = GetString(reader, "Transporter"),
                Product = GetString(reader, "Product"),
                Weight = reader.GetDecimal(reader.GetOrdinal("Weight")),
                WeighingType = (WeighingType)reader.GetInt32(reader.GetOrdinal("WeighingType")),
                SapDocumentNumber = GetNullableString(reader, "SapDocumentNumber"),
                SentToSap = reader.GetBoolean(reader.GetOrdinal("SentToSap")),
                Notes = GetNullableString(reader, "Notes")
            };
        }

        private static WeighingOperation MapOperation(SqlDataReader reader)
        {
            return new WeighingOperation
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                TicketNumber = reader.GetString(reader.GetOrdinal("TicketNumber")),
                EntryType = (EntryType)reader.GetInt32(reader.GetOrdinal("EntryType")),
                Status = (OperationStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                TruckNumber = reader.GetString(reader.GetOrdinal("TruckNumber")),
                Transporter = GetString(reader, "Transporter"),
                Product = GetString(reader, "Product"),
                SageID = GetString(reader, "SageID"),
                CustomerCode = GetString(reader, "CustomerCode"),
                SupplierCode = GetString(reader, "SupplierCode"),
                DriverName = GetString(reader, "DriverName"),
                SimpleWeight = GetNullableDecimal(reader, "SimpleWeight"),
                TotalNetWeight = reader.GetDecimal(reader.GetOrdinal("TotalNetWeight")),
                SageSyncStatus = (SageSyncStatus)reader.GetInt32(reader.GetOrdinal("SageSyncStatus")),
                SageDocumentNumber = GetNullableString(reader, "SageDocumentNumber"),
                DigitalTicketPath = GetNullableString(reader, "DigitalTicketPath"),
                DigitalTicketGeneratedAt = GetNullableDateTime(reader, "DigitalTicketGeneratedAt"),
                PaperlessDocumentId = GetNullableString(reader, "PaperlessDocumentId"),
                PaperlessDocumentUrl = GetNullableString(reader, "PaperlessDocumentUrl"),
                PaperlessUploadedAt = GetNullableDateTime(reader, "PaperlessUploadedAt"),
                Notes = GetNullableString(reader, "Notes"),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                CompletedAt = GetNullableDateTime(reader, "CompletedAt")
            };
        }

        private static WeighingCycle MapCycle(SqlDataReader reader)
        {
            return new WeighingCycle
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                OperationId = reader.GetInt32(reader.GetOrdinal("OperationId")),
                CycleNumber = reader.GetInt32(reader.GetOrdinal("CycleNumber")),
                EntryWeight = GetNullableDecimal(reader, "EntryWeight"),
                EntryTime = GetNullableDateTime(reader, "EntryTime"),
                ExitWeight = GetNullableDecimal(reader, "ExitWeight"),
                ExitTime = GetNullableDateTime(reader, "ExitTime"),
                NetWeight = reader.GetDecimal(reader.GetOrdinal("NetWeight"))
            };
        }

        private static object ToDbValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
        }

        private static object ToDbValue(decimal? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static object ToDbValue(DateTime? value)
        {
            return value.HasValue ? value.Value : DBNull.Value;
        }

        private static string GetString(SqlDataReader reader, string columnName)
        {
            return GetNullableString(reader, columnName) ?? string.Empty;
        }

        private static string? GetNullableString(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        private static decimal? GetNullableDecimal(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
        }

        private static DateTime? GetNullableDateTime(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}
