using System;
using System.Web.Http;
using RFC_PORTAL.Models;
using RFC_PORTAL.Services;

namespace RFC_PORTAL.Controllers.Api
{
    [RoutePrefix("api/rfc")]
    public class RfcParamApiController : ApiController
    {
        private readonly RfcParamService _service = new RfcParamService();
        private readonly AuditService _audit = new AuditService();

        // --- Params ---

        [HttpGet, Route("{rfcId:int}/params")]
        public IHttpActionResult GetParams(int rfcId)
        {
            try { return Ok(_service.GetParamsByRfcId(rfcId)); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPost, Route("{rfcId:int}/params")]
        public IHttpActionResult CreateParam(int rfcId, [FromBody] RfcParamModel model)
        {
            try
            {
                model.RfcId = rfcId;
                _service.CreateParam(model);
                _audit.LogCreate("RFC_PARAM", rfcId, 0, "portal_user");
                return Ok(new { success = true });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPut, Route("params/{id:int}")]
        public IHttpActionResult UpdateParam(int id, [FromBody] RfcParamModel model)
        {
            try
            {
                _service.UpdateParam(id, model);
                _audit.LogChange("RFC_PARAM", model.RfcId, id, "Updated", null, null, null, "portal_user");
                return Ok(new { success = true });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpDelete, Route("params/{id:int}")]
        public IHttpActionResult DeleteParam(int id)
        {
            try
            {
                _service.DeleteParam(id);
                _audit.LogChange("RFC_PARAM", null, id, "Deleted", null, null, null, "portal_user");
                return Ok(new { success = true });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // --- Select Options ---

        [HttpGet, Route("{rfcId:int}/select-options")]
        public IHttpActionResult GetSelectOptions(int rfcId)
        {
            try { return Ok(_service.GetSelectOptionsByRfcId(rfcId)); }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPost, Route("{rfcId:int}/select-options")]
        public IHttpActionResult CreateSelectOption(int rfcId, [FromBody] RfcSelectOptionModel model)
        {
            try
            {
                model.RfcId = rfcId;
                _service.CreateSelectOption(model);
                _audit.LogCreate("RFC_SELECT_OPTION", rfcId, 0, "portal_user");
                return Ok(new { success = true });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPut, Route("select-options/{id:int}")]
        public IHttpActionResult UpdateSelectOption(int id, [FromBody] RfcSelectOptionModel model)
        {
            try
            {
                _service.UpdateSelectOption(id, model);
                return Ok(new { success = true });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpDelete, Route("select-options/{id:int}")]
        public IHttpActionResult DeleteSelectOption(int id)
        {
            try
            {
                _service.DeleteSelectOption(id);
                return Ok(new { success = true });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
