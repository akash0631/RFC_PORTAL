using System;

namespace RFC_PORTAL.Models
{
    public class RfcMasterModel
    {
        public int Id { get; set; }
        public string RfcCode { get; set; }
        public string RfcFunctionName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Department { get; set; }
        public string SubDepartment { get; set; }
        public string SapModule { get; set; }
        public string SourceSystem { get; set; }
        public string TargetSystem { get; set; }
        public string TargetTable { get; set; }
        public string SapReturnTable { get; set; }
        public string ExecutionPattern { get; set; }
        public string LoopSourceQuery { get; set; }
        public string WriteMode { get; set; }
        public int BulkBatchSize { get; set; }
        public int TimeoutSeconds { get; set; }
        public int MaxRetry { get; set; }
        public int RetryDelaySec { get; set; }
        public string Status { get; set; }
        public string ExecutionType { get; set; }
        public string ScheduleCron { get; set; }
        public string ScheduleDesc { get; set; }
        public string Owner { get; set; }
        public int? SapConnectionId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreatedDt { get; set; }
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedDt { get; set; }
        public bool IsDeleted { get; set; }

        // Computed fields from joins
        public string LastRunStatus { get; set; }
        public DateTime? LastRunTime { get; set; }
        public int? LastRunDuration { get; set; }
        public long? LastRunRecords { get; set; }
    }
}
