using System;
using System.Web.Http;
using RFC_PORTAL.Models;
using RFC_PORTAL.Services;

namespace RFC_PORTAL.Controllers.Api
{
    [RoutePrefix("api/rfc/connections")]
    public class RfcConnectionApiController : ApiController
    {
        private readonly SapConnectionService _service = new SapConnectionService();
        private readonly AuditService _audit = new AuditService();

        [HttpGet, Route("")]
        public IHttpActionResult GetAll()
        {
            try { return Ok(_service.GetAll()); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var conn = _service.GetById(id);
                if (conn == null) return NotFound();
                // Mask password for security
                if (conn.ContainsKey("RFC_PASSWORD_ENC"))
                    conn["RFC_PASSWORD_ENC"] = "********";
                return Ok(conn);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPut, Route("{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] SapConnectionModel model)
        {
            try
            {
                var existing = _service.GetById(id);
                if (existing == null) return NotFound();

                _service.Update(id, model);
                _audit.LogChange("RFC_SAP_CONNECTION", null, id, "Updated", null, null, null, "portal_user");
                return Ok(new { success = true, message = "Connection updated" });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPost, Route("")]
        public IHttpActionResult Create([FromBody] SapConnectionModel model)
        {
            try
            {
                _service.Create(model);
                _audit.LogCreate("RFC_SAP_CONNECTION", null, 0, "portal_user");
                return Ok(new { success = true, message = "Connection created" });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("active")]
        public IHttpActionResult GetActive()
        {
            try { return Ok(_service.GetActiveConnections()); }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
