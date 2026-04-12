using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using SAP.Middleware.Connector;
using Snowflake.Data.Client;

namespace RfcWorker
{
    /// <summary>
    /// Core execution engine. Reads RFC config from Snowflake, executes SAP RFC via NCo,
    /// writes results back to Snowflake, and logs everything.
    /// </summary>
    public class RfcEngine
    {
        private readonly string _sfConnStr;

        public RfcEngine()
        {
            _sfConnStr = System.Configuration.ConfigurationManager.ConnectionStrings["Snowflake"]?.ConnectionString
                ?? "account=iafphkw-hh80816;user=akashv2kart;password=SVXqEe5pDdamMb9;db=V2RETAIL;schema=GOLD;warehouse=V2_WH;";
        }

        public int Execute(string rfcCode, string env, string dateFrom, string dateTo, string storeFilter, string triggeredBy)
        {
            var runId = $"RFC-{DateTime.Now:yyyyMMdd-HHmmss}";

            // 1. Load RFC configuration from Snowflake
            var rfcConfig = LoadRfcConfig(rfcCode);
            if (rfcConfig == null)
            {
                Console.WriteLine($"ERROR: RFC '{rfcCode}' not found in RFC_MASTER");
                return 1;
            }

            int rfcId = Convert.ToInt32(rfcConfig["ID"]);
            string functionName = rfcConfig["RFC_FUNCTION_NAME"]?.ToString();
            string targetTable = rfcConfig["TARGET_TABLE"]?.ToString();
            string sapReturnTable = rfcConfig["SAP_RETURN_TABLE"]?.ToString() ?? "ET_DATA";
            string executionPattern = rfcConfig["EXECUTION_PATTERN"]?.ToString() ?? "Single";
            string writeMode = rfcConfig["WRITE_MODE"]?.ToString() ?? "Append";
            string loopSourceQuery = rfcConfig["LOOP_SOURCE_QUERY"]?.ToString();
            int batchSize = Convert.ToInt32(rfcConfig["BULK_BATCH_SIZE"] ?? 100000);
            int timeoutSec = Convert.ToInt32(rfcConfig["TIMEOUT_SECONDS"] ?? 3600);
            int maxRetry = Convert.ToInt32(rfcConfig["MAX_RETRY"] ?? 0);
            int sapConnectionId = Convert.ToInt32(rfcConfig["SAP_CONNECTION_ID"] ?? 1);

            // 2. Load SAP connection config
            var sapConn = LoadSapConnection(sapConnectionId, env);
            if (sapConn == null)
            {
                Console.WriteLine($"ERROR: SAP Connection ID={sapConnectionId} ENV={env} not found");
                return 1;
            }

            // 3. Load RFC parameters
            var rfcParams = LoadRfcParams(rfcId);

            // 4. Create execution log
            int logId = CreateExecutionLog(runId, rfcId, rfcCode, env, triggeredBy, dateFrom, dateTo);

            Console.WriteLine($"Run ID:      {runId}");
            Console.WriteLine($"Function:    {functionName}");
            Console.WriteLine($"Pattern:     {executionPattern}");
            Console.WriteLine($"Target:      {targetTable}");
            Console.WriteLine($"SAP Server:  {sapConn["APP_SERVER_HOST"]}:{sapConn["SYSTEM_NUMBER"]}");
            Console.WriteLine();

            var totalSw = Stopwatch.StartNew();
            int totalItems = 0;
            int processedItems = 0;
            long totalRecordsPulled = 0;
            long totalRecordsPushed = 0;
            string errorMessage = null;
            string errorCategory = null;
            string finalStatus = "Success";

            try
            {
                // 5. Build loop items based on execution pattern
                var loopItems = BuildLoopItems(executionPattern, loopSourceQuery, storeFilter, dateFrom, dateTo);
                totalItems = loopItems.Count;

                UpdateExecutionLogItems(logId, totalItems);
                Console.WriteLine($"Total items to process: {totalItems}");
                Console.WriteLine();

                // 6. Connect to SAP via NCo
                Console.WriteLine("Connecting to SAP...");
                RfcConfigParameters rfcPar = new RfcConfigParameters();
                rfcPar.Add(RfcConfigParameters.Name, sapConn["CONNECTION_NAME"]?.ToString() ?? "SAP");
                rfcPar.Add(RfcConfigParameters.AppServerHost, sapConn["APP_SERVER_HOST"].ToString());
                rfcPar.Add(RfcConfigParameters.Client, sapConn["CLIENT"].ToString());
                rfcPar.Add(RfcConfigParameters.SystemNumber, sapConn["SYSTEM_NUMBER"].ToString());
                rfcPar.Add(RfcConfigParameters.User, sapConn["RFC_USER"].ToString());
                rfcPar.Add(RfcConfigParameters.Password, sapConn["RFC_PASSWORD_ENC"].ToString());
                rfcPar.Add(RfcConfigParameters.Language, sapConn["LANGUAGE"]?.ToString() ?? "EN");
                RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
                dest.Ping(); // Validate connection
                RfcRepository rfcrep = dest.Repository;
                Console.WriteLine("SAP connected.");
                Console.WriteLine();

                // 7. Process each loop item
                foreach (var item in loopItems)
                {
                    var itemSw = Stopwatch.StartNew();
                    string itemKey = item.ContainsKey("key") ? item["key"] : $"item_{processedItems + 1}";
                    Console.Write($"  [{processedItems + 1}/{totalItems}] {itemKey}... ");

                    int retryCount = 0;
                    bool itemSuccess = false;

                    while (!itemSuccess && retryCount <= maxRetry)
                    {
                        try
                        {
                            if (retryCount > 0)
                            {
                                Console.Write($"(retry {retryCount}) ");
                                Thread.Sleep(Convert.ToInt32(rfcConfig["RETRY_DELAY_SEC"] ?? 30) * 1000);
                            }

                            // 7a. Create SAP function call
                            IRfcFunction myfun = rfcrep.CreateFunction(functionName);

                            // 7b. Set scalar parameters from RFC_PARAM + loop item values
                            foreach (var param in rfcParams)
                            {
                                string paramName = param["PARAM_NAME"].ToString();
                                string paramType = param["PARAM_TYPE"]?.ToString() ?? "Scalar";
                                string defaultExpr = param["DEFAULT_EXPRESSION"]?.ToString();

                                if (paramType == "Scalar")
                                {
                                    string value = ResolveParamValue(defaultExpr, item, dateFrom, dateTo);
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        try { myfun.SetValue(paramName, value); }
                                        catch { /* Parameter may not exist in this RFC function */ }
                                    }
                                }
                            }

                            // 7c. Invoke RFC
                            myfun.Invoke(dest);

                            // 7d. Get return table
                            IRfcTable rfcTable = myfun.GetTable(sapReturnTable);
                            long pulled = rfcTable.RowCount;

                            // 7e. Convert to DataTable
                            DataTable dt = SapTableToDataTable(rfcTable);

                            // 7f. Write to Snowflake
                            long pushed = WriteToSnowflake(targetTable, dt, writeMode, item, batchSize);

                            totalRecordsPulled += pulled;
                            totalRecordsPushed += pushed;
                            processedItems++;
                            itemSuccess = true;

                            itemSw.Stop();
                            Console.WriteLine($"OK ({pulled} pulled, {pushed} pushed) [{itemSw.Elapsed.TotalSeconds:F1}s]");

                            LogExecutionDetail(logId, itemKey, pulled, pushed, "Success", null, processedItems);
                        }
                        catch (Exception itemEx)
                        {
                            retryCount++;
                            if (retryCount > maxRetry)
                            {
                                itemSw.Stop();
                                processedItems++;
                                Console.WriteLine($"FAILED: {itemEx.Message}");
                                LogExecutionDetail(logId, itemKey, 0, 0, "Failed", itemEx.Message, processedItems);
                            }
                        }
                    }

                    // Update progress every 10 items (reduce Snowflake calls)
                    if (processedItems % 10 == 0 || processedItems == totalItems)
                        UpdateExecutionLogProgress(logId, processedItems, totalRecordsPulled, totalRecordsPushed);
                }
            }
            catch (Exception ex)
            {
                finalStatus = "Failed";
                errorMessage = ex.Message;
                errorCategory = ClassifyError(ex);
                Console.WriteLine();
                Console.WriteLine($"EXECUTION FAILED: {ex.Message}");
            }
            finally
            {
                totalSw.Stop();
                int durationSec = (int)totalSw.Elapsed.TotalSeconds;

                // 8. Update final execution log
                CompleteExecutionLog(logId, finalStatus, durationSec, totalItems, processedItems,
                    totalRecordsPulled, totalRecordsPushed, errorMessage, errorCategory);

                Console.WriteLine();
                Console.WriteLine($"=== Execution Complete ===");
                Console.WriteLine($"Status:   {finalStatus}");
                Console.WriteLine($"Duration: {totalSw.Elapsed}");
                Console.WriteLine($"Items:    {processedItems}/{totalItems}");
                Console.WriteLine($"Records:  {totalRecordsPulled} pulled, {totalRecordsPushed} pushed");
                if (errorMessage != null)
                    Console.WriteLine($"Error:    {errorMessage}");
            }

