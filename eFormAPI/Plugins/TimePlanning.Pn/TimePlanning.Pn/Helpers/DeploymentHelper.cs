using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Dto;
using Microting.eForm.Infrastructure.Constants;
using Microting.eForm.Infrastructure.Data.Entities;
using Microting.eForm.Infrastructure.Models;
using Microting.TimePlanningBase.Infrastructure.Data.Entities;
using TimePlanning.Pn.Resources;

namespace TimePlanning.Pn.Helpers;

public class DeploymentHelper
{
    public async Task<int> DeployResults(PlanRegistration planRegistration, int maxHistoryDays, int eFormId, eFormCore.Core core, Site siteInfo, int folderId, string messageText)
        {
            if (planRegistration.StatusCaseId != 0)
            {
                    await core.CaseDelete(planRegistration.StatusCaseId);
            }
            await using var sdkDbContext = core.DbContextHelper.GetDbContext();
            var language = await sdkDbContext.Languages.SingleAsync(x => x.Id == siteInfo.LanguageId);
            var folder = await sdkDbContext.Folders.SingleOrDefaultAsync(x => x.Id == folderId);
            var mainElement = await core.ReadeForm(eFormId, language);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(language.LanguageCode);
            CultureInfo ci = new CultureInfo(language.LanguageCode);
            mainElement.Label = planRegistration.Date.ToString("dddd dd. MMM yyyy", ci);
            mainElement.EndDate = DateTime.UtcNow.AddDays(maxHistoryDays);
            DateTime startDate = new DateTime(2020, 1, 1);
            mainElement.DisplayOrder = (startDate - planRegistration.Date).Days;
            DataElement element = (DataElement)mainElement.ElementList.First();
            element.Label = mainElement.Label;
            element.DoneButtonEnabled = false;
            CDataValue cDataValue = new CDataValue
            {
                InderValue = $"<strong>{Translations.NettoHours}: {planRegistration.NettoHours:0.00}</strong><br/>" +
                             $"{messageText}"
            };
            element.Description = cDataValue;
            DataItem dataItem = element.DataItemList.First();
            dataItem.Color = Constants.FieldColors.Yellow;
            dataItem.Label = $"<strong>{Translations.Date}: {planRegistration.Date.ToString("dddd dd. MMM yyyy", ci)}</strong>";
            cDataValue = new CDataValue
            {
                InderValue = $"{Translations.PlanText}: {planRegistration.PlanText}<br/>"+
                             $"{Translations.PlanHours}: {planRegistration.PlanHours}<br/><br/>" +
                             $"{Translations.Shift_1__start}: {planRegistration.Options[planRegistration.Start1Id > 0 ? planRegistration.Start1Id - 1 : 0]}<br/>" +
                             $"{Translations.Shift_1__pause}: {planRegistration.Options[planRegistration.Pause1Id > 0 ? planRegistration.Pause1Id - 1 : 0]}<br/>" +
                             $"{Translations.Shift_1__end}: {planRegistration.Options[planRegistration.Stop1Id > 0 ? planRegistration.Stop1Id - 1 : 0]}<br/><br/>" +
                             $"{Translations.Shift_2__start}: {planRegistration.Options[planRegistration.Start2Id > 0 ? planRegistration.Start2Id - 1 : 0]}<br/>" +
                             $"{Translations.Shift_2__pause}: {planRegistration.Options[planRegistration.Pause2Id > 0 ? planRegistration.Pause2Id - 1 : 0]}<br/>" +
                             $"{Translations.Shift_2__end}: {planRegistration.Options[planRegistration.Stop2Id > 0 ? planRegistration.Stop2Id - 1 : 0]}<br/><br/>" +
                             $"<strong>{Translations.NettoHours}: {planRegistration.NettoHours:0.00}</strong><br/><br/>" +
                             $"{Translations.Flex}: {planRegistration.Flex:0.00}<br/>" +
                             $"{Translations.SumFlexStart}: {planRegistration.SumFlexEnd:0.00}<br/>" +
                             $"{Translations.PaidOutFlex}: {planRegistration.PaiedOutFlex:0.00}<br/><br/>" +
                             $"<strong>{Translations.Message}:</strong><br/>" +
                             $"{messageText}<br/><br/>"+
                             $"<strong>{Translations.Comments}:</strong><br/>" +
                             $"{planRegistration.WorkerComment?.Replace("\n", "<br>")}<br/><br/>" +
                             $"<strong>{Translations.Comment_office}:</strong><br/>" +
                             $"{planRegistration.CommentOffice?.Replace("\n", "<br>")}<br/><br/>" // +
                             // $"<strong>{Translations.Comment_office_all}:</strong><br/>" +
                             // $"{planRegistration.CommentOffice}<br/>"
            };
            dataItem.Description = cDataValue;

            if (folder != null) mainElement.CheckListFolderName = folder.MicrotingUid.ToString();

            return (int)await core.CaseCreate(mainElement, "", (int)siteInfo.MicrotingUid, folderId);
        }
}