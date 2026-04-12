using System;
using System.Web.Http;
using RFC_PORTAL.Services;

namespace RFC_PORTAL.Controllers.Api
{
    [RoutePrefix("api/rfc/dashboard")]
    public class RfcDashboardApiController : ApiController
    {
        private readonly DashboardService _service = new DashboardService();

        [HttpGet, Route("summary")]
        public IHttpActionResult GetSummary()
        {
            try
            {
                var summary = _service.GetSummary();
                return Ok(summary);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("dept-distribution")]
        public IHttpActionResult GetDeptDistribution()
        {
            try
            {
                return Ok(_service.GetDeptDistribution());
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("daily-trend")]
        public IHttpActionResult GetDailyTrend(int days = 30)
        {
            try
            {
                return Ok(_service.GetDailyTrend(days));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("top-errors")]
        public IHttpActionResult GetTopErrors(int limit = 10)
        {
            try
            {
                return Ok(_service.GetTopErrors(limit));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("recent")]
        public IHttpActionResult GetRecentExecutions(int limit = 15)
        {
            try
            {
                return Ok(_service.GetRecentExecutions(limit));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
