using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;
using Npgsql;
using RCEDataImport.Models;
using System.Globalization;

namespace RCEDataImport.Core
{
    /// <summary>
    /// Generic data importer that handles CSV reading and multi-table PostgreSQL insertion.
    /// </summary>
    public class DataImporter
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly DatabaseManager _dbManager;

        public DataImporter(string connectionString, ILogger logger = null)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? new ConsoleLogger();
            _dbManager = new DatabaseManager(connectionString, _logger);
        }

        /// <summary>
        /// Imports data from CSV file to PostgreSQL tables.
        /// </summary>
        public async Task<ImportResult> ImportAsync(string csvFilePath, bool recreateTables = true)
        {
            var result = new ImportResult();

            // Validate CSV file
            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException($"CSV file not found: {csvFilePath}");

            _logger.Info($"Reading CSV from: {csvFilePath}");

            // Read CSV records
            var records = ReadCsvRecords(csvFilePath);
            result.TotalRecordsRead = records.Count;
            _logger.Info($"✓ Read {records.Count} records from CSV\n");

            // Create tables if needed
            if (recreateTables)
                await _dbManager.CreateTablesAsync();

            // Process data
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Insert users and track their IDs
                var userIdMap = await InsertUsersAsync(connection, records, result);

                // Insert orders for each user
                await InsertOrdersAsync(connection, records, userIdMap, result);

                // Fetch and display inserted data
                await _dbManager.DisplayAllDataAsync(connection);

                connection.Close();
            }

            return result;
        }

        private List<CsvRecord> ReadCsvRecords(string filePath)
        {
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                return csv.GetRecords<CsvRecord>().ToList();
            }
        }

        private async Task<Dictionary<string, int>> InsertUsersAsync(
            NpgsqlConnection connection, 
            List<CsvRecord> records, 
            ImportResult result)
        {
            _logger.Info("--- Inserting User Data ---");
            var userIdMap = new Dictionary<string, int>();

            // Get unique users
            var uniqueUsers = records
                .GroupBy(r => r.Email)
                .Select(g => g.First())
                .ToList();

            foreach (var user in uniqueUsers)
            {
                try
                {
                    var userId = await _dbManager.InsertGenericAsync(
                        connection,
                        "Users",
                        new Dictionary<string, object>
                        {
                            { "Name", user.Name },
                            { "Email", user.Email },
                            { "Department", user.Department }
                        }
                    );

                    userIdMap[user.Email] = userId;
                    result.UsersInserted++;
                    _logger.Info($"✓ User: {user.Name} (ID: {userId})");
                }
                catch (Exception ex)
                {
                    var error = $"Failed to insert user {user.Email}: {ex.Message}";
                    result.Errors.Add(error);
                    _logger.Warn($"⚠ {error}");
                }
            }

            _logger.Info($"\n✓ Inserted {result.UsersInserted} users\n");
            return userIdMap;
        }

        private async Task InsertOrdersAsync(
            NpgsqlConnection connection, 
            List<CsvRecord> records, 
            Dictionary<string, int> userIdMap, 
            ImportResult result)
        {
            _logger.Info("--- Inserting Order Data ---");

            foreach (var record in records)
            {
                try
                {
                    if (!userIdMap.ContainsKey(record.Email))
                    {
                        result.Errors.Add($"User email not found: {record.Email}");
                        continue;
                    }

                    var orderId = await _dbManager.InsertGenericAsync(
                        connection,
                        "Orders",
                        new Dictionary<string, object>
                        {
                            { "UserId", userIdMap[record.Email] },
                            { "OrderNumber", record.OrderNumber },
                            { "Amount", record.Amount },
                            { "OrderDate", record.OrderDate }
                        }
                    );

                    result.OrdersInserted++;
                    _logger.Info($"✓ Order: {record.OrderNumber} - ${record.Amount} (ID: {orderId}) for {record.Name}");
                }
                catch (Exception ex)
                {
                    var error = $"Failed to insert order {record.OrderNumber}: {ex.Message}";
                    result.Errors.Add(error);
                    _logger.Warn($"⚠ {error}");
                }
            }

            _logger.Info($"\n✓ Inserted {result.OrdersInserted} orders\n");
        }
    }
}
