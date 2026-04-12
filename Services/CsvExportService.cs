using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using RFC_PORTAL.Models;

namespace RFC_PORTAL.Services
{
    public class CsvExportService
    {
        private readonly SnowflakeService _sf = new SnowflakeService();
        private static readonly string ExportBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Exports");

        public CsvExportService()
        {
            if (!Directory.Exists(ExportBasePath))
                Directory.CreateDirectory(ExportBasePath);
        }

        /// <summary>
        /// Synchronous small export — streams directly to the output stream.
        /// Use for datasets under 100K rows.
        /// </summary>
        public void ExportSmall(string tableName, string filterJson, string sortBy, string sortDir, Stream outputStream)
        {
            var safeName = SnowflakeService.SanitizeIdentifier(tableName);
            Dictionary<string, object> parameters;
            var query = _sf.BuildFilteredQuery(safeName, filterJson, sortBy, sortDir, out parameters);

            using (var conn = _sf.GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = query;
                if (parameters != null)
                {
                    foreach (var p in parameters)
                    {
                        var param = cmd.CreateParameter();
                        param.ParameterName = p.Key;
                        param.Value = p.Value ?? DBNull.Value;
                        cmd.Parameters.Add(param);
                    }
                }

                using (var reader = cmd.ExecuteReader())
                using (var writer = new StreamWriter(outputStream, Encoding.UTF8, 8192, leaveOpen: true))
                {
                    // Write header
                    var colNames = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                        colNames.Add(reader.GetName(i));
                    writer.WriteLine(string.Join(",", colNames));

                    // Write rows
                    while (reader.Read())
                    {
                        var values = new List<string>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var val = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString();
                            values.Add(EscapeCsvField(val));
                        }
                        writer.WriteLine(string.Join(",", values));
                    }
                }
            }
        }

        /// <summary>
        /// Queue an async export job for large datasets.
        /// Returns the job ID for status polling.
        /// </summary>
        public int QueueExportJob(ExportRequest request)
        {
            var sql = @"INSERT INTO RFC_EXPORT_JOB
                        (SOURCE_TABLE, SOURCE_SYSTEM, FILTER_JSON, STATUS, REQUESTED_BY, EXPIRES_DT)
                        VALUES (:table, 'Snowflake', :filter, 'Queued', :user, DATEADD(day, 7, CURRENT_TIMESTAMP()))";
            _sf.ExecuteNonQuery(sql, new Dictionary<string, object>
            {
                { "table", request.TableName },
                { "filter", request.FilterJson },
                { "user", request.RequestedBy ?? "portal_user" }
            });

            return _sf.ExecuteScalar<int>("SELECT MAX(ID) FROM RFC_EXPORT_JOB WHERE REQUESTED_BY=:user",
                new Dictionary<string, object> { { "user", request.RequestedBy ?? "portal_user" } });
        }