            return finalStatus == "Success" ? 0 : 1;
        }

        #region Configuration Loading

        private Dictionary<string, object> LoadRfcConfig(string rfcCode)
        {
            using (var conn = new SnowflakeDbConnection(_sfConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM RFC_MASTER WHERE RFC_CODE = ? AND IS_DELETED = FALSE AND STATUS = 'Active'";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "1";
                    p.Value = rfcCode;
                    cmd.Parameters.Add(p);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        var dict = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                            dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        return dict;
                    }
                }
            }
        }

        private Dictionary<string, object> LoadSapConnection(int connectionId, string env)
        {
            using (var conn = new SnowflakeDbConnection(_sfConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM RFC_SAP_CONNECTION WHERE ID = ? AND IS_ACTIVE = TRUE";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "1";
                    p.Value = connectionId;
                    cmd.Parameters.Add(p);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null;
                        var dict = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                            dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        return dict;
                    }
                }
            }
        }

        private List<Dictionary<string, object>> LoadRfcParams(int rfcId)
        {
            var list = new List<Dictionary<string, object>>();
            using (var conn = new SnowflakeDbConnection(_sfConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT * FROM RFC_PARAM WHERE RFC_ID = ? ORDER BY SORT_ORDER";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "1";
                    p.Value = rfcId;
                    cmd.Parameters.Add(p);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var dict = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                                dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                            list.Add(dict);
                        }
                    }
                }
            }
            return list;
        }

        #endregion

        #region Loop Item Building

        private List<Dictionary<string, string>> BuildLoopItems(string pattern, string loopQuery, string storeFilter, string dateFrom, string dateTo)
        {
            var items = new List<Dictionary<string, string>>();

            switch (pattern)
            {
                case "PerStore":
                    var stores = GetStoreList(loopQuery, storeFilter);
                    foreach (var store in stores)
                    {
                        items.Add(new Dictionary<string, string>
                        {
                            { "key", store },
                            { "storecode", store },
                            { "dateFrom", dateFrom },
                            { "dateTo", dateTo }
                        });
                    }
                    break;

                case "PerDate":
                    var startDate = DateTime.Parse(dateFrom);
                    var endDate = DateTime.Parse(dateTo);
                    for (var dt = startDate; dt <= endDate; dt = dt.AddDays(1))
                    {
                        var dateStr = dt.ToString("yyyy-MM-dd");
                        items.Add(new Dictionary<string, string>
                        {
                            { "key", dateStr },
                            { "dateFrom", dateStr },
                            { "dateTo", dateStr }
                        });
                    }
                    break;

                case "Single":
                default:
                    items.Add(new Dictionary<string, string>
                    {
                        { "key", "single" },
                        { "dateFrom", dateFrom },
                        { "dateTo", dateTo }
                    });
                    break;
            }

            return items;
        }

        private List<string> GetStoreList(string loopQuery, string storeFilter)
        {
            if (!string.IsNullOrEmpty(storeFilter))
            {
                return storeFilter.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            }

            if (string.IsNullOrEmpty(loopQuery))
            {
                loopQuery = "SELECT ST_CD AS storecode FROM STORE_PLANT_MASTER ORDER BY ST_CD";
            }

            var stores = new List<string>();
            using (var conn = new SnowflakeDbConnection(_sfConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = loopQuery;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var val = reader.GetValue(0)?.ToString();
                            if (!string.IsNullOrEmpty(val))
                                stores.Add(val);
                        }
                    }
                }
            }
            return stores;
        }

        #endregion

        #region Parameter Resolution

        /// <summary>
        /// Resolves parameter default expressions.
        /// Supports: TODAY, TODAY-N, Loop:column, Static:value
        /// </summary>
        public string ResolveParamValue(string expression, Dictionary<string, string> loopItem, string dateFrom, string dateTo)
        {
            if (string.IsNullOrEmpty(expression)) return "";

            if (expression.StartsWith("Loop:", StringComparison.OrdinalIgnoreCase))
            {
                var col = expression.Substring(5);
                return loopItem.ContainsKey(col) ? loopItem[col] : "";
            }

            if (expression.StartsWith("Static:", StringComparison.OrdinalIgnoreCase))
            {
                return expression.Substring(7);
            }

            if (expression.Equals("TODAY", StringComparison.OrdinalIgnoreCase))
            {
                return DateTime.Now.ToString("yyyyMMdd");
            }

            if (expression.StartsWith("TODAY-", StringComparison.OrdinalIgnoreCase))
            {
                int days;
                if (int.TryParse(expression.Substring(6), out days))
                    return DateTime.Now.AddDays(-days).ToString("yyyyMMdd");
            }

            if (expression.Equals("DATE_FROM", StringComparison.OrdinalIgnoreCase))
            {
                return dateFrom.Replace("-", "");
            }

            if (expression.Equals("DATE_TO", StringComparison.OrdinalIgnoreCase))
            {
                return dateTo.Replace("-", "");
            }

            return expression;
        }

        #endregion

        #region Snowflake Write

        /// <summary>
        /// Write DataTable to Snowflake target table using batch INSERT.
        /// </summary>
        public long WriteToSnowflake(string targetTable, DataTable dt, string writeMode,
            Dictionary<string, string> loopItem, int batchSize)
        {
            if (dt == null || dt.Rows.Count == 0) return 0;

            using (var conn = new SnowflakeDbConnection(_sfConnStr))
            {
                conn.Open();

                // Handle write mode — delete existing data for this loop item
                if (writeMode == "Append")
                {
                    // For PerStore: DELETE WHERE store_col = storeCode AND date_col = date
                    // For PerDate: DELETE WHERE date_col = date
                    // Specific cleanup logic would be configured per RFC
                }

                // Build INSERT statement
                var columns = new List<string>();
                foreach (DataColumn col in dt.Columns)
                    columns.Add(col.ColumnName);

                long inserted = 0;
                var batch = new List<string>();

                foreach (DataRow row in dt.Rows)
                {
                    var values = new List<string>();
                    foreach (DataColumn col in dt.Columns)
                    {
                        var val = row[col];
                        if (val == null || val == DBNull.Value)
                            values.Add("NULL");
                        else if (col.DataType == typeof(string) || col.DataType == typeof(DateTime))
                            values.Add("'" + val.ToString().Replace("'", "''") + "'");
                        else
                            values.Add(val.ToString());
                    }
                    batch.Add("(" + string.Join(",", values) + ")");

                    if (batch.Count >= batchSize)
                    {
                        ExecuteBatchInsert(conn, targetTable, columns, batch);
                        inserted += batch.Count;
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    ExecuteBatchInsert(conn, targetTable, columns, batch);
                    inserted += batch.Count;
                }

                return inserted;
            }
        }

        private void ExecuteBatchInsert(IDbConnection conn, string table, List<string> columns, List<string> valueSets)
        {
            using (var cmd = conn.CreateCommand())
            {
                var sb = new StringBuilder();
                sb.Append($"INSERT INTO {table} ({string.Join(",", columns)}) VALUES ");
                sb.Append(string.Join(",", valueSets));
                cmd.CommandText = sb.ToString();
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region SAP DataTable Conversion

        /// <summary>
        /// Convert SAP IRfcTable to System.Data.DataTable.
        /// Replaces the copy-pasted LinqHelper.ToDataTable() from all 48 console apps.
        /// Handles all SAP data types: DATE, BCD, CHAR, STRING, INT2, INT4, FLOAT, NUM, TIME.
        /// </summary>
        public DataTable SapTableToDataTable(IRfcTable rfcTable)
        {
            var dt = new DataTable();

            // Build column schema from SAP metadata
            for (int i = 0; i < rfcTable.ElementCount; i++)
            {
                var meta = rfcTable.GetElementMetadata(i);
                Type colType;
                switch (meta.DataType)
                {
                    case RfcDataType.DATE:
                        colType = typeof(string);
                        break;
                    case RfcDataType.TIME:
                        colType = typeof(string);
                        break;
                    case RfcDataType.BCD:
                        colType = typeof(decimal);
                        break;
                    case RfcDataType.INT1:
                    case RfcDataType.INT2:
                    case RfcDataType.INT4:
                        colType = typeof(int);
                        break;
                    case RfcDataType.FLOAT:
                        colType = typeof(double);
                        break;
                    default:
                        colType = typeof(string);
                        break;
                }
                dt.Columns.Add(meta.Name, colType);
            }

            // Populate rows
            foreach (IRfcStructure row in rfcTable)
            {
                var dr = dt.NewRow();
                for (int i = 0; i < rfcTable.ElementCount; i++)
                {
                    var meta = rfcTable.GetElementMetadata(i);
                    try
                    {
                        switch (meta.DataType)
                        {
                            case RfcDataType.BCD:
                                dr[i] = row.GetDecimal(meta.Name);
                                break;
                            case RfcDataType.INT1:
                            case RfcDataType.INT2:
                            case RfcDataType.INT4:
                                dr[i] = row.GetInt(meta.Name);
                                break;
                            case RfcDataType.FLOAT:
                                dr[i] = row.GetDouble(meta.Name);
                                break;
                            default:
                                dr[i] = row.GetString(meta.Name);
                                break;
                        }
                    }
                    catch
                    {
                        // Fallback to string representation on any type conversion failure
                        dr[i] = row.GetString(meta.Name);
                    }
                }
                dt.Rows.Add(dr);
            }

            return dt;
        }

        #endregion

        #region Execution Logging

        private int CreateExecutionLog(string runId, int rfcId, string rfcCode, string env, string triggeredBy, string dateFrom, string dateTo)
        {
            using (var conn = new SnowflakeDbConnection(_sfConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO RFC_EXECUTION_LOG
                        (RUN_ID, RFC_ID, RFC_CODE, ENVIRONMENT, STARTED_DT, STATUS, TRIGGERED_BY, DATE_RANGE_FROM, DATE_RANGE_TO)
                        VALUES (?, ?, ?, ?, CURRENT_TIMESTAMP(), 'Running', ?, ?, ?)";
                    AddParam(cmd, runId);
                    AddParam(cmd, rfcId);
                    AddParam(cmd, rfcCode);
                    AddParam(cmd, env);
                    AddParam(cmd, triggeredBy);
                    AddParam(cmd, dateFrom);
                    AddParam(cmd, dateTo);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT MAX(ID) FROM RFC_EXECUTION_LOG WHERE RUN_ID = ?";
                    AddParam(cmd, runId);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        private void UpdateExecutionLogItems(int logId, int totalItems)
        {
            ExecuteNonQuery("UPDATE RFC_EXECUTION_LOG SET TOTAL_ITEMS = ? WHERE ID = ?", totalItems, logId);
        }

        private void UpdateExecutionLogProgress(int logId, int processed, long pulled, long pushed)
        {
            ExecuteNonQuery("UPDATE RFC_EXECUTION_LOG SET PROCESSED_ITEMS=?, TOTAL_RECORDS_PULLED=?, TOTAL_RECORDS_PUSHED=? WHERE ID=?",
                processed, pulled, pushed, logId);
        }

        private void CompleteExecutionLog(int logId, string status, int durationSec, int totalItems, int processed,
            long pulled, long pushed, string errorMsg, string errorCat)
        {
            using (var conn = new SnowflakeDbConnection(_sfConnStr))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"UPDATE RFC_EXECUTION_LOG SET
                        STATUS=?, COMPLETED_DT=CURRENT_TIMESTAMP(), DURATION_SEC=?,
                        TOTAL_ITEMS=?, PROCESSED_ITEMS=?,
                        TOTAL_RECORDS_PULLED=?, TOTAL_RECORDS_PUSHED=?,
                        ERROR_MESSAGE=?, ERROR_CATEGORY=?
                        WHERE ID=?";
                    AddParam(cmd, status);
                    AddParam(cmd, durationSec);
                    AddParam(cmd, totalItems);
                    AddParam(cmd, processed);
                    AddParam(cmd, pulled);
                    AddParam(cmd, pushed);
                    AddParam(cmd, errorMsg);
                    AddParam(cmd, errorCat);
                    AddParam(cmd, logId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void LogExecutionDetail(int logId, string itemKey, long pulled, long pushed, string status, string error, int sortOrder)
        {
            try
            {
                using (var conn = new SnowflakeDbConnection(_sfConnStr))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"INSERT INTO RFC_EXECUTION_DETAIL
                            (EXECUTION_LOG_ID, ITEM_KEY, RFC_START_DT, RFC_END_DT, RFC_RECORDS_PULLED,
                             SQL_START_DT, SQL_END_DT, SQL_RECORDS_PUSHED, STATUS, ERROR_MESSAGE, SORT_ORDER)
                            VALUES (?, ?, CURRENT_TIMESTAMP(), CURRENT_TIMESTAMP(), ?,
                                    CURRENT_TIMESTAMP(), CURRENT_TIMESTAMP(), ?, ?, ?, ?)";
                        AddParam(cmd, logId);
                        AddParam(cmd, itemKey);
                        AddParam(cmd, pulled);
                        AddParam(cmd, pushed);
                        AddParam(cmd, status);
                        AddParam(cmd, error);
                        AddParam(cmd, sortOrder);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { /* Don't fail the main loop for logging errors */ }
        }

        #endregion

        #region Helpers

        private string ClassifyError(Exception ex)
        {
            string msg = ex.Message.ToLower();
            if (msg.Contains("rfc") || msg.Contains("sap")) return "SAP_RFC_CALL";
            if (msg.Contains("timeout")) return "SAP_TIMEOUT";
            if (msg.Contains("connection") && msg.Contains("sap")) return "SAP_CONNECTION";
            if (msg.Contains("snowflake")) return "SNOWFLAKE_CONNECTION";
            if (msg.Contains("insert") || msg.Contains("write")) return "SNOWFLAKE_WRITE";
            return "UNKNOWN";
        }

        private void ExecuteNonQuery(string sql, params object[] values)
        {
            try
            {
                using (var conn = new SnowflakeDbConnection(_sfConnStr))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        foreach (var v in values)
                            AddParam(cmd, v);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { /* Best effort for progress updates */ }
        }

        private static void AddParam(IDbCommand cmd, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = (cmd.Parameters.Count + 1).ToString();
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        #endregion
    }
}
