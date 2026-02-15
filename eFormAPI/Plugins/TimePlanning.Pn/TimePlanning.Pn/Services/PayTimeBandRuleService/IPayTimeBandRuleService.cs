/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayTimeBandRuleService;

using System.Threading.Tasks;
using Infrastructure.Models.PayTimeBandRule;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IPayTimeBandRuleService
{
    Task<OperationDataResult<PayTimeBandRulesListModel>> Index(PayTimeBandRulesRequestModel requestModel);
    Task<OperationDataResult<PayTimeBandRuleModel>> Read(int id);
    Task<OperationResult> Create(PayTimeBandRuleCreateModel model);
    Task<OperationResult> Update(int id, PayTimeBandRuleUpdateModel model);
    Task<OperationResult> Delete(int id);
}
