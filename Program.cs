using System;
using System.Threading.Tasks;
using RCEDataImport.Core;
using RCEDataImport.Models;
using Microsoft.Extensions.Configuration;

namespace RCEDataImport
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== RCE Data Import - PostgreSQL Multi-Table Insert ===\n");

                // Load configuration
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false)
                    .Build();

                var connectionString = config["Database:ConnectionString"];
                var csvFilePath = config["Import:CsvFilePath"];
                var recreateTables = bool.Parse(config["Import:RecreateTables"] ?? "true");

                // Initialize importer
                var importer = new DataImporter(connectionString);
                var result = await importer.ImportAsync(csvFilePath, recreateTables);

                // Display results
                DisplayResults(result);
                Console.WriteLine("\n✓ Data import completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error: {ex.Message}");
                Console.WriteLine($"Details: {ex.InnerException?.Message}");
            }
        }

        static void DisplayResults(ImportResult result)
        {
            Console.WriteLine($"\n--- Import Summary ---");
            Console.WriteLine($"Total Records Read: {result.TotalRecordsRead}");
            Console.WriteLine($"Users Inserted: {result.UsersInserted}");
            Console.WriteLine($"Orders Inserted: {result.OrdersInserted}");
            Console.WriteLine($"Errors: {result.Errors.Count}");

            if (result.Errors.Count > 0)
            {
                Console.WriteLine("\n--- Errors ---");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"⚠ {error}");
                }
            }
        }
    }
}
