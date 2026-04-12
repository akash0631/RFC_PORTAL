using System;
using System.Threading.Tasks;
using System.Web.Http;
using RFC_PORTAL.Services;

namespace RFC_PORTAL.Controllers.Api
{
    /// <summary>
    /// Background job processing for async CSV exports.
    /// Called internally to process queued export jobs.
    /// </summary>
    [RoutePrefix("api/rfc/jobs")]
    public class RfcBackgroundJobController : ApiController
    {
        private readonly CsvExportService _exportService = new CsvExportService();
        private readonly SnowflakeService _sf = new SnowflakeService();

        /// <summary>
        /// Process a specific export job by ID.
        /// </summary>
        [HttpPost, Route("process-export/{jobId:int}")]
        public IHttpActionResult ProcessExport(int jobId)
        {
            try
            {
                // Run in background thread
                Task.Run(() =>
                {
                    try
                    {
                        _exportService.ProcessExportJob(jobId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Export job {jobId} failed: {ex.Message}");
                    }
                });

                return Ok(new { success = true, message = $"Export job {jobId} processing started" });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        /// <summary>
        /// Process all queued export jobs.
        /// Can be called by a scheduled task or timer.
        /// </summary>
        [HttpPost, Route("process-queued-exports")]
        public IHttpActionResult ProcessQueuedExports()
        {
            try
            {
                var queuedJobs = _sf.QueryAsList(
                    "SELECT ID FROM RFC_EXPORT_JOB WHERE STATUS = 'Queued' ORDER BY CREATED_DT LIMIT 5");

                int count = 0;
                foreach (var job in queuedJobs)
                {
                    var jobId = Convert.ToInt32(job["ID"]);
                    Task.Run(() =>
                    {
                        try { _exportService.ProcessExportJob(jobId); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Export job {jobId} failed: {ex.Message}"); }
                    });
                    count++;
                }

                return Ok(new { success = true, jobsStarted = count });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
