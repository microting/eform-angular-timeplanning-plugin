/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayDayTypeRuleService;

using System.Threading.Tasks;
using Infrastructure.Models.PayDayTypeRule;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IPayDayTypeRuleService
{
    Task<OperationDataResult<PayDayTypeRulesListModel>> Index(PayDayTypeRulesRequestModel requestModel);
    Task<OperationDataResult<PayDayTypeRuleModel>> Read(int id);
    Task<OperationResult> Create(PayDayTypeRuleCreateModel model);
    Task<OperationResult> Update(int id, PayDayTypeRuleUpdateModel model);
    Task<OperationResult> Delete(int id);
}
