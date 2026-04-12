using System.Collections.Generic;
using System.Data;
using System.Linq;
using RFC_PORTAL.Models;

namespace RFC_PORTAL.Services
{
    public class DashboardService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();

        public DashboardSummary GetSummary()
        {
            var masterSql = @"SELECT
                COUNT(*) AS TOTAL_RFCS,
                SUM(CASE WHEN STATUS='Active' THEN 1 ELSE 0 END) AS ACTIVE_RFCS,
                SUM(CASE WHEN STATUS='Inactive' THEN 1 ELSE 0 END) AS INACTIVE_RFCS
                FROM RFC_MASTER WHERE IS_DELETED = FALSE";
            var masterDt = _sf.ExecuteQuery(masterSql);

            var todaySql = @"SELECT
                COUNT(*) AS TOTAL_RUNS,
                SUM(CASE WHEN STATUS='Success' THEN 1 ELSE 0 END) AS SUCCESS_COUNT,
                SUM(CASE WHEN STATUS='Failed' THEN 1 ELSE 0 END) AS FAILED_COUNT,
                SUM(CASE WHEN STATUS='Running' THEN 1 ELSE 0 END) AS RUNNING_COUNT,
                COALESCE(AVG(DURATION_SEC),0) AS AVG_DURATION,
                COALESCE(SUM(TOTAL_RECORDS_PUSHED),0) AS TOTAL_RECORDS
                FROM RFC_EXECUTION_LOG
                WHERE CAST(STARTED_DT AS DATE) = CURRENT_DATE";
            var todayDt = _sf.ExecuteQuery(todaySql);

            var exportSql = "SELECT COUNT(*) AS CNT FROM RFC_EXPORT_JOB WHERE STATUS IN ('Queued','Processing')";
            var exportDt = _sf.ExecuteQuery(exportSql);

            var mr = masterDt.Rows[0];
            var tr = todayDt.Rows[0];

            return new DashboardSummary
            {
                TotalRfcs = GetLong(mr, "TOTAL_RFCS"),
                ActiveRfcs = GetLong(mr, "ACTIVE_RFCS"),
                InactiveRfcs = GetLong(mr, "INACTIVE_RFCS"),
                TodayRuns = GetLong(tr, "TOTAL_RUNS"),
                TodaySuccess = GetLong(tr, "SUCCESS_COUNT"),
                TodayFailed = GetLong(tr, "FAILED_COUNT"),
                TodayRunning = GetLong(tr, "RUNNING_COUNT"),
                AvgDurationSec = GetDouble(tr, "AVG_DURATION"),
                TotalRecordsToday = GetLong(tr, "TOTAL_RECORDS"),
                PendingExports = GetLong(exportDt.Rows[0], "CNT")
            };
        }

        public List<DeptDistribution> GetDeptDistribution()
        {
            var sql = @"SELECT DEPARTMENT, COUNT(*) AS COUNT
                        FROM RFC_MASTER WHERE IS_DELETED=FALSE
                        GROUP BY DEPARTMENT ORDER BY COUNT DESC";
            var dt = _sf.ExecuteQuery(sql);
            return dt.AsEnumerable().Select(r => new DeptDistribution
            {
                Department = r.Field<string>("DEPARTMENT") ?? "Unknown",
                Count = (int)GetLong(r, "COUNT")
            }).ToList();
        }

        public List<DailyTrend> GetDailyTrend(int days = 30)
        {
            var sql = $@"SELECT CAST(STARTED_DT AS DATE) AS RUN_DATE,
                         SUM(CASE WHEN STATUS='Success' THEN 1 ELSE 0 END) AS SUCCESS_COUNT,
                         SUM(CASE WHEN STATUS='Failed' THEN 1 ELSE 0 END) AS FAILED_COUNT,
                         COUNT(*) AS TOTAL_COUNT
                         FROM RFC_EXECUTION_LOG
                         WHERE STARTED_DT >= DATEADD(day, -{days}, CURRENT_DATE)
                         GROUP BY CAST(STARTED_DT AS DATE)
                         ORDER BY RUN_DATE";
            var dt = _sf.ExecuteQuery(sql);
            return dt.AsEnumerable().Select(r => new DailyTrend
            {
                Date = r["RUN_DATE"]?.ToString(),
                SuccessCount = (int)GetLong(r, "SUCCESS_COUNT"),
                FailedCount = (int)GetLong(r, "FAILED_COUNT"),
                TotalCount = (int)GetLong(r, "TOTAL_COUNT")
            }).ToList();
        }

        public List<TopError> GetTopErrors(int limit = 10)
        {
            var sql = $@"SELECT ERROR_CATEGORY, COUNT(*) AS COUNT
                         FROM RFC_EXECUTION_LOG
                         WHERE ERROR_CATEGORY IS NOT NULL AND STATUS='Failed'
                         GROUP BY ERROR_CATEGORY
                         ORDER BY COUNT DESC
                         LIMIT {limit}";
            var dt = _sf.ExecuteQuery(sql);
            return dt.AsEnumerable().Select(r => new TopError
            {
                ErrorCategory = r.Field<string>("ERROR_CATEGORY"),
                Count = (int)GetLong(r, "COUNT")
            }).ToList();
        }

        public List<RecentExecution> GetRecentExecutions(int limit = 15)
        {
            var sql = $@"SELECT l.RUN_ID, l.RFC_CODE, m.DISPLAY_NAME, l.STATUS,
                         l.DURATION_SEC, l.TOTAL_RECORDS_PUSHED, l.TRIGGERED_BY, l.STARTED_DT
                         FROM RFC_EXECUTION_LOG l
                         LEFT JOIN RFC_MASTER m ON m.ID = l.RFC_ID
                         ORDER BY l.STARTED_DT DESC
                         LIMIT {limit}";
            var dt = _sf.ExecuteQuery(sql);
            return dt.AsEnumerable().Select(r => new RecentExecution
            {
                RunId = r.Field<string>("RUN_ID"),
                RfcCode = r.Field<string>("RFC_CODE"),
                DisplayName = r.Field<string>("DISPLAY_NAME"),
                Status = r.Field<string>("STATUS"),
                DurationSec = r["DURATION_SEC"] == System.DBNull.Value ? (int?)null : (int)GetLong(r, "DURATION_SEC"),
                TotalRecordsPushed = GetLong(r, "TOTAL_RECORDS_PUSHED"),
                TriggeredBy = r.Field<string>("TRIGGERED_BY"),
                StartedDt = r.Field<System.DateTime>("STARTED_DT")
            }).ToList();
        }

        private static long GetLong(DataRow row, string col)
        {
            var val = row[col];
            if (val == null || val == System.DBNull.Value) return 0;
            return System.Convert.ToInt64(val);
        }

        private static double GetDouble(DataRow row, string col)
        {
            var val = row[col];
            if (val == null || val == System.DBNull.Value) return 0;
            return System.Convert.ToDouble(val);
        }
    }
}
