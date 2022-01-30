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
        public ActionResult UpdateServices(List<int> checkedNodes, int taskOption = 1)
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

            return ScheduleSteamUpdates(servicesToUpdate, taskOption);
        }

        private JsonHttpStatusResult ScheduleSteamUpdates(List<Service> services, int taskOption)
        {
            var user = TCAdmin.SDK.Session.GetCurrentUser();
            var taskIds = new List<string>();

            switch (taskOption)
            {
                case 1: //A single task for all services
                    var taskInfoSingle = new TaskInfo
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
                                             DisplayName = $"Updating {service.ConnectionInfo}..."
                                         })
                    {
                        taskInfoSingle.AddStep(step);
                    }

                    taskIds.Add(taskInfoSingle.CreateTask().TaskId.ToString());
                    break;

                case 2: //A task for each server
                    var orderedServices = services.ToArray().OrderBy(x => x.ServerId);
                    TaskInfo serverTask = new TaskInfo();
                    var lastServerId = -1;
                    foreach (var service in orderedServices)
                    {
                        if (lastServerId != service.ServerId)
                        {
                            if(lastServerId != -1)
                            {
                                taskIds.Add(serverTask.CreateTask().TaskId.ToString());
                            }

                            lastServerId = service.ServerId;
                            var server = new TCAdmin.SDK.Objects.Server(service.ServerId);
                            serverTask = new TaskInfo
                            {
                                CreatedBy = user.UserId,
                                RunNow = true,
                                UserId = user.UserId,
                                DisplayName = $"Steam Update on {server.Name}",
                                Source = server.GetType().ToString(),
                                SourceId = server.ServerId.ToString()
                            };
                        }

                        var arguments = new XmlField
                        {
                            ["ScheduledScript.ServiceId"] = service.ServiceId,
                            ["ScheduledScript.ScriptId"] = "steam",
                            ["ScheduledScript.ForceExecution"] = true,
                            ["ScheduledScript.WaitEmpty"] = false,
                            ["ScheduledScript.SkipExecution"] = false,
                            ["ScheduledScript.CheckSteamApiUpdate"] = false
                        };

                        var step = new StepInfo
                        {
                            ModuleId = "d3b2aa93-7e2b-4e0d-8080-67d14b2fa8a9",
                            ProcessId = 18,
                            ServerId = service.ServerId,
                            Arguments = arguments.ToString(),
                            DisplayName = $"Updating ${service.ConnectionInfo}..."
                        };
                        serverTask.AddStep(step);

                        if(service.ServiceId == orderedServices.Last().ServiceId)
                        {
                            taskIds.Add(serverTask.CreateTask().TaskId.ToString());
                        }
                    }

                    break;
                case 3: //A task for each service
                    foreach (var service in services)
                    {
                        var arguments = new XmlField
                        {
                            ["ScheduledScript.ServiceId"] = service.ServiceId,
                            ["ScheduledScript.ScriptId"] = "steam",
                            ["ScheduledScript.ForceExecution"] = true,
                            ["ScheduledScript.WaitEmpty"] = false,
                            ["ScheduledScript.SkipExecution"] = false,
                            ["ScheduledScript.CheckSteamApiUpdate"] = false
                        };

                        var step= new StepInfo
                        {
                            ModuleId = "d3b2aa93-7e2b-4e0d-8080-67d14b2fa8a9",
                            ProcessId = 18,
                            ServerId = service.ServerId,
                            Arguments = arguments.ToString(),
                            DisplayName = $"Updating..."
                        };

                        var taskInfoEachService = new TaskInfo
                        {
                            CreatedBy = user.UserId,
                            RunNow = true,
                            UserId = user.UserId,
                            DisplayName = $"Steam Update on {service.ConnectionInfo}",
                            Source = service.GetType().ToString(),
                            SourceId = service.ServiceId.ToString()
                        };

                        taskInfoEachService.AddStep(step);

                        taskIds.Add(taskInfoEachService.CreateTask().TaskId.ToString());
                    }

                    break;
                default:
                    break;
            }

            if (taskIds.Count == 1)
            {
                return new JsonHttpStatusResult(new
                {
                    url = $"/Aspx/Interface/TaskScheduler/TaskStatusPopup.aspx?taskid={taskIds[0]}&redirect={HttpUtility.UrlEncode("/SteamUpdate")}",
                    dialog = true
                }, HttpStatusCode.OK);
            }
            else
            {
                return new JsonHttpStatusResult(new
                {
                    url = $"/Interface/Task/TaskManager?view=1&tasks={string.Join(",", taskIds.ToArray())}",
                    dialog = false
                }, HttpStatusCode.OK);
            }
        }
    }
}
