using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using TCAdmin.GameHosting.SDK.Objects;
using TCAdmin.SDK.Database;
using TCAdmin.SDK.Web.MVC.Controllers;
using TCAdmin.TaskScheduler.ModuleApi;
using TCAdminBatchSteamUpdate.HttpResponses;
using TCAdminBatchSteamUpdate.Models;

namespace TCAdminBatchSteamUpdate.Controllers
{
    [Authorize]
    public class SteamUpdateController : BaseController
    {
        public ActionResult Index()
        {
            var services = Service.GetServices().Cast<Service>().ToList();

            services.RemoveAll(x => !new TCAdmin.GameHosting.SDK.Objects.Game(x.GameId).Steam.EnableSteamCmd);
            services.RemoveAll(x => !x.GetPermission("SteamUpdate").CurrentUserHasPermission());

            var model = new SteamUpdateModel
            {
                Services = services
            };
            
            return View(model);
        }

        [HttpPost]
        public ActionResult UpdateServices(List<int> checkedNodes)
        {
            if (checkedNodes == null || !checkedNodes.Any())
            {
                return new JsonHttpStatusResult(new
                {
                    Message = "Please choose at least 1 service to update."
                }, HttpStatusCode.BadRequest);
            }

            var servicesToUpdate = checkedNodes.Select(serviceId => new Service(serviceId))
                .Where(service => service.GetPermission("SteamUpdate").CurrentUserHasPermission()).ToList();

            var task = ScheduleSteamUpdates(servicesToUpdate);

            return new JsonHttpStatusResult(new
            {
                url =
                    $"/Aspx/Interface/TaskScheduler/TaskStatusPopup.aspx?taskid={task.TaskId}&redirect={HttpUtility.UrlEncode("/SteamUpdate")}"
            }, HttpStatusCode.OK);
        }

        private TCAdmin.TaskScheduler.SDK.Objects.Task ScheduleSteamUpdates(List<Service> services)
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();

            var taskInfo = new TaskInfo
            {
                CreatedBy = user.UserId,
                RunNow = true,
                UserId = user.UserId,
                DisplayName = $"Batch Steam Update",
                Source = this.GetType().ToString(),
                SourceId = "-1"
            };

            foreach (var step in from service in services
                let arguments = new XmlField
                {
                    ["ScheduledScript.ServiceId"] = service.ServiceId,
                    ["ScheduledScript.ScriptId"] = "steam",
                    ["ScheduledScript.ForceExecution"] = true,
                    ["ScheduledScript.WaitEmpty"] = false,
                    ["ScheduledScript.SkipExecution"] = false,
                    ["ScheduledScript.CheckSteamApiUpdate"] = false
                }
                select new StepInfo
                {
                    ModuleId = "d3b2aa93-7e2b-4e0d-8080-67d14b2fa8a9",
                    ProcessId = 18,
                    ServerId = service.ServerId,
                    Arguments = arguments.ToString(),
                    DisplayName = $"Updating {service.ConnectionInfo} via Steam Update"
                })
            {
                taskInfo.AddStep(step);
            }

            var task = new TCAdmin.TaskScheduler.SDK.Objects.Task(taskInfo.CreateTask().TaskId);
            task.Start();

            return task;
        }
    }
}