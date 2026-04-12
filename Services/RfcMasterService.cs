using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using RFC_PORTAL.Models;

namespace RFC_PORTAL.Services
{
    public class RfcMasterService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();

        private static readonly HashSet<string> AllowedSortColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ID", "RFC_CODE", "DISPLAY_NAME", "DEPARTMENT", "SAP_MODULE", "STATUS",
            "EXECUTION_TYPE", "EXECUTION_PATTERN", "OWNER", "TARGET_TABLE", "CREATED_DT", "UPDATED_DT"
        };

        public PagedResult<Dictionary<string, object>> GetPaged(RfcFilterRequest filter)
        {
            var conditions = new List<string> { "m.IS_DELETED = FALSE" };
            var parameters = new Dictionary<string, object>();
            int pIdx = 1;

            if (!string.IsNullOrEmpty(filter.Search))
            {
                conditions.Add($"(m.RFC_CODE ILIKE :p{pIdx} OR m.DISPLAY_NAME ILIKE :p{pIdx} OR m.DESCRIPTION ILIKE :p{pIdx})");
                parameters[$"p{pIdx}"] = $"%{filter.Search}%";
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.Department))
            {
                conditions.Add($"m.DEPARTMENT = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.Department;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.SapModule))
            {
                conditions.Add($"m.SAP_MODULE = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.SapModule;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.Status))
            {
                conditions.Add($"m.STATUS = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.Status;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.ExecutionType))
            {
                conditions.Add($"m.EXECUTION_TYPE = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.ExecutionType;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.ExecutionPattern))
            {
                conditions.Add($"m.EXECUTION_PATTERN = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.ExecutionPattern;
                pIdx++;
            }
            if (!string.IsNullOrEmpty(filter.Owner))
            {
                conditions.Add($"m.OWNER = :p{pIdx}");
                parameters[$"p{pIdx}"] = filter.Owner;
                pIdx++;
            }

            string whereClause = string.Join(" AND ", conditions);
            string sortCol = AllowedSortColumns.Contains(filter.SortBy ?? "") ? filter.SortBy : "RFC_CODE";
            string sortDir = filter.SortDir?.ToUpper() == "DESC" ? "DESC" : "ASC";

            string baseQuery = $@"
                SELECT m.*,
                       l.STATUS AS LAST_RUN_STATUS,
                       l.STARTED_DT AS LAST_RUN_TIME,
                       l.DURATION_SEC AS LAST_RUN_DURATION,
                       l.TOTAL_RECORDS_PUSHED AS LAST_RUN_RECORDS
                FROM RFC_MASTER m
                LEFT JOIN (
                    SELECT RFC_ID, STATUS, STARTED_DT, DURATION_SEC, TOTAL_RECORDS_PUSHED,
                           ROW_NUMBER() OVER (PARTITION BY RFC_ID ORDER BY STARTED_DT DESC) AS rn
                    FROM RFC_EXECUTION_LOG
                ) l ON l.RFC_ID = m.ID AND l.rn = 1
                WHERE {whereClause}
                ORDER BY m.{sortCol} {sortDir}";

            string countQuery = $"SELECT COUNT(*) FROM RFC_MASTER m WHERE {whereClause}";

            return _sf.ExecutePagedQuery(baseQuery, countQuery, filter.Page, filter.PageSize, parameters);
        }

        public Dictionary<string, object> GetById(int id)
        {
            var sql = @"SELECT m.*,
                               l.STATUS AS LAST_RUN_STATUS,
                               l.STARTED_DT AS LAST_RUN_TIME,
                               l.DURATION_SEC AS LAST_RUN_DURATION,
                               l.TOTAL_RECORDS_PUSHED AS LAST_RUN_RECORDS
                        FROM RFC_MASTER m
                        LEFT JOIN (
                            SELECT RFC_ID, STATUS, STARTED_DT, DURATION_SEC, TOTAL_RECORDS_PUSHED,
                                   ROW_NUMBER() OVER (PARTITION BY RFC_ID ORDER BY STARTED_DT DESC) AS rn
                            FROM RFC_EXECUTION_LOG
                        ) l ON l.RFC_ID = m.ID AND l.rn = 1
                        WHERE m.ID = :id AND m.IS_DELETED = FALSE";
            var results = _sf.QueryAsList(sql, new Dictionary<string, object> { { "id", id } });
            return results.FirstOrDefault();
        }

        public int Create(RfcMasterModel model)
        {
            var sql = @"INSERT INTO RFC_MASTER (RFC_CODE, RFC_FUNCTION_NAME, DISPLAY_NAME, DESCRIPTION,
                        DEPARTMENT, SUB_DEPARTMENT, SAP_MODULE, SOURCE_SYSTEM, TARGET_SYSTEM, TARGET_TABLE,
                        SAP_RETURN_TABLE, EXECUTION_PATTERN, LOOP_SOURCE_QUERY, WRITE_MODE,
                        BULK_BATCH_SIZE, TIMEOUT_SECONDS, MAX_RETRY, RETRY_DELAY_SEC,
                        STATUS, EXECUTION_TYPE, SCHEDULE_CRON, SCHEDULE_DESC, OWNER,
                        SAP_CONNECTION_ID, CREATED_BY, UPDATED_BY)
                        VALUES (:p1,:p2,:p3,:p4,:p5,:p6,:p7,:p8,:p9,:p10,:p11,:p12,:p13,:p14,
                                :p15,:p16,:p17,:p18,:p19,:p20,:p21,:p22,:p23,:p24,:p25,:p25)";
            var parameters = new Dictionary<string, object>
            {
                {"p1", model.RfcCode}, {"p2", model.RfcFunctionName}, {"p3", model.DisplayName},
                {"p4", model.Description}, {"p5", model.Department}, {"p6", model.SubDepartment},
                {"p7", model.SapModule}, {"p8", model.SourceSystem ?? "SAP"}, {"p9", model.TargetSystem ?? "Snowflake"},
                {"p10", model.TargetTable}, {"p11", model.SapReturnTable}, {"p12", model.ExecutionPattern},
                {"p13", model.LoopSourceQuery}, {"p14", model.WriteMode ?? "Append"},
                {"p15", model.BulkBatchSize > 0 ? model.BulkBatchSize : 100000},
                {"p16", model.TimeoutSeconds > 0 ? model.TimeoutSeconds : 3600},
                {"p17", model.MaxRetry}, {"p18", model.RetryDelaySec > 0 ? model.RetryDelaySec : 30},
                {"p19", model.Status ?? "Active"}, {"p20", model.ExecutionType ?? "Scheduled"},
                {"p21", model.ScheduleCron}, {"p22", model.ScheduleDesc}, {"p23", model.Owner},
                {"p24", model.SapConnectionId}, {"p25", model.CreatedBy}
            };
            return _sf.ExecuteNonQuery(sql, parameters);
        }

        public int Update(int id, RfcMasterModel model)
        {
            var sql = @"UPDATE RFC_MASTER SET
                        RFC_CODE=:p1, RFC_FUNCTION_NAME=:p2, DISPLAY_NAME=:p3, DESCRIPTION=:p4,
                        DEPARTMENT=:p5, SUB_DEPARTMENT=:p6, SAP_MODULE=:p7,
                        TARGET_TABLE=:p8, SAP_RETURN_TABLE=:p9, EXECUTION_PATTERN=:p10,
                        LOOP_SOURCE_QUERY=:p11, WRITE_MODE=:p12, BULK_BATCH_SIZE=:p13,
                        TIMEOUT_SECONDS=:p14, MAX_RETRY=:p15, RETRY_DELAY_SEC=:p16,
                        STATUS=:p17, EXECUTION_TYPE=:p18, SCHEDULE_CRON=:p19, SCHEDULE_DESC=:p20,
                        OWNER=:p21, SAP_CONNECTION_ID=:p22, UPDATED_BY=:p23, UPDATED_DT=CURRENT_TIMESTAMP()
                        WHERE ID=:id";
            var parameters = new Dictionary<string, object>
            {
                {"p1", model.RfcCode}, {"p2", model.RfcFunctionName}, {"p3", model.DisplayName},
                {"p4", model.Description}, {"p5", model.Department}, {"p6", model.SubDepartment},
                {"p7", model.SapModule}, {"p8", model.TargetTable}, {"p9", model.SapReturnTable},
                {"p10", model.ExecutionPattern}, {"p11", model.LoopSourceQuery}, {"p12", model.WriteMode},
                {"p13", model.BulkBatchSize}, {"p14", model.TimeoutSeconds},
                {"p15", model.MaxRetry}, {"p16", model.RetryDelaySec},
                {"p17", model.Status}, {"p18", model.ExecutionType},
                {"p19", model.ScheduleCron}, {"p20", model.ScheduleDesc},
                {"p21", model.Owner}, {"p22", model.SapConnectionId},
                {"p23", model.UpdatedBy}, {"id", id}
            };
            return _sf.ExecuteNonQuery(sql, parameters);
        }

        public int UpdateStatus(int id, string status, string updatedBy)
        {
            var sql = "UPDATE RFC_MASTER SET STATUS=:status, UPDATED_BY=:user, UPDATED_DT=CURRENT_TIMESTAMP() WHERE ID=:id";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                {"status", status}, {"user", updatedBy}, {"id", id}
            });
        }

        public int SoftDelete(int id, string deletedBy)
        {
            var sql = "UPDATE RFC_MASTER SET IS_DELETED=TRUE, UPDATED_BY=:user, UPDATED_DT=CURRENT_TIMESTAMP() WHERE ID=:id";
            return _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                {"user", deletedBy}, {"id", id}
            });
        }

        public List<string> GetDistinctDepartments()
        {
            var dt = _sf.ExecuteQuery("SELECT DISTINCT DEPARTMENT FROM RFC_MASTER WHERE IS_DELETED=FALSE AND DEPARTMENT IS NOT NULL ORDER BY DEPARTMENT");
            return dt.AsEnumerable().Select(r => r.Field<string>("DEPARTMENT")).ToList();
        }

        public List<string> GetDistinctModules()
        {
            var dt = _sf.ExecuteQuery("SELECT DISTINCT SAP_MODULE FROM RFC_MASTER WHERE IS_DELETED=FALSE AND SAP_MODULE IS NOT NULL ORDER BY SAP_MODULE");
            return dt.AsEnumerable().Select(r => r.Field<string>("SAP_MODULE")).ToList();
        }

        public List<string> GetDistinctOwners()
        {
            var dt = _sf.ExecuteQuery("SELECT DISTINCT OWNER FROM RFC_MASTER WHERE IS_DELETED=FALSE AND OWNER IS NOT NULL ORDER BY OWNER");
            return dt.AsEnumerable().Select(r => r.Field<string>("OWNER")).ToList();
        }
    }
}
