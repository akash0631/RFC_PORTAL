using System;

namespace RFC_PORTAL.Models
{
    public class RfcExportJobModel
    {
        public int Id { get; set; }
        public string SourceTable { get; set; }
        public string SourceSystem { get; set; }
        public string FilterJson { get; set; }
        public string Status { get; set; }
        public long TotalRows { get; set; }
        public long ExportedRows { get; set; }
        public string FilePath { get; set; }
        public decimal? FileSizeMb { get; set; }
        public DateTime? StartedDt { get; set; }
        public DateTime? CompletedDt { get; set; }
        public string ErrorMessage { get; set; }
        public string RequestedBy { get; set; }
        public DateTime? CreatedDt { get; set; }
        public DateTime? ExpiresDt { get; set; }
    }
}
