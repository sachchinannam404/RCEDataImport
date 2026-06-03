namespace RCEDataImport.Models
{
    /// <summary>
    /// Represents a record from the CSV file.
    /// </summary>
    public class CsvRecord
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
        public string OrderNumber { get; set; }
        public decimal Amount { get; set; }
        public DateTime? OrderDate { get; set; }
    }
}
