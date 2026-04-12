using System.Web.Mvc;

namespace RFC_PORTAL.Controllers
{
    public class SapRfcController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Integrations()
        {
            return View();
        }

        public ActionResult Detail(int id)
        {
            ViewBag.RfcId = id;
            return View();
        }

        public ActionResult Execute(int id)
        {
            ViewBag.RfcId = id;
            return View();
        }

        public ActionResult DataExplorer()
        {
            return View();
        }

        public ActionResult Logs()
        {
            return View();
        }

        public ActionResult Audit()
        {
            return View();
        }

        public ActionResult Connections()
        {
            return View();
        }
    }
}
