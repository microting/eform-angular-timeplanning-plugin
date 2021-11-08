import { CommonDictionaryModel } from 'src/app/common/models';

export class TimeFlexesUpdateModel {
  worker: CommonDictionaryModel;
  date: string;
  sumFlex: number;
  paidOutFlex: number;
  commentOffice: string;
  commentOfficeAll: string;
}
