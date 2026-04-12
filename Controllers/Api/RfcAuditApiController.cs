using System;
using System.Web.Http;
using RFC_PORTAL.Models;
using RFC_PORTAL.Services;

namespace RFC_PORTAL.Controllers.Api
{
    [RoutePrefix("api/rfc/audit")]
    public class RfcAuditApiController : ApiController
    {
        private readonly RfcLogService _logService = new RfcLogService();

        [HttpGet, Route("")]
        public IHttpActionResult GetAuditLogs([FromUri] AuditFilterRequest filter)
        {
            try
            {
                if (filter == null) filter = new AuditFilterRequest();
                return Ok(_logService.GetAuditLogs(filter));
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
