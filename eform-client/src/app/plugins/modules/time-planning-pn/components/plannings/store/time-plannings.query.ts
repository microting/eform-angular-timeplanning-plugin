// import { Injectable } from '@angular/core';
// import { Query } from '@datorama/akita';
// import { TimePlanningsState, TimePlanningsStore } from './time-plannings.store';
// import { PaginationModel, SortModel } from 'src/app/common/models';
//
// @Injectable({ providedIn: 'root' })
// export class TimePlanningsQuery extends Query<TimePlanningsState> {
//   constructor(protected store: TimePlanningsStore) {
//     super(store);
//   }
//
//   get pageSetting() {
//     return this.getValue();
//   }
//
//   // selectNameFilter$ = this.select((state) => state.filters.nameFilter);
//   // selectPageSize$ = this.select((state) => state.pagination.pageSize);
//   // selectPagination$ = this.select(
//   //   (state) =>
//   //     new PaginationModel(
//   //       state.totalProperties,
//   //       state.pagination.pageSize,
//   //       state.pagination.offset
//   //     )
//   // );
//   selectSort$ = this.select(
//     (state) => new SortModel(state.pagination.sort, state.pagination.isSortDsc)
//   );
// }
