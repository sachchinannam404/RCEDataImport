using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper;
using Npgsql;
using System.Globalization;

class Program
{
    private const string ConnectionString = "Host=localhost;Username=postgres;Password=your_password;Database=your_database";
    private const string CsvFilePath = "data.csv";

    static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("=== RCE Data Import - PostgreSQL Multi-Table Insert ===\n");

            // Validate CSV file exists
            if (!File.Exists(CsvFilePath))
            {
                Console.WriteLine($"✗ Error: CSV file not found at {CsvFilePath}");
                return;
            }

            // Create tables
            CreateTables();

            // Read CSV and insert data
            ReadCsvAndInsertData();

            Console.WriteLine("\n✓ Data import completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }

    static void CreateTables()
    {
        Console.WriteLine("Creating tables...");

        using (var connection = new NpgsqlConnection(ConnectionString))
        {
            connection.Open();

            // Drop existing tables
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    DROP TABLE IF EXISTS Orders CASCADE;
                    DROP TABLE IF EXISTS Users CASCADE;";

                cmd.ExecuteNonQuery();
            }

            // Create Users table
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE Users (
                        Id SERIAL PRIMARY KEY,
                        Name VARCHAR(100) NOT NULL,
                        Email VARCHAR(100) NOT NULL UNIQUE,
                        Department VARCHAR(100),
                        CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );";

                cmd.ExecuteNonQuery();
            }

            // Create Orders table
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE Orders (
                        Id SERIAL PRIMARY KEY,
                        UserId INT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
                        OrderNumber VARCHAR(50) NOT NULL,
                        Amount DECIMAL(10, 2) NOT NULL,
                        OrderDate DATE,
                        Status VARCHAR(50) DEFAULT 'Pending',
                        CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                    );";

                cmd.ExecuteNonQuery();
            }

            connection.Close();
        }

        Console.WriteLine("✓ Tables created successfully!\n");
    }

    static void ReadCsvAndInsertData()
    {
        var userData = new Dictionary<string, int>(); // Email -> UserId mapping

        Console.WriteLine("--- Reading CSV File ---");

        using (var reader = new StreamReader(CsvFilePath))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            var records = csv.GetRecords<CsvRecord>().ToList();
            Console.WriteLine($"✓ Read {records.Count} records from CSV\n");

            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                connection.Open();

                // Group records by user (to avoid duplicate user inserts)
                var groupedByUser = records
                    .GroupBy(r => r.Email)
                    .ToDictionary(g => g.Key, g => g.First());

                Console.WriteLine("--- Inserting User Data ---");
                int userCount = 0;

                // Insert users first
                foreach (var userRecord in groupedByUser.Values)
                {
                    try
                    {
                        int userId = InsertUser(connection, userRecord.Name, userRecord.Email, userRecord.Department);
                        userData[userRecord.Email] = userId;
                        Console.WriteLine($"✓ User: {userRecord.Name} (ID: {userId})");
                        userCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠ Skipped user {userRecord.Email}: {ex.Message}");
                    }
                }

                Console.WriteLine($"\n✓ Inserted {userCount} users\n");

                // Insert orders
                Console.WriteLine("--- Inserting Order Data ---");
                int orderCount = 0;

                foreach (var record in records)
                {
                    try
                    {
                        if (userData.ContainsKey(record.Email))
                        {
                            int userId = userData[record.Email];
                            int orderId = InsertOrder(connection, userId, record.OrderNumber, record.Amount, record.OrderDate);
                            Console.WriteLine($"✓ Order: {record.OrderNumber} - ${record.Amount} (ID: {orderId}) for {record.Name}");
                            orderCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠ Skipped order {record.OrderNumber}: {ex.Message}");
                    }
                }

                Console.WriteLine($"\n✓ Inserted {orderCount} orders\n");

                // Fetch and display all data
                Console.WriteLine("--- Fetching Inserted Data ---");
                FetchAllData(connection);

                connection.Close();
            }
        }
    }

    static int InsertUser(NpgsqlConnection connection, string name, string email, string department)
    {
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Users (Name, Email, Department) 
                VALUES (@name, @email, @department) 
                RETURNING Id;";

            cmd.Parameters.AddWithValue("@name", name ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@email", email ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@department", department ?? (object)DBNull.Value);

            return (int)cmd.ExecuteScalar();
        }
    }

    static int InsertOrder(NpgsqlConnection connection, int userId, string orderNumber, decimal amount, DateTime? orderDate)
    {
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Orders (UserId, OrderNumber, Amount, OrderDate) 
                VALUES (@userId, @orderNumber, @amount, @orderDate) 
                RETURNING Id;";

            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@orderNumber", orderNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@amount", amount);
            cmd.Parameters.AddWithValue("@orderDate", orderDate ?? (object)DBNull.Value);

            return (int)cmd.ExecuteScalar();
        }
    }

    static void FetchAllData(NpgsqlConnection connection)
    {
        // Fetch Users
        Console.WriteLine("\n📋 ALL USERS:");
        Console.WriteLine(new string('-', 80));

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, Name, Email, Department, CreatedAt FROM Users ORDER BY Id;";

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    Console.WriteLine($"ID: {reader["Id"],-5} | Name: {reader["Name"],-20} | Email: {reader["Email"],-30} | Dept: {reader["Department"]}");
                }
            }
        }

        // Fetch Orders with User JOIN
        Console.WriteLine("\n📋 ALL ORDERS:");
        Console.WriteLine(new string('-', 120));

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    o.Id,
                    o.OrderNumber,
                    o.Amount,
                    o.OrderDate,
                    o.Status,
                    u.Name,
                    u.Email
                FROM Orders o
                JOIN Users u ON o.UserId = u.Id
                ORDER BY o.Id;";

            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var orderDate = reader["OrderDate"] != DBNull.Value 
                        ? ((DateTime)reader["OrderDate"]).ToString("yyyy-MM-dd")
                        : "N/A";

                    Console.WriteLine($"Order ID: {reader["Id"],-4} | Number: {reader["OrderNumber"],-12} | Amount: ${reader["Amount"],-10} | Date: {orderDate,-12} | Status: {reader["Status"],-10} | Customer: {reader["Name"]}");
                }
            }
        }

        Console.WriteLine();
    }
}

// CSV Record Model
public class CsvRecord
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Department { get; set; }
    public string OrderNumber { get; set; }
    public decimal Amount { get; set; }
    public DateTime? OrderDate { get; set; }
}
