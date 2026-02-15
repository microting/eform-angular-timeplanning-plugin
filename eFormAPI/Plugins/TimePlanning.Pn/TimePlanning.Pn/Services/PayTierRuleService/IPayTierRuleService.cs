/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayTierRuleService;

using System.Threading.Tasks;
using Infrastructure.Models.PayTierRule;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IPayTierRuleService
{
    Task<OperationDataResult<PayTierRulesListModel>> Index(PayTierRulesRequestModel requestModel);
    Task<OperationDataResult<PayTierRuleModel>> Read(int id);
    Task<OperationResult> Create(PayTierRuleCreateModel model);
    Task<OperationResult> Update(int id, PayTierRuleUpdateModel model);
    Task<OperationResult> Delete(int id);
}
