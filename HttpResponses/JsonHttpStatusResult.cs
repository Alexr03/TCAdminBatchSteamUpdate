using System.Net;
using System.Web.Mvc;

namespace TCAdminBatchSteamUpdate.HttpResponses
{
    public class JsonHttpStatusResult : JsonResult
    {
        private readonly HttpStatusCode _httpStatus;

        public JsonHttpStatusResult(object data, HttpStatusCode httpStatus, JsonRequestBehavior behavior = JsonRequestBehavior.AllowGet)
        {
            Data = data;
            _httpStatus = httpStatus;
            JsonRequestBehavior = behavior;
        }

        public override void ExecuteResult(ControllerContext context)
        {
            context.RequestContext.HttpContext.Response.StatusCode = (int)_httpStatus;
            base.ExecuteResult(context);
        }
    }
}