namespace RFC_PORTAL.Models
{
    public class RfcFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string SortBy { get; set; } = "RFC_CODE";
        public string SortDir { get; set; } = "ASC";
        public string Search { get; set; }
        public string Department { get; set; }
        public string SapModule { get; set; }
        public string Status { get; set; }
        public string ExecutionType { get; set; }
        public string ExecutionPattern { get; set; }
        public string Owner { get; set; }
    }

    public class LogFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string SortBy { get; set; } = "STARTED_DT";
        public string SortDir { get; set; } = "DESC";
        public string RfcCode { get; set; }
        public string Department { get; set; }
        public string Status { get; set; }
        public string Environment { get; set; }
        public string TriggeredBy { get; set; }
        public string DateFrom { get; set; }
        public string DateTo { get; set; }
        public string RunId { get; set; }
    }

    public class AuditFilterRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string TableName { get; set; }
        public string Action { get; set; }
        public string ChangedBy { get; set; }
        public string DateFrom { get; set; }
        public string DateTo { get; set; }
        public int? RfcId { get; set; }
    }

    public class DataExplorerRequest
    {
        public string TableName { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string SortBy { get; set; }
        public string SortDir { get; set; } = "ASC";
        public string FilterJson { get; set; }
    }

    public class ExportRequest
    {
        public string TableName { get; set; }
        public string FilterJson { get; set; }
        public string SortBy { get; set; }
        public string SortDir { get; set; }
        public string RequestedBy { get; set; }
    }

    public class ExecuteRfcRequest
    {
        public string Environment { get; set; } = "PROD";
        public string DateFrom { get; set; }
        public string DateTo { get; set; }
        public string StoreFilter { get; set; }
        public string WriteMode { get; set; }
        public string TriggeredBy { get; set; }
    }
}
