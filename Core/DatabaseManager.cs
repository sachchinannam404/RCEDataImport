using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;

namespace RCEDataImport.Core
{
    /// <summary>
    /// Manages database operations in a generic way.
    /// Note: CreateTablesAsync has been made a no-op; table creation should be managed externally.
    /// </summary>
    public class DatabaseManager
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public DatabaseManager(string connectionString, ILogger logger = null)
        {
            _connectionString = connectionString;
            _logger = logger ?? new ConsoleLogger();
        }

        /// <summary>
        /// Previously created and dropped tables. To support environments
        /// where schema is managed externally, this method is now a no-op.
        /// </summary>
        public async Task CreateTablesAsync()
        {
            _logger.Info("Skipping table creation: schema must be managed outside the importer.");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Generic insert method that works with any table and column mappings.
        /// </summary>
        public async Task<int> InsertGenericAsync(
            NpgsqlConnection connection,
            string tableName,
            Dictionary<string, object> columnValues)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentException(nameof(tableName));

            if (columnValues == null || columnValues.Count == 0)
                throw new ArgumentException(nameof(columnValues));

            var columns = string.Join(", ", columnValues.Keys);
            var parameters = string.Join(", ", columnValues.Keys.Select(k => $"@{k}"));

            var sql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters}) RETURNING Id;";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;

                foreach (var kvp in columnValues)
                {
                    cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
                }

                var result = await cmd.ExecuteScalarAsync();
                return result != null ? (int)result : -1;
            }
        }

        /// <summary>
        /// Fetches and displays all data from tables.
        /// </summary>
        public async Task DisplayAllDataAsync(NpgsqlConnection connection)
        {
            _logger.Info("--- Fetching Inserted Data ---\n");

            // Display Users
            await DisplayTableDataAsync(
                connection,
                "📋 ALL USERS",
                "SELECT Id, Name, Email, Department, CreatedAt FROM Users ORDER BY Id;",
                new[] { "Id", "Name", "Email", "Department" },
                new[] { 5, 20, 30, 20 });

            // Display Orders with JOIN
            await DisplayTableDataAsync(
                connection,
                "📋 ALL ORDERS",
                @"SELECT o.Id, o.OrderNumber, o.Amount, o.OrderDate, o.Status, u.Name
                  FROM Orders o JOIN Users u ON o.UserId = u.Id ORDER BY o.Id;",
                new[] { "Id", "OrderNumber", "Amount", "OrderDate", "Status", "Name" },
                new[] { 6, 15, 12, 12, 12, 20 });
        }

        private async Task DisplayTableDataAsync(
            NpgsqlConnection connection,
            string title,
            string query,
            string[] columns,
            int[] columnWidths)
        {
            Console.WriteLine(title);
            Console.WriteLine(new string('-', columnWidths.Sum()));

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = query;
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var row = "";
                        for (int i = 0; i < columns.Length; i++)
                        {
                            var value = reader[columns[i]] != DBNull.Value
                                ? reader[columns[i]].ToString()
                                : "N/A";

                            row += value.PadRight(columnWidths[i]);
                        }
                        Console.WriteLine(row);
                    }
                }
            }

            Console.WriteLine();
        }

        private async Task ExecuteCommandAsync(NpgsqlConnection connection, string commandText)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = commandText;
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
