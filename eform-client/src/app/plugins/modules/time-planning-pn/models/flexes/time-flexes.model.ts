import { CommonDictionaryModel } from 'src/app/common/models';

export class TimeFlexesModel {
  sdkSiteId: number;
  worker: CommonDictionaryModel = new CommonDictionaryModel();
  date: string;
  flexHours: number;
  sumFlex: number;
  paidOutFlex: number;
  commentWorker: string;
  commentOffice: string;
  commentOfficeAll: string;
}
