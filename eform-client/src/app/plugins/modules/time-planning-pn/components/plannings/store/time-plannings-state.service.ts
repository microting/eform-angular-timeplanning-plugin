import { Injectable } from '@angular/core';
import { TimePlanningsStore, TimePlanningsQuery } from './';
import { Observable } from 'rxjs';
import {
  OperationDataResult,
  Paged,
  PaginationModel,
  SortModel,
} from 'src/app/common/models';
import { updateTableSort } from 'src/app/common/helpers';
import { getOffset } from 'src/app/common/helpers/pagination.helper';
import { map } from 'rxjs/operators';
import { TimePlanningModel, TimePlanningsRequestModel } from '../../../models';
import { TimePlanningPnPlanningsService } from '../../../services';
import { arrayToggle } from '@datorama/akita';

@Injectable({ providedIn: 'root' })
export class TimePlanningsStateService {
  constructor(
    private store: TimePlanningsStore,
    private service: TimePlanningPnPlanningsService,
    private query: TimePlanningsQuery
  ) {}

  getSort(): Observable<SortModel> {
    return this.query.selectSort$;
  }

  getPlannings(
    model: TimePlanningsRequestModel
  ): Observable<OperationDataResult<TimePlanningModel[]>> {
    return this.service
      .getPlannings({
        ...model,
        sort: this.query.pageSetting.pagination.sort,
        isSortDsc: this.query.pageSetting.pagination.isSortDsc,
      })
      .pipe(
        map((response) => {
          if (response && response.success && response.model) {
            this.store.update(() => ({
              totalPlannings: 1000000,
            }));
          }
          return response;
        })
      );
  }

  onSortTable(sort: string) {
    const localPageSettings = updateTableSort(
      sort,
      this.query.pageSetting.pagination.sort,
      this.query.pageSetting.pagination.isSortDsc
    );
    this.store.update((state) => ({
      pagination: {
        ...state.pagination,
        isSortDsc: localPageSettings.isSortDsc,
        sort: localPageSettings.sort,
      },
    }));
  }

  checkOffset() {
    const newOffset = getOffset(
      this.query.pageSetting.pagination.pageSize,
      this.query.pageSetting.pagination.offset,
      this.query.pageSetting.totalPlannings
    );
    if (newOffset !== this.query.pageSetting.pagination.offset) {
      this.store.update((state) => ({
        pagination: {
          ...state.pagination,
          offset: newOffset,
        },
      }));
    }
  }
}
