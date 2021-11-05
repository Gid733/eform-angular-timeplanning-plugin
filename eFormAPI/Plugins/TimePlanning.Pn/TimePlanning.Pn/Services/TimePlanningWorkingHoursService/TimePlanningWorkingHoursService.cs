/*
The MIT License (MIT)
Copyright (c) 2007 - 2021 Microting A/S
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
namespace TimePlanning.Pn.Services.TimePlanningWorkingHoursService
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using eFormCore;
    using Infrastructure.Models.Settings;
    using Infrastructure.Models.WorkingHours.Index;
    using Infrastructure.Models.WorkingHours.UpdateCreate;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microting.eForm.Infrastructure;
    using Microting.eForm.Infrastructure.Constants;
    using Microting.eFormApi.BasePn.Abstractions;
    using Microting.eFormApi.BasePn.Infrastructure.Helpers.PluginDbOptions;
    using Microting.eFormApi.BasePn.Infrastructure.Models.API;
    using Microting.TimePlanningBase.Infrastructure.Data;
    using Microting.TimePlanningBase.Infrastructure.Data.Entities;
    using TimePlanningLocalizationService;

    /// <summary>
    /// TimePlanningWorkingHoursService
    /// </summary>
    public class TimePlanningWorkingHoursService : ITimePlanningWorkingHoursService
    {
        private readonly IPluginDbOptions<TimePlanningBaseSettings> _options;
        private readonly ILogger<TimePlanningWorkingHoursService> _logger;
        private readonly TimePlanningPnDbContext _dbContext;
        private readonly IUserService _userService;
        private readonly ITimePlanningLocalizationService _localizationService;
        private readonly IEFormCoreService _core;

        public TimePlanningWorkingHoursService(
            ILogger<TimePlanningWorkingHoursService> logger,
            TimePlanningPnDbContext dbContext,
            IUserService userService,
            ITimePlanningLocalizationService localizationService,
            IEFormCoreService core,
            IPluginDbOptions<TimePlanningBaseSettings> options)
        {
            _logger = logger;
            _dbContext = dbContext;
            _userService = userService;
            _localizationService = localizationService;
            _core = core;
            _options = options;
        }

        public async Task<OperationDataResult<List<TimePlanningWorkingHoursModel>>> Index(TimePlanningWorkingHoursRequestModel model)
        {
            try
            {
                var core = await _core.GetCore();
                await using var sdkDbContext = core.DbContextHelper.GetDbContext();
                var eformId = _options.Value.EformId == 0 ? null : _options.Value.EformId;
                var caseId = await sdkDbContext.Cases
                    .Where(x => x.WorkerId == model.SiteId && x.CheckListId == eformId)
                    .OrderByDescending(x => x.DoneAt)
                    .Select(x => x.Id)
                    .FirstOrDefaultAsync();
                var commentWorker = await sdkDbContext.FieldValues
                    .Where(x => x.CaseId == caseId)
                    .Select(x => x.Value)
                    .LastOrDefaultAsync();

                var timePlanningRequest = _dbContext.PlanRegistrations
                    .Include(x => x.AssignedSite)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.AssignedSite.SiteId == model.SiteId);

                // two dates may be displayed instead of one if the same date is selected.
                if (model.DateFrom == model.DateTo)
                {
                    timePlanningRequest = timePlanningRequest
                        .Where(x => x.Date == model.DateFrom);
                }
                else
                {
                    timePlanningRequest = timePlanningRequest
                        .Where(x => x.Date >= model.DateFrom && x.Date <= model.DateTo);
                }


                var timePlannings = await timePlanningRequest
                    .Select(x => new TimePlanningWorkingHoursModel
                    {
                        //WorkerId = pr.AssignedSiteId,
                        WeekDay = (int)x.Date.DayOfWeek,
                        Date = x.Date,
                        PlanText = x.PlanText,
                        PlanHours = x.PlanHours,
                        Shift1Start = x.Start1Id,
                        Shift1Stop = x.Stop1Id,
                        Shift1Pause = x.Pause1Id,
                        Shift2Start = x.Start2Id,
                        Shift2Stop = x.Stop2Id,
                        Shift2Pause = x.Pause2Id,
                        NettoHours = x.NettoHours,
                        FlexHours = x.Flex,
                        SumFlex = x.SumFlex,
                        PaidOutFlex = x.PaiedOutFlex,
                        Message = x.MessageId,
                        CommentWorker = commentWorker,
                        CommentOffice = x.CommentOffice ?? "",
                        CommentOfficeAll = x.CommentOfficeAll ?? "",
                    })
                    .ToListAsync();

                var date = (int)(model.DateTo - model.DateFrom).TotalDays + 1;

                if (timePlannings.Count < date)
                {
                    var timePlanningForAdd = new List<TimePlanningWorkingHoursModel>();
                    for (var i = 0; i < date; i++)
                    {
                        if (timePlannings.All(x => x.Date != model.DateFrom.AddDays(i)))
                        {
                            timePlanningForAdd.Add(new TimePlanningWorkingHoursModel
                            {
                                Date = model.DateFrom.AddDays(i),
                                WeekDay = (int)model.DateFrom.AddDays(i).DayOfWeek,
                                //WorkerId = model.WorkerId,
                            });
                        }
                    }
                    timePlannings.AddRange(timePlanningForAdd);
                }

                timePlannings = timePlannings.OrderBy(x => x.Date).ToList();

                return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                    true,
                    timePlannings);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileObtainingPlannings"));
            };
        }

        public async Task<OperationResult> CreateUpdate(TimePlanningWorkingHoursUpdateCreateModel model)
        {
            try
            {
                var assignedSiteId = await _dbContext.AssignedSites.Where(x => x.SiteId == model.SiteId)
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Select(x => x.Id)
                    .FirstAsync();
                var planRegistrations = await _dbContext.PlanRegistrations
                    .Where(x => x.WorkflowState != Constants.WorkflowStates.Removed)
                    .Where(x => x.AssignedSiteId == assignedSiteId)
                    .ToListAsync();
                foreach (var planning in model.Plannings)
                {
                    var planningFomrDb = planRegistrations.FirstOrDefault(x => x.Date == planning.Date);
                    if (planningFomrDb != null)
                    {
                        await UpdatePlanning(planningFomrDb, planning);
                    }
                    else
                    {
                        await CreatePlanning(planning, assignedSiteId);
                    }
                }
                return new OperationResult(
                    true,
                    _localizationService.GetString("SuccessfullyCreateOrUpdatePlanning"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
                return new OperationDataResult<List<TimePlanningWorkingHoursModel>>(
                    false,
                    _localizationService.GetString("ErrorWhileCreateUpdatePlannings"));
            }
        }

        private async Task CreatePlanning(TimePlanningWorkingHoursModel model, int assignedSiteId)
        {
            try
            {
                var planRegistration = new PlanRegistration
                {
                    MessageId = model.Message,
                    PlanText = model.PlanText,
                    AssignedSiteId = assignedSiteId,
                    Date = model.Date,
                    PlanHours = model.PlanHours,
                    CreatedByUserId = _userService.UserId,
                    UpdatedByUserId = _userService.UserId,
                    CommentOffice = model.CommentOffice,
                    CommentOfficeAll = model.CommentOfficeAll,
                    NettoHours = model.NettoHours,
                    PaiedOutFlex = model.PaidOutFlex,
                    Pause1Id = model.Shift1Pause ?? 0,
                    Pause2Id = model.Shift1Pause ?? 0,
                    Start1Id = model.Shift1Start ?? 0,
                    Start2Id = model.Shift2Start ?? 0,
                    Stop1Id = model.Shift1Stop ?? 0,
                    Stop2Id = model.Shift2Stop ?? 0,
                    Flex = model.FlexHours,
                    SumFlex = model.SumFlex,
                };

                await planRegistration.Create(_dbContext);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
            }
        }

        private async Task UpdatePlanning(PlanRegistration planRegistration,
            TimePlanningWorkingHoursModel model)
        {
            try
            {
                planRegistration.MessageId = model.Message;
                planRegistration.PlanText = model.PlanText;
                planRegistration.Date = model.Date;
                planRegistration.PlanHours = model.PlanHours;
                planRegistration.UpdatedByUserId = _userService.UserId;
                planRegistration.CommentOffice = model.CommentOffice;
                planRegistration.CommentOfficeAll = model.CommentOfficeAll;
                planRegistration.NettoHours = model.NettoHours;
                planRegistration.PaiedOutFlex = model.PaidOutFlex;
                planRegistration.Pause1Id = model.Shift1Pause ?? 0;
                planRegistration.Pause2Id = model.Shift1Pause ?? 0;
                planRegistration.Start1Id = model.Shift1Start ?? 0;
                planRegistration.Start2Id = model.Shift2Start ?? 0;
                planRegistration.Stop1Id = model.Shift1Stop ?? 0;
                planRegistration.Stop2Id = model.Shift2Stop ?? 0;
                planRegistration.Flex = model.FlexHours;
                planRegistration.SumFlex = model.SumFlex;

                await planRegistration.Update(_dbContext);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _logger.LogError(e.Message);
            }
        }

    }
}