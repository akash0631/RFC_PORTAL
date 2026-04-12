using System;

namespace RFC_PORTAL.Models
{
    public class RfcExecutionLogModel
    {
        public int Id { get; set; }
        public string RunId { get; set; }
        public int RfcId { get; set; }
        public string RfcCode { get; set; }
        public string Environment { get; set; }
        public DateTime StartedDt { get; set; }
        public DateTime? CompletedDt { get; set; }
        public int? DurationSec { get; set; }
        public string Status { get; set; }
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public long TotalRecordsPulled { get; set; }
        public long TotalRecordsPushed { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorCategory { get; set; }
        public string TriggeredBy { get; set; }
        public DateTime? DateRangeFrom { get; set; }
        public DateTime? DateRangeTo { get; set; }
        public DateTime? CreatedDt { get; set; }

        // Joined fields
        public string DisplayName { get; set; }
        public string Department { get; set; }
    }

    public class RfcExecutionDetailModel
    {
        public int Id { get; set; }
        public int ExecutionLogId { get; set; }
        public string ItemKey { get; set; }
        public DateTime? RfcStartDt { get; set; }
        public DateTime? RfcEndDt { get; set; }
        public long RfcRecordsPulled { get; set; }
        public DateTime? SqlStartDt { get; set; }
        public DateTime? SqlEndDt { get; set; }
        public long SqlRecordsPushed { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public int SortOrder { get; set; }
    }
}
