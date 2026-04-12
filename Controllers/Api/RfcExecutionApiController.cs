using System;
using System.Web.Http;
using RFC_PORTAL.Models;
using RFC_PORTAL.Services;

namespace RFC_PORTAL.Controllers.Api
{
    [RoutePrefix("api/rfc/execution")]
    public class RfcExecutionApiController : ApiController
    {
        private readonly RfcLogService _logService = new RfcLogService();
        private readonly SnowflakeService _sf = new SnowflakeService();
        private readonly RfcMasterService _masterService = new RfcMasterService();

        [HttpGet, Route("logs")]
        public IHttpActionResult GetLogs([FromUri] LogFilterRequest filter)
        {
            try
            {
                if (filter == null) filter = new LogFilterRequest();
                return Ok(_logService.GetExecutionLogs(filter));
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("logs/{id:int}")]
        public IHttpActionResult GetLogById(int id)
        {
            try
            {
                var log = _logService.GetExecutionLogById(id);
                if (log == null) return NotFound();

                var details = _logService.GetExecutionDetails(id);
                return Ok(new { log, details });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("logs/rfc/{rfcId:int}")]
        public IHttpActionResult GetLogsByRfcId(int rfcId, int limit = 20)
        {
            try
            {
                return Ok(_logService.GetExecutionLogsByRfcId(rfcId, limit));
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPost, Route("execute/{rfcId:int}")]
        public IHttpActionResult Execute(int rfcId, [FromBody] ExecuteRfcRequest request)
        {
            try
            {
                var rfc = _masterService.GetById(rfcId);
                if (rfc == null) return NotFound();

                var rfcCode = rfc["RFC_CODE"]?.ToString();
                var runId = $"RFC-{DateTime.Now:yyyyMMdd-HHmmss}";

                // Create execution log entry
                var sql = @"INSERT INTO RFC_EXECUTION_LOG
                    (RUN_ID, RFC_ID, RFC_CODE, ENVIRONMENT, STARTED_DT, STATUS, TRIGGERED_BY, DATE_RANGE_FROM, DATE_RANGE_TO)
                    VALUES (:runId, :rfcId, :rfcCode, :env, CURRENT_TIMESTAMP(), 'Queued', :user, :dateFrom, :dateTo)";

                _sf.ExecuteNonQuery(sql, new System.Collections.Generic.Dictionary<string, object>
                {
                    { "runId", runId },
                    { "rfcId", rfcId },
                    { "rfcCode", rfcCode },
                    { "env", request?.Environment ?? "PROD" },
                    { "user", request?.TriggeredBy ?? "portal_user" },
                    { "dateFrom", string.IsNullOrEmpty(request?.DateFrom) ? (object)DBNull.Value : request.DateFrom },
                    { "dateTo", string.IsNullOrEmpty(request?.DateTo) ? (object)DBNull.Value : request.DateTo }
                });

                // Trigger the RFC Worker process in the background
                try
                {
                    var workerPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RfcWorker", "RfcWorker.exe");
                    if (!System.IO.File.Exists(workerPath))
                        workerPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "RfcWorker", "bin", "Debug", "RfcWorker.exe");

                    if (System.IO.File.Exists(workerPath))
                    {
                        var args = $"{rfcCode} --env {request?.Environment ?? "PROD"}";
                        if (!string.IsNullOrEmpty(request?.DateFrom)) args += $" --date-from {request.DateFrom}";
                        if (!string.IsNullOrEmpty(request?.DateTo)) args += $" --date-to {request.DateTo}";
                        if (!string.IsNullOrEmpty(request?.StoreFilter)) args += $" --stores {request.StoreFilter}";
                        args += $" --triggered-by \"{request?.TriggeredBy ?? "Portal User"}\"";

                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = workerPath,
                            Arguments = args,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                }
                catch { /* Worker trigger is best-effort; log entry already created */ }

                return Ok(new
                {
                    success = true,
                    runId,
                    message = $"Execution started for {rfcCode}. Run ID: {runId}"
                });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("{runId}/status")]
        public IHttpActionResult GetExecutionStatus(string runId)
        {
            try
            {
                var results = _sf.QueryAsList(
                    "SELECT * FROM RFC_EXECUTION_LOG WHERE RUN_ID=:runId",
                    new System.Collections.Generic.Dictionary<string, object> { { "runId", runId } });

                if (results.Count == 0) return NotFound();
                return Ok(results[0]);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
