using System;
using System.Collections.Generic;

namespace RFC_PORTAL.Models
{
    public class DashboardSummary
    {
        public long TotalRfcs { get; set; }
        public long ActiveRfcs { get; set; }
        public long InactiveRfcs { get; set; }
        public long TodayRuns { get; set; }
        public long TodaySuccess { get; set; }
        public long TodayFailed { get; set; }
        public long TodayRunning { get; set; }
        public double AvgDurationSec { get; set; }
        public long TotalRecordsToday { get; set; }
        public long PendingExports { get; set; }
    }

    public class DeptDistribution
    {
        public string Department { get; set; }
        public int Count { get; set; }
    }

    public class DailyTrend
    {
        public string Date { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int TotalCount { get; set; }
    }

    public class TopError
    {
        public string ErrorCategory { get; set; }
        public int Count { get; set; }
    }

    public class RecentExecution
    {
        public string RunId { get; set; }
        public string RfcCode { get; set; }
        public string DisplayName { get; set; }
        public string Status { get; set; }
        public int? DurationSec { get; set; }
        public long TotalRecordsPushed { get; set; }
        public string TriggeredBy { get; set; }
        public DateTime StartedDt { get; set; }
    }
}