        /// <summary>
        /// Process a queued export job (called by background worker).
        /// </summary>
        public void ProcessExportJob(int jobId)
        {
            var jobData = _sf.QueryAsList("SELECT * FROM RFC_EXPORT_JOB WHERE ID=:id",
                new Dictionary<string, object> { { "id", jobId } });

            if (jobData.Count == 0) return;
            var job = jobData[0];

            var tableName = job["SOURCE_TABLE"]?.ToString();
            var filterJson = job["FILTER_JSON"]?.ToString();

            // Mark as Processing
            _sf.ExecuteNonQuery("UPDATE RFC_EXPORT_JOB SET STATUS='Processing', STARTED_DT=CURRENT_TIMESTAMP() WHERE ID=:id",
                new Dictionary<string, object> { { "id", jobId } });

            try
            {
                var safeName = SnowflakeService.SanitizeIdentifier(tableName);
                Dictionary<string, object> parameters;
                var query = _sf.BuildFilteredQuery(safeName, filterJson, null, null, out parameters);

                // Get total count
                var countQuery = query.Replace("SELECT *", "SELECT COUNT(*)");
                var orderIdx = countQuery.IndexOf(" ORDER BY");
                if (orderIdx > 0) countQuery = countQuery.Substring(0, orderIdx);
                var totalRows = _sf.ExecuteScalar<long>(countQuery, parameters);

                _sf.ExecuteNonQuery("UPDATE RFC_EXPORT_JOB SET TOTAL_ROWS=:rows WHERE ID=:id",
                    new Dictionary<string, object> { { "rows", totalRows }, { "id", jobId } });

                // Generate CSV file
                var fileName = $"export_{jobId}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(ExportBasePath, fileName);
                long exportedRows = 0;

                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    _sf.StreamQuery(query, reader =>
                    {
                        // Lazy init writer in closure
                    }, parameters);

                    // Use streaming approach
                    using (var conn = _sf.GetConnection())
                    using (var cmd = conn.CreateCommand())
                    using (var writer = new StreamWriter(fileStream, Encoding.UTF8, 65536))
                    {
                        cmd.CommandText = query;
                        if (parameters != null)
                        {
                            foreach (var p in parameters)
                            {
                                var param = cmd.CreateParameter();
                                param.ParameterName = p.Key;
                                param.Value = p.Value ?? DBNull.Value;
                                cmd.Parameters.Add(param);
                            }
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            // Header
                            var colNames = new List<string>();
                            for (int i = 0; i < reader.FieldCount; i++)
                                colNames.Add(reader.GetName(i));
                            writer.WriteLine(string.Join(",", colNames));

                            // Data
                            while (reader.Read())
                            {
                                var values = new List<string>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var val = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString();
                                    values.Add(EscapeCsvField(val));
                                }
                                writer.WriteLine(string.Join(",", values));
                                exportedRows++;

                                // Update progress every 100K rows
                                if (exportedRows % 100000 == 0)
                                {
                                    _sf.ExecuteNonQuery("UPDATE RFC_EXPORT_JOB SET EXPORTED_ROWS=:rows WHERE ID=:id",
                                        new Dictionary<string, object> { { "rows", exportedRows }, { "id", jobId } });
                                }
                            }
                        }
                    }
                }

                var fileSizeMb = new FileInfo(filePath).Length / (1024.0 * 1024.0);

                _sf.ExecuteNonQuery(@"UPDATE RFC_EXPORT_JOB SET
                    STATUS='Ready', EXPORTED_ROWS=:rows, FILE_PATH=:path, FILE_SIZE_MB=:size,
                    COMPLETED_DT=CURRENT_TIMESTAMP() WHERE ID=:id",
                    new Dictionary<string, object>
                    {
                        { "rows", exportedRows }, { "path", filePath },
                        { "size", Math.Round((decimal)fileSizeMb, 2) }, { "id", jobId }
                    });
            }
            catch (Exception ex)
            {
                _sf.ExecuteNonQuery(@"UPDATE RFC_EXPORT_JOB SET
                    STATUS='Failed', ERROR_MESSAGE=:err, COMPLETED_DT=CURRENT_TIMESTAMP() WHERE ID=:id",
                    new Dictionary<string, object> { { "err", ex.Message }, { "id", jobId } });
            }
        }

        public Dictionary<string, object> GetJobStatus(int jobId)
        {
            var results = _sf.QueryAsList("SELECT * FROM RFC_EXPORT_JOB WHERE ID=:id",
                new Dictionary<string, object> { { "id", jobId } });
            return results.Count > 0 ? results[0] : null;
        }

        public List<Dictionary<string, object>> GetRecentJobs(string requestedBy = null, int limit = 20)
        {
            var sql = "SELECT * FROM RFC_EXPORT_JOB";
            var parameters = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(requestedBy))
            {
                sql += " WHERE REQUESTED_BY=:user";
                parameters["user"] = requestedBy;
            }
            sql += $" ORDER BY CREATED_DT DESC LIMIT {limit}";
            return _sf.QueryAsList(sql, parameters);
        }

        public string GetExportFilePath(int jobId)
        {
            var job = GetJobStatus(jobId);
            if (job == null || job["STATUS"]?.ToString() != "Ready") return null;
            return job["FILE_PATH"]?.ToString();
        }

        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return field;
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }
    }
}
