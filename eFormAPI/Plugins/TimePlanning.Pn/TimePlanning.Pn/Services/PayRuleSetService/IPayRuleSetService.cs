/*
The MIT License (MIT)

Copyright (c) 2007 - 2021 Microting A/S
*/

namespace TimePlanning.Pn.Services.PayRuleSetService;

using System.Threading.Tasks;
using Infrastructure.Models.PayRuleSet;
using Microting.eFormApi.BasePn.Infrastructure.Models.API;

public interface IPayRuleSetService
{
    Task<OperationDataResult<PayRuleSetsListModel>> Index(PayRuleSetsRequestModel requestModel);
    Task<OperationDataResult<PayRuleSetModel>> Read(int id);
    Task<OperationResult> Create(PayRuleSetCreateModel model);
    Task<OperationResult> Update(int id, PayRuleSetUpdateModel model);
    Task<OperationResult> Delete(int id);
}
