import { TimePlanningMessagesEnum } from '../../enums';
import { CommonDictionaryModel } from 'src/app/common/models';

export class TimeFlexesModel {
  worker: CommonDictionaryModel;
  date: string;
  flexHours: number;
  sumFlex: number;
  paidOutFlex: number;
  commentWorker: string;
  commentOffice: string;
  commentOfficeAll: string;
}
