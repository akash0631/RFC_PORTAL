using System.Collections.Generic;
using System.Data;
using System.Linq;
using RFC_PORTAL.Models;

namespace RFC_PORTAL.Services
{
    public class DataExplorerService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();

        public List<Dictionary<string, object>> GetAllTables()
        {
            var sql = @"SELECT TABLE_NAME, ROW_COUNT, BYTES, CREATED, LAST_ALTERED
                        FROM INFORMATION_SCHEMA.TABLES
                        WHERE TABLE_SCHEMA = 'GOLD' AND TABLE_TYPE = 'BASE TABLE'
                        ORDER BY TABLE_NAME";
            return _sf.QueryAsList(sql);
        }

        public List<Dictionary<string, object>> GetTableColumns(string tableName)
        {
            var safeName = SnowflakeService.SanitizeIdentifier(tableName);
            var sql = @"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE,
                               CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE,
                               COLUMN_DEFAULT, ORDINAL_POSITION
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = 'GOLD' AND TABLE_NAME = :tbl
                        ORDER BY ORDINAL_POSITION";
            return _sf.QueryAsList(sql, new Dictionary<string, object> { { "tbl", safeName } });
        }

        public PagedResult<Dictionary<string, object>> QueryTable(DataExplorerRequest request)
        {
            var safeName = SnowflakeService.SanitizeIdentifier(request.TableName);
            Dictionary<string, object> parameters;
            var baseQuery = _sf.BuildFilteredQuery(safeName, request.FilterJson, request.SortBy, request.SortDir, out parameters);

            var countQuery = baseQuery;
            int selectIdx = countQuery.IndexOf("SELECT *");
            if (selectIdx >= 0)
            {
                countQuery = countQuery.Substring(0, selectIdx) + "SELECT COUNT(*)" + countQuery.Substring(selectIdx + 8);
            }

            int orderIdx = countQuery.IndexOf(" ORDER BY");
            if (orderIdx > 0)
            {
                countQuery = countQuery.Substring(0, orderIdx);
            }

            int limitIdx = countQuery.IndexOf(" LIMIT ");
            if (limitIdx > 0)
            {
                countQuery = countQuery.Substring(0, limitIdx);
            }

            return _sf.ExecutePagedQuery(baseQuery, countQuery, request.Page, request.PageSize, parameters);
        }

        public long GetRowCount(string tableName, string filterJson = null)
        {
            var safeName = SnowflakeService.SanitizeIdentifier(tableName);
            if (string.IsNullOrEmpty(filterJson))
            {
                return _sf.ExecuteScalar<long>($"SELECT COUNT(*) FROM {safeName}");
            }

            Dictionary<string, object> parameters;
            var query = _sf.BuildFilteredQuery(safeName, filterJson, null, null, out parameters);
            var countQuery = query.Replace("SELECT *", "SELECT COUNT(*)");
            int orderIdx = countQuery.IndexOf(" ORDER BY");
            if (orderIdx > 0)
                countQuery = countQuery.Substring(0, orderIdx);
            return _sf.ExecuteScalar<long>(countQuery, parameters);
        }
    }
}
