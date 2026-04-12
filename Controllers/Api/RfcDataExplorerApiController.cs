using System;
using System.Web.Http;
using RFC_PORTAL.Models;
using RFC_PORTAL.Services;

namespace RFC_PORTAL.Controllers.Api
{
    [RoutePrefix("api/rfc/data")]
    public class RfcDataExplorerApiController : ApiController
    {
        private readonly DataExplorerService _service = new DataExplorerService();

        [HttpGet, Route("tables")]
        public IHttpActionResult GetTables()
        {
            try { return Ok(_service.GetAllTables()); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("{tableName}/columns")]
        public IHttpActionResult GetColumns(string tableName)
        {
            try { return Ok(_service.GetTableColumns(tableName)); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPost, Route("{tableName}/query")]
        public IHttpActionResult QueryTable(string tableName, [FromBody] DataExplorerRequest request)
        {
            try
            {
                if (request == null) request = new DataExplorerRequest();
                request.TableName = tableName;
                return Ok(_service.QueryTable(request));
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPost, Route("{tableName}/count")]
        public IHttpActionResult GetCount(string tableName, [FromBody] DataExplorerRequest request)
        {
            try
            {
                var count = _service.GetRowCount(tableName, request?.FilterJson);
                return Ok(new { count });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
