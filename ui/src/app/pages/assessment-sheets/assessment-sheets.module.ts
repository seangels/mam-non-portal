import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxCheckBoxModule } from 'devextreme-angular/ui/check-box';
import { DxDataGridModule } from 'devextreme-angular/ui/data-grid';
import { DxFormModule } from 'devextreme-angular/ui/form';
import { DxLoadIndicatorModule } from 'devextreme-angular/ui/load-indicator';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxSelectBoxModule } from 'devextreme-angular/ui/select-box';
import { DxTagBoxModule } from 'devextreme-angular/ui/tag-box';
import { DxTextAreaModule } from 'devextreme-angular/ui/text-area';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import { DxDateBoxModule } from 'devextreme-angular/ui/date-box';
import { AssessmentPickerComponent } from './assessment-picker.component';
import { AssessmentSheetsComponent } from './assessment-sheets.component';
import { AssessmentSheetFormComponent } from './assessment-sheets-form.component';

@NgModule({
  declarations: [
    AssessmentPickerComponent,
    AssessmentSheetsComponent,
    AssessmentSheetFormComponent
  ],
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    DxButtonModule,
    DxCheckBoxModule,
    DxDataGridModule,
    DxFormModule,
    DxLoadIndicatorModule,
    DxPopupModule,
    DxSelectBoxModule,
    DxTagBoxModule,
    DxTextAreaModule,
    DxTextBoxModule,
    DxDateBoxModule
  ],
  exports: [AssessmentSheetsComponent, AssessmentSheetFormComponent]
})
export class AssessmentSheetsModule {}
