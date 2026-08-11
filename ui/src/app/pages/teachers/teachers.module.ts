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
import { DxTextAreaModule } from 'devextreme-angular/ui/text-area';
import { DxTextBoxModule } from 'devextreme-angular/ui/text-box';
import { TeacherDetailComponent } from './teacher-detail.component';
import { TeacherFormComponent } from './teacher-form.component';
import { TeacherPasswordDialogComponent } from './teacher-password-dialog.component';
import { TeachersComponent } from './teachers.component';

@NgModule({
  declarations: [
    TeachersComponent,
    TeacherDetailComponent,
    TeacherFormComponent,
    TeacherPasswordDialogComponent
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
    DxTextAreaModule,
    DxTextBoxModule
  ],
  exports: [TeachersComponent, TeacherDetailComponent, TeacherFormComponent]
})
export class TeachersModule {}
