import { Component, EventEmitter, Input, OnInit, Output } from '@angular/core';
import { format } from 'date-fns';
import { PARSING_DATE_FORMAT } from 'src/app/common/const';
import { SiteDto } from 'src/app/common/models';
import { TimePlanningsRequestModel } from '../../../../models';

@Component({
  selector: 'app-working-hours-header',
  templateUrl: './working-hours-header.component.html',
  styleUrls: ['./working-hours-header.component.scss'],
})
export class WorkingHoursHeaderComponent implements OnInit {
  @Input()
  workingHoursRequest: TimePlanningsRequestModel = new TimePlanningsRequestModel();
  @Input() availableSites: SiteDto[] = [];
  @Output()
  filtersChanged: EventEmitter<TimePlanningsRequestModel> = new EventEmitter<TimePlanningsRequestModel>();
  @Output() updateWorkingHours: EventEmitter<void> = new EventEmitter<void>();

  dateRange: any;
  siteId: number;

  constructor() {}

  ngOnInit(): void {}

  updateDateRange(range: Date[]) {
    this.dateRange = range;
    if (this.siteId) {
      this.filtersChanged.emit({
        siteId: this.siteId,
        dateFrom: format(range[0], PARSING_DATE_FORMAT),
        dateTo: format(range[1], PARSING_DATE_FORMAT),
      });
    }
  }

  onSiteChanged(siteId: number) {
    this.siteId = siteId;
    if (this.dateRange) {
      this.filtersChanged.emit({
        siteId,
        dateFrom: format(this.dateRange[0], PARSING_DATE_FORMAT),
        dateTo: format(this.dateRange[1], PARSING_DATE_FORMAT),
      });
    }
  }

  onSaveWorkingHours() {
    this.updateWorkingHours.emit();
  }
}
