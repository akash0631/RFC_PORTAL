using System.Collections.Generic;

namespace RFC_PORTAL.Services
{
    public class AuditService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();

        public void LogChange(string tableName, int? rfcId, int? recordId, string action,
                              string fieldName, string oldValue, string newValue, string changedBy)
        {
            var sql = @"INSERT INTO RFC_CONFIG_AUDIT
                        (RFC_ID, TABLE_NAME, RECORD_ID, ACTION, FIELD_NAME, OLD_VALUE, NEW_VALUE, CHANGED_BY)
                        VALUES (:rfcId, :table, :recordId, :action, :field, :oldVal, :newVal, :user)";
            _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "rfcId", rfcId.HasValue ? (object)rfcId.Value : System.DBNull.Value },
                { "table", tableName },
                { "recordId", recordId.HasValue ? (object)recordId.Value : System.DBNull.Value },
                { "action", action },
                { "field", fieldName },
                { "oldVal", oldValue },
                { "newVal", newValue },
                { "user", changedBy ?? "system" }
            });
        }

        public void LogCreate(string tableName, int? rfcId, int recordId, string changedBy)
        {
            LogChange(tableName, rfcId, recordId, "Created", null, null, null, changedBy);
        }

        public void LogDelete(string tableName, int? rfcId, int recordId, string changedBy)
        {
            LogChange(tableName, rfcId, recordId, "Deleted", null, null, null, changedBy);
        }

        public void LogFieldChange(string tableName, int? rfcId, int recordId,
                                    string fieldName, object oldValue, object newValue, string changedBy)
        {
            var oldStr = oldValue?.ToString();
            var newStr = newValue?.ToString();
            if (oldStr == newStr) return; // No actual change
            LogChange(tableName, rfcId, recordId, "Updated", fieldName, oldStr, newStr, changedBy);
        }

        /// <summary>
        /// Compare two dictionaries and log all field-level changes.
        /// </summary>
        public void LogAllChanges(string tableName, int? rfcId, int recordId,
                                  Dictionary<string, object> oldData, Dictionary<string, object> newData, string changedBy)
        {
            foreach (var key in newData.Keys)
            {
                if (key == "ID" || key == "CREATED_DT" || key == "UPDATED_DT" || key == "CREATED_BY") continue;

                object oldVal = null;
                if (oldData != null && oldData.ContainsKey(key))
                    oldVal = oldData[key];

                var newVal = newData[key];
                var oldStr = oldVal?.ToString() ?? "";
                var newStr = newVal?.ToString() ?? "";

                if (oldStr != newStr)
                {
                    LogChange(tableName, rfcId, recordId, "Updated", key, oldStr, newStr, changedBy);
                }
            }
        }
    }
}
