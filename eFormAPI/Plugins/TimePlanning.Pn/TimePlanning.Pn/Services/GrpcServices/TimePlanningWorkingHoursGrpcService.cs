using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;
using TimePlanning.Pn.Grpc;
using TimePlanning.Pn.Infrastructure.Models.WorkingHours.UpdateCreate;
using TimePlanning.Pn.Services.TimePlanningWorkingHoursService;

namespace TimePlanning.Pn.Services.GrpcServices;

public class TimePlanningWorkingHoursGrpcService
    : Grpc.TimePlanningWorkingHoursService.TimePlanningWorkingHoursServiceBase
{
    private readonly ITimePlanningWorkingHoursService _workingHoursService;

    public TimePlanningWorkingHoursGrpcService(ITimePlanningWorkingHoursService workingHoursService)
    {
        _workingHoursService = workingHoursService;
    }

    public override async Task<ReadWorkingHoursResponse> ReadWorkingHours(
        ReadWorkingHoursRequest request, ServerCallContext context)
    {
        var token = string.IsNullOrEmpty(request.Token) ? null : request.Token;
        var date = DateTime.Parse(request.Date, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        date = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);

        Console.WriteLine($"[DEBUG-GRPC-READ] ReadWorkingHours called: date={request.Date}, sdkSiteId={request.SdkSiteId}, token={(token == null ? "NULL (personal mode)" : token[..Math.Min(8, token.Length)] + "...")}");

        OperationDataResult<Infrastructure.Models.WorkingHours.Index.TimePlanningWorkingHoursModel> result;
        if (token == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-READ] Entering PERSONAL mode branch (ReadFullByCurrentUser)");
            // Personal mode -- resolve user from JWT
            result = await _workingHoursService.ReadFullByCurrentUser(
                date,
                request.Device?.SoftwareVersion,
                request.Device?.DeviceModel,
                request.Device?.Manufacturer,
                request.Device?.OsVersion);
        }
        else
        {
            Console.WriteLine($"[DEBUG-GRPC-READ] Entering KIOSK mode branch (Read) with sdkSiteId={request.SdkSiteId}");
            // Kiosk mode -- device token lookup
            result = await _workingHoursService.Read(request.SdkSiteId, date, token);
        }

        Console.WriteLine($"[DEBUG-GRPC-READ] Service returned: success={result.Success}, message={result.Message ?? "null"}");
        if (result.Success && result.Model != null)
        {
            Console.WriteLine($"[DEBUG-GRPC-READ] Model from service: Id={result.Model.Id}, SdkSiteId={result.Model.SdkSiteId}, Date={result.Model.Date:yyyy-MM-dd}, Start1={result.Model.Shift1Start}, Stop1={result.Model.Shift1Stop}, Pause1={result.Model.Shift1Pause}, Start1StartedAt={result.Model.Start1StartedAt}, Stop1StoppedAt={result.Model.Stop1StoppedAt}, NettoHours={result.Model.NettoHours}");
        }
        else
        {
            Console.WriteLine($"[DEBUG-GRPC-READ] No model returned (success={result.Success}, model is null={result.Model == null})");
        }

        var response = new ReadWorkingHoursResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };

        if (result.Success && result.Model != null)
        {
            response.Model = MapToGrpc(result.Model);
        }

        return response;
    }

    public override async Task<UpdateWorkingHoursResponse> UpdateWorkingHours(
        UpdateWorkingHoursRequest request, ServerCallContext context)
    {
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] UpdateWorkingHours RPC called");
        Console.WriteLine($"[DEBUG-GRPC-UPDATE] request.Date={request.Date}, request.SdkSiteId={request.SdkSiteId}, request.Token={(string.IsNullOrEmpty(request.Token) ? "NULL" : request.Token[..Math.Min(8, request.Token.Length)] + "...")}");
        if (request.Model != null)
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] request.Model: Start1Id={request.Model.Start1Id}, Stop1Id={request.Model.Stop1Id}, Pause1Id={request.Model.Pause1Id}, Start1StartedAt={request.Model.Start1StartedAt}, Stop1StoppedAt={request.Model.Stop1StoppedAt}, Comment={request.Model.Comment}");
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] request.Model shifts 2-5: Start2Id={request.Model.Start2Id}, Stop2Id={request.Model.Stop2Id}, Start3Id={request.Model.Start3Id}, Stop3Id={request.Model.Stop3Id}");
        }
        else
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] WARNING: request.Model is NULL");
        }

        var model = MapFromGrpc(request.Model, request.Device);
        model.Date = DateTime.Parse(request.Date, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        model.Date = new DateTime(model.Date.Year, model.Date.Month, model.Date.Day, 0, 0, 0);

        Console.WriteLine($"[DEBUG-GRPC-UPDATE] After MapFromGrpc: Date={model.Date:yyyy-MM-dd}, Shift1Start={model.Shift1Start}, Shift1Stop={model.Shift1Stop}, Shift1Pause={model.Shift1Pause}, Start1StartedAt={model.Start1StartedAt}, Stop1StoppedAt={model.Stop1StoppedAt}, CommentWorker={model.CommentWorker}");

        var token = string.IsNullOrEmpty(request.Token) ? null : request.Token;

        Microting.eFormApi.BasePn.Infrastructure.Models.API.OperationResult result;
        if (token == null)
        {
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] Entering PERSONAL mode branch (1-param UpdateWorkingHour)");
            // Personal mode -- 1-param overload uses JWT
            result = await _workingHoursService.UpdateWorkingHour(model);
        }
        else
        {
            // Kiosk mode -- 3-param overload uses device token
            int? sdkSiteId = request.SdkSiteId == 0 ? null : request.SdkSiteId;
            Console.WriteLine($"[DEBUG-GRPC-UPDATE] Entering KIOSK mode branch (3-param UpdateWorkingHour) with sdkSiteId={sdkSiteId}");
            result = await _workingHoursService.UpdateWorkingHour(sdkSiteId, model, token);
        }

        Console.WriteLine($"[DEBUG-GRPC-UPDATE] Service returned: success={result.Success}, message={result.Message ?? "null"}");

        return new UpdateWorkingHoursResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };
    }

    public override async Task<CalculateHoursSummaryResponse> CalculateHoursSummary(
        CalculateHoursSummaryRequest request, ServerCallContext context)
    {
        var device = request.Device;
        var result = await _workingHoursService.CalculateHoursSummary(
            DateTime.Parse(request.StartDate, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
            DateTime.Parse(request.EndDate, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
            device?.SoftwareVersion,
            device?.DeviceModel,
            device?.Manufacturer,
            device?.OsVersion);

        var response = new CalculateHoursSummaryResponse
        {
            Success = result.Success,
            Message = result.Message ?? ""
        };

        if (result.Success && result.Model != null)
        {
            response.Model = new HoursSummaryModel
            {
                TotalWorkedHours = result.Model.TotalNettoHours,
                TotalWorkedMinutes = (result.Model.TotalNettoHours % 1) * 60,
                TotalPlannedHours = result.Model.TotalPlanHours,
                TotalPlannedMinutes = (result.Model.TotalPlanHours % 1) * 60,
                TotalFlexHours = result.Model.Difference,
                TotalFlexMinutes = (result.Model.Difference % 1) * 60,
                PaidOutFlex = 0,
                VacationDays = result.Model.VacationDays,
                SickDays = result.Model.SickDays,
                OtherAbsenceDays = result.Model.OtherAbsenceDays,
                AbsenceWithoutPermissionDays = result.Model.AbsenceWithoutPermissionDays,
                SundayHolidayHours = result.Model.SundayHolidayHours
            };
        }

        return response;
    }

    private static string FormatDateTime(DateTime? dt) =>
        dt?.ToString("yyyy-MM-ddTHH:mm:ss.FFFFFF") ?? "";

    private static Grpc.WorkingHoursModel MapToGrpc(
        Infrastructure.Models.WorkingHours.Index.TimePlanningWorkingHoursModel m)
    {
        return new Grpc.WorkingHoursModel
        {
            Id = m.Id ?? 0,
            SdkSiteId = m.SdkSiteId,
            Date = m.Date.ToString("yyyy-MM-dd"),
            PlanText = m.PlanText ?? "",
            PlanHours = m.PlanHours,
            // Shift 1
            Start1Id = m.Shift1Start ?? 0,
            Start1StartedAt = FormatDateTime(m.Start1StartedAt),
            Stop1Id = m.Shift1Stop ?? 0,
            Stop1StoppedAt = FormatDateTime(m.Stop1StoppedAt),
            Pause1Id = m.Shift1Pause ?? 0,
            Pause1StartedAt = FormatDateTime(m.Pause1StartedAt),
            Pause1StoppedAt = FormatDateTime(m.Pause1StoppedAt),
            Shift1PauseNumber = m.Shift1PauseNumber,
            Pause10StartedAt = FormatDateTime(m.Pause10StartedAt),
            Pause10StoppedAt = FormatDateTime(m.Pause10StoppedAt),
            Pause11StartedAt = FormatDateTime(m.Pause11StartedAt),
            Pause11StoppedAt = FormatDateTime(m.Pause11StoppedAt),
            Pause12StartedAt = FormatDateTime(m.Pause12StartedAt),
            Pause12StoppedAt = FormatDateTime(m.Pause12StoppedAt),
            Pause13StartedAt = FormatDateTime(m.Pause13StartedAt),
            Pause13StoppedAt = FormatDateTime(m.Pause13StoppedAt),
            Pause14StartedAt = FormatDateTime(m.Pause14StartedAt),
            Pause14StoppedAt = FormatDateTime(m.Pause14StoppedAt),
            Pause15StartedAt = FormatDateTime(m.Pause15StartedAt),
            Pause15StoppedAt = FormatDateTime(m.Pause15StoppedAt),
            Pause16StartedAt = FormatDateTime(m.Pause16StartedAt),
            Pause16StoppedAt = FormatDateTime(m.Pause16StoppedAt),
            Pause17StartedAt = FormatDateTime(m.Pause17StartedAt),
            Pause17StoppedAt = FormatDateTime(m.Pause17StoppedAt),
            Pause18StartedAt = FormatDateTime(m.Pause18StartedAt),
            Pause18StoppedAt = FormatDateTime(m.Pause18StoppedAt),
            Pause19StartedAt = FormatDateTime(m.Pause19StartedAt),
            Pause19StoppedAt = FormatDateTime(m.Pause19StoppedAt),
            Pause100StartedAt = FormatDateTime(m.Pause100StartedAt),
            Pause100StoppedAt = FormatDateTime(m.Pause100StoppedAt),
            Pause101StartedAt = FormatDateTime(m.Pause101StartedAt),
            Pause101StoppedAt = FormatDateTime(m.Pause101StoppedAt),
            Pause102StartedAt = FormatDateTime(m.Pause102StartedAt),
            Pause102StoppedAt = FormatDateTime(m.Pause102StoppedAt),
            // Shift 2
            Start2Id = m.Shift2Start ?? 0,
            Start2StartedAt = FormatDateTime(m.Start2StartedAt),
            Stop2Id = m.Shift2Stop ?? 0,
            Stop2StoppedAt = FormatDateTime(m.Stop2StoppedAt),
            Pause2Id = m.Shift2Pause ?? 0,
            Pause2StartedAt = FormatDateTime(m.Pause2StartedAt),
            Pause2StoppedAt = FormatDateTime(m.Pause2StoppedAt),
            Shift2PauseNumber = m.Shift2PauseNumber,
            Pause20StartedAt = FormatDateTime(m.Pause20StartedAt),
            Pause20StoppedAt = FormatDateTime(m.Pause20StoppedAt),
            Pause21StartedAt = FormatDateTime(m.Pause21StartedAt),
            Pause21StoppedAt = FormatDateTime(m.Pause21StoppedAt),
            Pause22StartedAt = FormatDateTime(m.Pause22StartedAt),
            Pause22StoppedAt = FormatDateTime(m.Pause22StoppedAt),
            Pause23StartedAt = FormatDateTime(m.Pause23StartedAt),
            Pause23StoppedAt = FormatDateTime(m.Pause23StoppedAt),
            Pause24StartedAt = FormatDateTime(m.Pause24StartedAt),
            Pause24StoppedAt = FormatDateTime(m.Pause24StoppedAt),
            Pause25StartedAt = FormatDateTime(m.Pause25StartedAt),
            Pause25StoppedAt = FormatDateTime(m.Pause25StoppedAt),
            Pause26StartedAt = FormatDateTime(m.Pause26StartedAt),
            Pause26StoppedAt = FormatDateTime(m.Pause26StoppedAt),
            Pause27StartedAt = FormatDateTime(m.Pause27StartedAt),
            Pause27StoppedAt = FormatDateTime(m.Pause27StoppedAt),
            Pause28StartedAt = FormatDateTime(m.Pause28StartedAt),
            Pause28StoppedAt = FormatDateTime(m.Pause28StoppedAt),
            Pause29StartedAt = FormatDateTime(m.Pause29StartedAt),
            Pause29StoppedAt = FormatDateTime(m.Pause29StoppedAt),
            Pause200StartedAt = FormatDateTime(m.Pause200StartedAt),
            Pause200StoppedAt = FormatDateTime(m.Pause200StoppedAt),
            Pause201StartedAt = FormatDateTime(m.Pause201StartedAt),
            Pause201StoppedAt = FormatDateTime(m.Pause201StoppedAt),
            Pause202StartedAt = FormatDateTime(m.Pause202StartedAt),
            Pause202StoppedAt = FormatDateTime(m.Pause202StoppedAt),
            Comment = m.CommentWorker ?? "",
            // Shift 3
            Start3Id = m.Shift3Start ?? 0,
            Start3StartedAt = FormatDateTime(m.Start3StartedAt),
            Stop3Id = m.Shift3Stop ?? 0,
            Stop3StoppedAt = FormatDateTime(m.Stop3StoppedAt),
            Pause3Id = m.Shift3Pause ?? 0,
            Pause3StartedAt = FormatDateTime(m.Pause3StartedAt),
            Pause3StoppedAt = FormatDateTime(m.Pause3StoppedAt),
            // Shift 4
            Start4Id = m.Shift4Start ?? 0,
            Start4StartedAt = FormatDateTime(m.Start4StartedAt),
            Stop4Id = m.Shift4Stop ?? 0,
            Stop4StoppedAt = FormatDateTime(m.Stop4StoppedAt),
            Pause4Id = m.Shift4Pause ?? 0,
            Pause4StartedAt = FormatDateTime(m.Pause4StartedAt),
            Pause4StoppedAt = FormatDateTime(m.Pause4StoppedAt),
            // Shift 5
            Start5Id = m.Shift5Start ?? 0,
            Start5StartedAt = FormatDateTime(m.Start5StartedAt),
            Stop5Id = m.Shift5Stop ?? 0,
            Stop5StoppedAt = FormatDateTime(m.Stop5StoppedAt),
            Pause5Id = m.Shift5Pause ?? 0,
            Pause5StartedAt = FormatDateTime(m.Pause5StartedAt),
            Pause5StoppedAt = FormatDateTime(m.Pause5StoppedAt),
            // Calculations
            NetWorkingHours = m.NettoHours,
            FlexHours = m.FlexHours,
            PaidOutFlex = double.TryParse(m.PaidOutFlex, out var pof) ? pof : 0,
            Message = m.Message?.ToString() ?? "",
            SumFlexStart = m.SumFlexStart,
            SumFlexEnd = m.SumFlexEnd,
        };
    }

    private static TimePlanningWorkingHoursUpdateModel MapFromGrpc(
        Grpc.WorkingHoursModel? m, Grpc.DeviceMetadata? device)
    {
        if (m == null) return new TimePlanningWorkingHoursUpdateModel();

        return new TimePlanningWorkingHoursUpdateModel
        {
            Shift1Start = m.Start1Id != 0 ? m.Start1Id : null,
            Shift1Stop = m.Stop1Id != 0 ? m.Stop1Id : null,
            Shift1Pause = m.Pause1Id != 0 ? m.Pause1Id : null,
            Shift2Start = m.Start2Id != 0 ? m.Start2Id : null,
            Shift2Stop = m.Stop2Id != 0 ? m.Stop2Id : null,
            Shift2Pause = m.Pause2Id != 0 ? m.Pause2Id : null,
            Shift3Start = m.Start3Id != 0 ? m.Start3Id : null,
            Shift3Stop = m.Stop3Id != 0 ? m.Stop3Id : null,
            Shift3Pause = m.Pause3Id != 0 ? m.Pause3Id : null,
            Shift4Start = m.Start4Id != 0 ? m.Start4Id : null,
            Shift4Stop = m.Stop4Id != 0 ? m.Stop4Id : null,
            Shift4Pause = m.Pause4Id != 0 ? m.Pause4Id : null,
            Shift5Start = m.Start5Id != 0 ? m.Start5Id : null,
            Shift5Stop = m.Stop5Id != 0 ? m.Stop5Id : null,
            Shift5Pause = m.Pause5Id != 0 ? m.Pause5Id : null,
            CommentWorker = m.Comment,
            Start1StartedAt = NullIfEmpty(m.Start1StartedAt),
            Stop1StoppedAt = NullIfEmpty(m.Stop1StoppedAt),
            Pause1StartedAt = NullIfEmpty(m.Pause1StartedAt),
            Pause1StoppedAt = NullIfEmpty(m.Pause1StoppedAt),
            Start2StartedAt = NullIfEmpty(m.Start2StartedAt),
            Stop2StoppedAt = NullIfEmpty(m.Stop2StoppedAt),
            Pause2StartedAt = NullIfEmpty(m.Pause2StartedAt),
            Pause2StoppedAt = NullIfEmpty(m.Pause2StoppedAt),
            Start3StartedAt = NullIfEmpty(m.Start3StartedAt),
            Stop3StoppedAt = NullIfEmpty(m.Stop3StoppedAt),
            Start4StartedAt = NullIfEmpty(m.Start4StartedAt),
            Stop4StoppedAt = NullIfEmpty(m.Stop4StoppedAt),
            Start5StartedAt = NullIfEmpty(m.Start5StartedAt),
            Stop5StoppedAt = NullIfEmpty(m.Stop5StoppedAt),
            Pause3StartedAt = NullIfEmpty(m.Pause3StartedAt),
            Pause3StoppedAt = NullIfEmpty(m.Pause3StoppedAt),
            Pause4StartedAt = NullIfEmpty(m.Pause4StartedAt),
            Pause4StoppedAt = NullIfEmpty(m.Pause4StoppedAt),
            Pause5StartedAt = NullIfEmpty(m.Pause5StartedAt),
            Pause5StoppedAt = NullIfEmpty(m.Pause5StoppedAt),
            Pause10StartedAt = NullIfEmpty(m.Pause10StartedAt),
            Pause10StoppedAt = NullIfEmpty(m.Pause10StoppedAt),
            Pause11StartedAt = NullIfEmpty(m.Pause11StartedAt),
            Pause11StoppedAt = NullIfEmpty(m.Pause11StoppedAt),
            Pause12StartedAt = NullIfEmpty(m.Pause12StartedAt),
            Pause12StoppedAt = NullIfEmpty(m.Pause12StoppedAt),
            Pause13StartedAt = NullIfEmpty(m.Pause13StartedAt),
            Pause13StoppedAt = NullIfEmpty(m.Pause13StoppedAt),
            Pause14StartedAt = NullIfEmpty(m.Pause14StartedAt),
            Pause14StoppedAt = NullIfEmpty(m.Pause14StoppedAt),
            Pause15StartedAt = NullIfEmpty(m.Pause15StartedAt),
            Pause15StoppedAt = NullIfEmpty(m.Pause15StoppedAt),
            Pause16StartedAt = NullIfEmpty(m.Pause16StartedAt),
            Pause16StoppedAt = NullIfEmpty(m.Pause16StoppedAt),
            Pause17StartedAt = NullIfEmpty(m.Pause17StartedAt),
            Pause17StoppedAt = NullIfEmpty(m.Pause17StoppedAt),
            Pause18StartedAt = NullIfEmpty(m.Pause18StartedAt),
            Pause18StoppedAt = NullIfEmpty(m.Pause18StoppedAt),
            Pause19StartedAt = NullIfEmpty(m.Pause19StartedAt),
            Pause19StoppedAt = NullIfEmpty(m.Pause19StoppedAt),
            Pause100StartedAt = NullIfEmpty(m.Pause100StartedAt),
            Pause100StoppedAt = NullIfEmpty(m.Pause100StoppedAt),
            Pause101StartedAt = NullIfEmpty(m.Pause101StartedAt),
            Pause101StoppedAt = NullIfEmpty(m.Pause101StoppedAt),
            Pause102StartedAt = NullIfEmpty(m.Pause102StartedAt),
            Pause102StoppedAt = NullIfEmpty(m.Pause102StoppedAt),
            Pause20StartedAt = NullIfEmpty(m.Pause20StartedAt),
            Pause20StoppedAt = NullIfEmpty(m.Pause20StoppedAt),
            Pause21StartedAt = NullIfEmpty(m.Pause21StartedAt),
            Pause21StoppedAt = NullIfEmpty(m.Pause21StoppedAt),
            Pause22StartedAt = NullIfEmpty(m.Pause22StartedAt),
            Pause22StoppedAt = NullIfEmpty(m.Pause22StoppedAt),
            Pause23StartedAt = NullIfEmpty(m.Pause23StartedAt),
            Pause23StoppedAt = NullIfEmpty(m.Pause23StoppedAt),
            Pause24StartedAt = NullIfEmpty(m.Pause24StartedAt),
            Pause24StoppedAt = NullIfEmpty(m.Pause24StoppedAt),
            Pause25StartedAt = NullIfEmpty(m.Pause25StartedAt),
            Pause25StoppedAt = NullIfEmpty(m.Pause25StoppedAt),
            Pause26StartedAt = NullIfEmpty(m.Pause26StartedAt),
            Pause26StoppedAt = NullIfEmpty(m.Pause26StoppedAt),
            Pause27StartedAt = NullIfEmpty(m.Pause27StartedAt),
            Pause27StoppedAt = NullIfEmpty(m.Pause27StoppedAt),
            Pause28StartedAt = NullIfEmpty(m.Pause28StartedAt),
            Pause28StoppedAt = NullIfEmpty(m.Pause28StoppedAt),
            Pause29StartedAt = NullIfEmpty(m.Pause29StartedAt),
            Pause29StoppedAt = NullIfEmpty(m.Pause29StoppedAt),
            Pause200StartedAt = NullIfEmpty(m.Pause200StartedAt),
            Pause200StoppedAt = NullIfEmpty(m.Pause200StoppedAt),
            Pause201StartedAt = NullIfEmpty(m.Pause201StartedAt),
            Pause201StoppedAt = NullIfEmpty(m.Pause201StoppedAt),
            Pause202StartedAt = NullIfEmpty(m.Pause202StartedAt),
            Pause202StoppedAt = NullIfEmpty(m.Pause202StoppedAt),
            Shift1PauseNumber = m.Shift1PauseNumber,
            Shift2PauseNumber = m.Shift2PauseNumber,
            SoftwareVersion = device?.SoftwareVersion,
            Model = device?.DeviceModel,
            Manufacturer = device?.Manufacturer,
            OsVersion = device?.OsVersion,
        };
    }

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrEmpty(s) ? null : s;
}
