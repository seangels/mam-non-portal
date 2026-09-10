import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { DxLoadIndicatorModule } from 'devextreme-angular/ui/load-indicator';
import { DxSelectBoxModule } from 'devextreme-angular/ui/select-box';
import { DxTextAreaModule } from 'devextreme-angular/ui/text-area';
import { AssessmentResultsComponent } from './assessment-results.component';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    DxButtonModule,
    DxDataGridModule,
    DxLoadIndicatorModule,
    DxSelectBoxModule,
    DxTextAreaModule
  ],
  declarations: [AssessmentResultsComponent],
  exports: [AssessmentResultsComponent]
})
export class AssessmentResultsModule {}
