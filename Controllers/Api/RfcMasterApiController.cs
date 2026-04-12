using System;
using System.Web.Http;
using RFC_PORTAL.Models;
using RFC_PORTAL.Services;

namespace RFC_PORTAL.Controllers.Api
{
    [RoutePrefix("api/rfc/master")]
    public class RfcMasterApiController : ApiController
    {
        private readonly RfcMasterService _service = new RfcMasterService();
        private readonly AuditService _audit = new AuditService();

        [HttpGet, Route("")]
        public IHttpActionResult GetAll([FromUri] RfcFilterRequest filter)
        {
            try
            {
                if (filter == null) filter = new RfcFilterRequest();
                var result = _service.GetPaged(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("{id:int}")]
        public IHttpActionResult GetById(int id)
        {
            try
            {
                var rfc = _service.GetById(id);
                if (rfc == null) return NotFound();
                return Ok(rfc);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route("")]
        public IHttpActionResult Create([FromBody] RfcMasterModel model)
        {
            try
            {
                if (model == null) return BadRequest("Model is required");
                var result = _service.Create(model);
                _audit.LogCreate("RFC_MASTER", null, result, model.CreatedBy ?? "portal_user");
                return Ok(new { success = true, message = "RFC created successfully" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPut, Route("{id:int}")]
        public IHttpActionResult Update(int id, [FromBody] RfcMasterModel model)
        {
            try
            {
                var existing = _service.GetById(id);
                if (existing == null) return NotFound();

                _service.Update(id, model);
                _audit.LogChange("RFC_MASTER", id, id, "Updated", null, null, null, model.UpdatedBy ?? "portal_user");
                return Ok(new { success = true, message = "RFC updated successfully" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPatch, Route("{id:int}/toggle")]
        public IHttpActionResult Toggle(int id)
        {
            try
            {
                var rfc = _service.GetById(id);
                if (rfc == null) return NotFound();

                var currentStatus = rfc["STATUS"]?.ToString();
                var newStatus = currentStatus == "Active" ? "Inactive" : "Active";
                _service.UpdateStatus(id, newStatus, "portal_user");
                _audit.LogChange("RFC_MASTER", id, id, "Updated", "STATUS", currentStatus, newStatus, "portal_user");
                return Ok(new { success = true, status = newStatus });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpDelete, Route("{id:int}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var rfc = _service.GetById(id);
                if (rfc == null) return NotFound();

                _service.SoftDelete(id, "portal_user");
                _audit.LogDelete("RFC_MASTER", id, id, "portal_user");
                return Ok(new { success = true, message = "RFC deleted" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("departments")]
        public IHttpActionResult GetDepartments()
        {
            try { return Ok(_service.GetDistinctDepartments()); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("modules")]
        public IHttpActionResult GetModules()
        {
            try { return Ok(_service.GetDistinctModules()); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet, Route("owners")]
        public IHttpActionResult GetOwners()
        {
            try { return Ok(_service.GetDistinctOwners()); }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
