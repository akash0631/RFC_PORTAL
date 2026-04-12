using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using RFC_PORTAL.Models;
using RFC_PORTAL.Services;

namespace RFC_PORTAL.Controllers.Api
{
    [RoutePrefix("api/rfc/export")]
    public class RfcExportApiController : ApiController
    {
        private readonly CsvExportService _service = new CsvExportService();
        private readonly DataExplorerService _dataService = new DataExplorerService();

        [HttpPost, Route("")]
        public IHttpActionResult RequestExport([FromBody] ExportRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.TableName))
                    return BadRequest("TableName is required");

                // Check row count to decide sync vs async
                var rowCount = _dataService.GetRowCount(request.TableName, request.FilterJson);

                if (rowCount <= 100000)
                {
                    // Sync export - return file directly
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    var stream = new MemoryStream();
                    _service.ExportSmall(request.TableName, request.FilterJson, request.SortBy, request.SortDir, stream);
                    stream.Position = 0;
                    response.Content = new StreamContent(stream);
                    response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
                    response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                    {
                        FileName = $"{request.TableName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                    };
                    return ResponseMessage(response);
                }
                else
                {
                    // Async export — queue and auto-start background processing
                    var jobId = _service.QueueExportJob(request);

                    // Start background processing
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try { _service.ProcessExportJob(jobId); }
                        catch { /* Logged inside ProcessExportJob */ }
                    });

                    return Ok(new
                    {
                        success = true,
                        async_ = true,
                        jobId,
                        rowCount,
                        message = $"Export started. {rowCount:N0} rows will be exported in background. Job ID: {jobId}"
                    });
                }
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("{jobId:int}/status")]
        public IHttpActionResult GetJobStatus(int jobId)
        {
            try
            {
                var job = _service.GetJobStatus(jobId);
                if (job == null) return NotFound();
                return Ok(job);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("{jobId:int}/download")]
        public IHttpActionResult DownloadExport(int jobId)
        {
            try
            {
                var filePath = _service.GetExportFilePath(jobId);
                if (filePath == null || !File.Exists(filePath))
                    return NotFound();

                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StreamContent(new FileStream(filePath, FileMode.Open, FileAccess.Read));
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = Path.GetFileName(filePath)
                };
                return ResponseMessage(response);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("jobs")]
        public IHttpActionResult GetJobs(string requestedBy = null, int limit = 20)
        {
            try { return Ok(_service.GetRecentJobs(requestedBy, limit)); }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
