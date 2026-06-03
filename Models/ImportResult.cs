namespace RCEDataImport.Models
{
    /// <summary>
    /// Result of the import operation.
    /// </summary>
    public class ImportResult
    {
        public int TotalRecordsRead { get; set; }
        public int UsersInserted { get; set; }
        public int OrdersInserted { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}
