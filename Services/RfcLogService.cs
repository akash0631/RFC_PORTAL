using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RFC_PORTAL.Models;

namespace RFC_PORTAL.Services
{
    public class RfcLogService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();

        private static readonly HashSet<string> AllowedLogSortColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "STARTED_DT", "RFC_CODE", "STATUS", "DURATION_SEC", "TOTAL_RECORDS_PUSHED", "TRIGGERED_BY", "ENVIRONMENT"
        };

        public PagedResult<Dictionary<string, object>> GetExecutionLogs(LogFilterRequest filter)
        {
            var conditions = new List<string>();
            var parameters = new Dictionary<string, object>();
            int pIdx = 1;

            if (!string.IsNullOrEmpty(filter.RfcCode))
            {
                conditions.Add($"l.RFC_CODE = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.RfcCode;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.Department))
            {
                conditions.Add($"m.DEPARTMENT = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.Department;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.Status))
            {
                conditions.Add($"l.STATUS = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.Status;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.Environment))
            {
                conditions.Add($"l.ENVIRONMENT = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.Environment;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.TriggeredBy))
            {
                conditions.Add($"l.TRIGGERED_BY ILIKE :p{pIdx}");
                parameters[$"p{pIdx}"] = $"%{filter.TriggeredBy}%";
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.DateFrom))
            {
                conditions.Add($"CAST(l.STARTED_DT AS DATE) >= :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.DateFrom;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.DateTo))
            {
                conditions.Add($"CAST(l.STARTED_DT AS DATE) <= :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.DateTo;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.RunId))
            {
                conditions.Add($"l.RUN_ID = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.RunId;
                pIdx++;
            }

            string whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";
            string sortCol = AllowedLogSortColumns.Contains(filter.SortBy ?? "") ? $"l.{filter.SortBy}" : "l.STARTED_DT";
            string sortDir = filter.SortDir?.ToUpper() == "ASC" ? "ASC" : "DESC";

            string baseQuery = $@"
                SELECT l.*, m.DISPLAY_NAME, m.DEPARTMENT
                FROM RFC_EXECUTION_LOG l
                LEFT JOIN RFC_MASTER m ON m.ID = l.RFC_ID
                {whereClause}
                ORDER BY {sortCol} {sortDir}";

            string countQuery = $@"
                SELECT COUNT(*)
                FROM RFC_EXECUTION_LOG l
                LEFT JOIN RFC_MASTER m ON m.ID = l.RFC_ID
                {whereClause}";

            return _sf.ExecutePagedQuery(baseQuery, countQuery, filter.Page, filter.PageSize, parameters);
        }

        public Dictionary<string, object> GetExecutionLogById(int id)
        {
            var sql = @"SELECT l.*, m.DISPLAY_NAME, m.DEPARTMENT
                        FROM RFC_EXECUTION_LOG l
                        LEFT JOIN RFC_MASTER m ON m.ID = l.RFC_ID
                        WHERE l.ID = :id";
            var results = _sf.QueryAsList(sql, new Dictionary<string, object> { { "id", id } });
            return results.FirstOrDefault();
        }

        public List<Dictionary<string, object>> GetExecutionDetails(int executionLogId)
        {
            var sql = @"SELECT * FROM RFC_EXECUTION_DETAIL
                        WHERE EXECUTION_LOG_ID = :logId
                        ORDER BY SORT_ORDER, RFC_START_DT";
            return _sf.QueryAsList(sql, new Dictionary<string, object> { { "logId", executionLogId } });
        }

        public PagedResult<Dictionary<string, object>> GetAuditLogs(AuditFilterRequest filter)
        {
            var conditions = new List<string>();
            var parameters = new Dictionary<string, object>();
            int pIdx = 1;

            if (!string.IsNullOrEmpty(filter.TableName))
            {
                conditions.Add($"a.TABLE_NAME = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.TableName;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.Action))
            {
                conditions.Add($"a.ACTION = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.Action;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.ChangedBy))
            {
                conditions.Add($"a.CHANGED_BY ILIKE :p{pIdx}");
                parameters[$"p{pIdx}"] = $"%{filter.ChangedBy}%";
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.DateFrom))
            {
                conditions.Add($"CAST(a.CHANGED_DT AS DATE) >= :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.DateFrom;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.DateTo))
            {
                conditions.Add($"CAST(a.CHANGED_DT AS DATE) <= :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.DateTo;
                pIdx++;
            }
            if (filter.RfcId.HasValue)
            {
                conditions.Add($"a.RFC_ID = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.RfcId.Value;
                pIdx++;
            }

            string whereClause = conditions.Any() ? "WHERE " + string.Join(" AND ", conditions) : "";

            string baseQuery = $@"SELECT a.*, m.DISPLAY_NAME AS RFC_NAME
                                  FROM RFC_CONFIG_AUDIT a
                                  LEFT JOIN RFC_MASTER m ON m.ID = a.RFC_ID
                                  {whereClause}
                                  ORDER BY a.CHANGED_DT DESC";

            string countQuery = $@"SELECT COUNT(*) FROM RFC_CONFIG_AUDIT a {whereClause}";

            return _sf.ExecutePagedQuery(baseQuery, countQuery, filter.Page, filter.PageSize, parameters);
        }

        public List<Dictionary<string, object>> GetExecutionLogsByRfcId(int rfcId, int limit = 20)
        {
            var sql = $@"SELECT * FROM RFC_EXECUTION_LOG
                         WHERE RFC_ID = :rfcId
                         ORDER BY STARTED_DT DESC
                         LIMIT {limit}";
            return _sf.QueryAsList(sql, new Dictionary<string, object> { { "rfcId", rfcId } });
        }
    }
}
