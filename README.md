# RCE Data Import

A C# console application that reads data from a CSV file and inserts it into PostgreSQL tables with multi-table relationships.

## Features

✅ **CSV File Reading** - Parses CSV files with user and order data  
✅ **Multi-Table Insert** - Automatically creates Users and Orders tables  
✅ **ID Retrieval** - Uses RETURNING clause to get inserted record IDs  
✅ **Foreign Key Relationships** - Orders reference Users by ID  
✅ **Duplicate Handling** - Skips duplicate users while allowing multiple orders per user  
✅ **Data Validation** - Graceful error handling for invalid records  
✅ **Data Display** - Shows all inserted data with formatted output

## Prerequisites

- .NET 6.0 or higher
- PostgreSQL 12 or higher
- NuGet packages: `Npgsql` and `CsvHelper`

## Setup

### 1. Clone the Repository
```bash
git clone https://github.com/sachchinannam404/RCEDataImport.git
cd RCEDataImport
```

### 2. Install Dependencies
```bash
dotnet restore
```

### 3. Configure Database Connection

Edit `Program.cs` and update the `ConnectionString`:

```csharp
private const string ConnectionString = "Host=localhost;Username=postgres;Password=YOUR_PASSWORD;Database=your_database";
```

Replace:
- `YOUR_PASSWORD` - Your PostgreSQL password
- `your_database` - Your database name

### 4. Create PostgreSQL Database (if needed)
```sql
CREATE DATABASE your_database;
```

## CSV File Format

The `data.csv` file should have the following columns:

```
Name,Email,Department,OrderNumber,Amount,OrderDate
John Doe,john.doe@example.com,Sales,ORD-001,150.50,2026-01-15
```

| Column | Type | Description |
|--------|------|-------------|
| Name | string | Customer full name |
| Email | string | Customer email (unique) |
| Department | string | Customer department |
| OrderNumber | string | Unique order number |
| Amount | decimal | Order amount |
| OrderDate | date | Order date (YYYY-MM-DD) |

## Database Schema

### Users Table
```sql
CREATE TABLE Users (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
    Department VARCHAR(100),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Orders Table
```sql
CREATE TABLE Orders (
    Id SERIAL PRIMARY KEY,
    UserId INT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    OrderNumber VARCHAR(50) NOT NULL,
    Amount DECIMAL(10, 2) NOT NULL,
    OrderDate DATE,
    Status VARCHAR(50) DEFAULT 'Pending',
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

## Usage

### Run the Application
```bash
dotnet run
```

### Expected Output
```
=== RCE Data Import - PostgreSQL Multi-Table Insert ===

Creating tables...
✓ Tables created successfully!

--- Reading CSV File ---
✓ Read 10 records from CSV

--- Inserting User Data ---
✓ User: John Doe (ID: 1)
✓ User: Jane Smith (ID: 2)
✓ User: Michael Johnson (ID: 3)
✓ User: Sarah Williams (ID: 4)
✓ User: David Brown (ID: 5)

✓ Inserted 5 users

--- Inserting Order Data ---
✓ Order: ORD-001 - $150.50 (ID: 1) for John Doe
✓ Order: ORD-002 - $299.99 (ID: 2) for John Doe
...

✓ Inserted 10 orders

--- Fetching Inserted Data ---

📋 ALL USERS:
────────────────────────────────────────────────────────────────────────────────
ID: 1     | Name: John Doe               | Email: john.doe@example.com    | Dept: Sales
...

📋 ALL ORDERS:
────────────────────────────────────────────────────────────────────────────────
Order ID: 1    | Number: ORD-001      | Amount: $150.50    | Date: 2026-01-15   | Status: Pending    | Customer: John Doe

✓ Data import completed successfully!
```

## How It Works

1. **Table Creation** - Drops and recreates Users and Orders tables
2. **CSV Parsing** - Reads data.csv and maps columns to CsvRecord objects
3. **User Insertion** - Inserts unique users and stores their IDs in a dictionary
4. **Order Insertion** - Inserts orders for each user, referencing their stored ID
5. **Data Retrieval** - Fetches and displays all inserted records with formatted output

## Error Handling

The application gracefully handles:
- Missing CSV files
- Duplicate email addresses (user level)
- Invalid data types
- Database connection errors
- Constraint violations

Failed records are logged and skipped while processing continues.

## Customization

### Add More Tables

1. Create a new table in `CreateTables()`
2. Add an insert method (e.g., `InsertProduct()`)
3. Call it from `ReadCsvAndInsertData()`
4. Add fields to `CsvRecord` class and CSV file

### Modify CSV Columns

Update `CsvRecord` class to match your CSV structure:
```csharp
public class CsvRecord
{
    public string Name { get; set; }
    public string Email { get; set; }
    // Add new properties
}
```

## Troubleshooting

**Connection Error**: Verify PostgreSQL is running and credentials are correct
```bash
psql -h localhost -U postgres -d your_database
```

**CSV Not Found**: Ensure `data.csv` is in the application directory

**Duplicate Email Error**: Check for duplicate emails in your CSV file

## License

MIT License

## Support

For issues or questions, open a GitHub issue in this repository.
