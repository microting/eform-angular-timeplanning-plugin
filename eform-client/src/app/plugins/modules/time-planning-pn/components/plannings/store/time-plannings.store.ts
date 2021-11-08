import { Injectable } from '@angular/core';
import { persistState, Store, StoreConfig } from '@datorama/akita';
import {
  CommonPaginationState,
} from 'src/app/common/models';

export interface TimePlanningsState {
  pagination: CommonPaginationState;
  totalPlannings: number;
}

function createInitialState(): TimePlanningsState {
  return <TimePlanningsState>{
    pagination: {
      pageSize: 100000,
      sort: 'Date',
      isSortDsc: false,
      offset: 0,
    },
    totalPlannings: 0,
  };
}

const propertiesPersistStorage = persistState({
  include: ['plannings'],
  key: 'timePlanningPn',
  preStorageUpdate(storeName, state: TimePlanningsState) {
    return {
      pagination: state.pagination
    };
  },
});

@Injectable({ providedIn: 'root' })
@StoreConfig({ name: 'plannings', resettable: true })
export class TimePlanningsStore extends Store<TimePlanningsState> {
  constructor() {
    super(createInitialState());
  }
}

export const planningsPersistProvider = {
  provide: 'persistStorage',
  useValue: propertiesPersistStorage,
  multi: true,
};
