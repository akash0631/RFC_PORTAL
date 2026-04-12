using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using Snowflake.Data.Client;
using RFC_PORTAL.Models;

namespace RFC_PORTAL.Services
{
    public class SnowflakeService
    {
        private readonly string _connectionString;

        public SnowflakeService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["Snowflake"].ConnectionString;
        }

        public IDbConnection GetConnection()
        {
            var conn = new SnowflakeDbConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public DataTable ExecuteQuery(string sql, Dictionary<string, object> parameters = null)
        {
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                AddParameters(cmd, parameters);
                var dt = new DataTable();
                using (var reader = cmd.ExecuteReader())
                {
                    dt.Load(reader);
                }
                return dt;
            }
        }

        public int ExecuteNonQuery(string sql, Dictionary<string, object> parameters = null)
        {
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                AddParameters(cmd, parameters);
                return cmd.ExecuteNonQuery();
            }
        }

        public T ExecuteScalar<T>(string sql, Dictionary<string, object> parameters = null)
        {
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                AddParameters(cmd, parameters);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return default(T);
                return (T)Convert.ChangeType(result, typeof(T));
            }
        }

        public PagedResult<Dictionary<string, object>> ExecutePagedQuery(
            string baseQuery, string countQuery,
            int page, int pageSize,
            Dictionary<string, object> parameters = null)
        {
            var totalCount = ExecuteScalar<long>(countQuery, parameters);
            int offset = (page - 1) * pageSize;
            var pagedSql = $"{baseQuery} LIMIT {pageSize} OFFSET {offset}";
            var dt = ExecuteQuery(pagedSql, parameters);

            var items = new List<Dictionary<string, object>>();
            foreach (DataRow row in dt.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns)
                {
                    dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }
                items.Add(dict);
            }

            return new PagedResult<Dictionary<string, object>>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        public List<Dictionary<string, object>> QueryAsList(string sql, Dictionary<string, object> parameters = null)
        {
            var dt = ExecuteQuery(sql, parameters);
            var list = new List<Dictionary<string, object>>();
            foreach (DataRow row in dt.Rows)
            {
                var dict = new Dictionary<string, object>();
                foreach (DataColumn col in dt.Columns)
                {
                    dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                }
                list.Add(dict);
            }
            return list;
        }

        public List<string> GetTableColumns(string tableName)
        {
            var safeName = SanitizeIdentifier(tableName);
            var dt = ExecuteQuery($"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='GOLD' AND TABLE_NAME='{safeName}' ORDER BY ORDINAL_POSITION");
            return dt.AsEnumerable().Select(r => r.Field<string>("COLUMN_NAME")).ToList();
        }

        public List<Dictionary<string, object>> GetTableColumnsWithTypes(string tableName)
        {
            var safeName = SanitizeIdentifier(tableName);
            var sql = @"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA='GOLD' AND TABLE_NAME=?
                        ORDER BY ORDINAL_POSITION";
            return QueryAsList(sql, new Dictionary<string, object> { { "1", safeName } });
        }

        public void StreamQuery(string sql, Action<IDataReader> rowHandler, Dictionary<string, object> parameters = null)
        {
            using (var conn = GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                AddParameters(cmd, parameters);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rowHandler(reader);
                    }
                }
            }
        }

        public string BuildFilteredQuery(string tableName, string filterJson, string sortBy, string sortDir, out Dictionary<string, object> parameters)
        {
            parameters = new Dictionary<string, object>();
            var safeName = SanitizeIdentifier(tableName);
            var sb = new StringBuilder($"SELECT * FROM {safeName}");
            var conditions = new List<string>();

            if (!string.IsNullOrEmpty(filterJson))
            {
                var filters = Newtonsoft.Json.JsonConvert.DeserializeObject<List<FilterCondition>>(filterJson);
                int paramIdx = 1;
                foreach (var f in filters)
                {
                    var col = SanitizeIdentifier(f.Column);
                    var paramName = $"p{paramIdx}";
                    switch (f.Operator.ToUpper())
                    {
                        case "EQUALS":
                            conditions.Add($"{col} = :{paramName}");
                            parameters[paramName] = f.Value;
                            break;
                        case "CONTAINS":
                            conditions.Add($"{col} ILIKE :{paramName}");
                            parameters[paramName] = $"%{f.Value}%";
                            break;
                        case "STARTS_WITH":
                            conditions.Add($"{col} ILIKE :{paramName}");
                            parameters[paramName] = $"{f.Value}%";
                            break;
                        case "GT":
                            conditions.Add($"{col} > :{paramName}");
                            parameters[paramName] = f.Value;
                            break;
                        case "LT":
                            conditions.Add($"{col} < :{paramName}");
                            parameters[paramName] = f.Value;
                            break;
                        case "BETWEEN":
                            conditions.Add($"{col} BETWEEN :{paramName}a AND :{paramName}b");
                            parameters[$"{paramName}a"] = f.Value;
                            parameters[$"{paramName}b"] = f.ValueTo;
                            break;
                        case "IN":
                            conditions.Add($"{col} IN (:{paramName})");
                            parameters[paramName] = f.Value;
                            break;
                        case "IS_NULL":
                            conditions.Add($"{col} IS NULL");
                            break;
                        case "IS_NOT_NULL":
                            conditions.Add($"{col} IS NOT NULL");
                            break;
                    }
                    paramIdx++;
                }
            }

            if (conditions.Any())
            {
                sb.Append(" WHERE ");
                sb.Append(string.Join(" AND ", conditions));
            }

            if (!string.IsNullOrEmpty(sortBy))
            {
                var safeSort = SanitizeIdentifier(sortBy);
                var dir = sortDir?.ToUpper() == "DESC" ? "DESC" : "ASC";
                sb.Append($" ORDER BY {safeSort} {dir}");
            }

            return sb.ToString();
        }

        public static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        }

        private void AddParameters(IDbCommand cmd, Dictionary<string, object> parameters)
        {
            if (parameters == null) return;
            foreach (var p in parameters)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = p.Key;
                var val = p.Value ?? DBNull.Value;
                param.Value = val;
                if (val is string) param.DbType = DbType.String;
                else if (val is int) param.DbType = DbType.Int32;
                else if (val is long) param.DbType = DbType.Int64;
                else if (val is double) param.DbType = DbType.Double;
                else if (val is decimal) param.DbType = DbType.Decimal;
                else if (val is bool) param.DbType = DbType.Boolean;
                else if (val is DateTime) param.DbType = DbType.DateTime;
                cmd.Parameters.Add(param);
            }
        }
    }

    public class FilterCondition
    {
        public string Column { get; set; }
        public string Operator { get; set; }
        public string Value { get; set; }
        public string ValueTo { get; set; }
    }
}
