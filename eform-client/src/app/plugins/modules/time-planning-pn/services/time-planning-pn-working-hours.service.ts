import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { OperationDataResult, OperationResult } from 'src/app/common/models';
import { ApiBaseService } from 'src/app/common/services';
import {
  TimePlanningModel,
  TimePlanningsRequestModel,
  WorkingHoursModel,
} from '../models';
import {ReportPnGenerateModel} from 'src/app/plugins/modules/items-planning-pn/models';
import {ItemsPlanningPnReportsMethods} from 'src/app/plugins/modules/items-planning-pn/services';

export let TimePlanningPnWorkingHoursMethods = {
  IndexWorkingHours: 'api/time-planning-pn/working-hours/index',
  WorkingHours: 'api/time-planning-pn/working-hours',
  Reports: 'api/time-planning-pn/working-hours/reports/file',
};

@Injectable({
  providedIn: 'root',
})
export class TimePlanningPnWorkingHoursService {
  constructor(private apiBaseService: ApiBaseService) {}

  getWorkingHours(
    model: TimePlanningsRequestModel
  ): Observable<OperationDataResult<TimePlanningModel[]>> {
    return this.apiBaseService.post(
      TimePlanningPnWorkingHoursMethods.IndexWorkingHours,
      model
    );
  }

  updateWorkingHours(model: WorkingHoursModel): Observable<OperationResult> {
    return this.apiBaseService.put(
      TimePlanningPnWorkingHoursMethods.WorkingHours,
      model
    );
  }

  downloadReport(model: TimePlanningsRequestModel): Observable<any> {
    return this.apiBaseService.getBlobData(
      TimePlanningPnWorkingHoursMethods.Reports,
      model
    );
  }
}
