import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Routes, RouterModule } from '@angular/router';
import { LoginFormComponent, SetupFormComponent, SetupFormModule } from './shared/components';
import { AuthGuardService, PublicOnlyGuard, RoleGuard } from './shared/services';
import { SetupCompletedGuard, SetupRequiredGuard } from './core/services/setup.service';
import { HomeComponent } from './pages/home/home.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { UsersComponent, UsersModule } from './pages/users/users.component';
import { StudentsComponent, StudentsModule } from './pages/students/students.component';
import { StudentGroupsComponent, StudentGroupsModule } from './pages/student-groups/student-groups.component';
import { AttendanceComponent, AttendanceModule } from './pages/attendance/attendance.component';
import { TeacherDetailComponent } from './pages/teachers/teacher-detail.component';
import { TeacherFormComponent } from './pages/teachers/teacher-form.component';
import { TeachersComponent } from './pages/teachers/teachers.component';
import { TeachersModule } from './pages/teachers/teachers.module';
import { PendingChangesGuard } from './core/guards/pending-changes.guard';
import { AssessmentsComponent } from './pages/assessments/assessments.component';
import { AssessmentsModule } from './pages/assessments/assessments.module';
import { AssessmentSheetsComponent } from './pages/assessment-sheets/assessment-sheets.component';
import { AssessmentSheetsModule } from './pages/assessment-sheets/assessment-sheets.module';
import { AssessmentSheetFormComponent } from './pages/assessment-sheets/assessment-sheets-form.component';
import { AssessmentSheetPlanPreviewComponent } from './pages/assessment-sheets/assessment-sheet-plan-preview.component';

export const APP_ROUTES: Routes = [
  {
    path: 'setup',
    component: SetupFormComponent,
    canActivate: [SetupRequiredGuard]
  },
  {
    path: 'users',
    component: UsersComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    data: { roles: ['SuperAdmin'] }
  },
  {
    path: 'teachers',
    component: TeachersComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin'] }
  },
  {
    path: 'teachers/new',
    component: TeacherFormComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    canDeactivate: [PendingChangesGuard],
    data: { roles: ['SuperAdmin', 'Admin'], mode: 'create' }
  },
  {
    path: 'teachers/:id/edit',
    component: TeacherFormComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    canDeactivate: [PendingChangesGuard],
    data: { roles: ['SuperAdmin', 'Admin'], mode: 'edit' }
  },
  {
    path: 'teachers/:id',
    component: TeacherDetailComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin'] }
  },
  {
    path: 'student-groups',
    component: StudentGroupsComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin'] }
  },
  {
    path: 'students',
    component: StudentsComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin'] }
  },
  {
    path: 'attendance',
    component: AttendanceComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    canDeactivate: [PendingChangesGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Teacher'] }
  },
  {
    path: 'assessments',
    component: AssessmentsComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Teacher'] }
  },
  {
    path: 'assessment-sheets',
    component: AssessmentSheetsComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Teacher'] }
  },
  {
    path: 'assessment-sheets/new',
    component: AssessmentSheetFormComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    canDeactivate: [PendingChangesGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Teacher'], mode: 'create' }
  },
  {
    path: 'assessment-sheets/:id/edit',
    component: AssessmentSheetFormComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    canDeactivate: [PendingChangesGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Teacher'], mode: 'edit' }
  },
  {
    path: 'assessment-sheets/:id/plan-pdf-preview',
    component: AssessmentSheetPlanPreviewComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService, RoleGuard],
    data: { roles: ['SuperAdmin', 'Admin', 'Teacher'] }
  },
  {
    path: 'profile',
    component: ProfileComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService]
  },
  {
    path: 'home',
    component: HomeComponent,
    canActivate: [SetupCompletedGuard, AuthGuardService]
  },
  {
    path: 'login-form',
    component: LoginFormComponent,
    canActivate: [SetupCompletedGuard, PublicOnlyGuard]
  },
  {
    path: '**',
    redirectTo: 'home'
  }
];

@NgModule({
  imports: [
    CommonModule,
    RouterModule.forRoot(APP_ROUTES, { useHash: true }),
    UsersModule,
    StudentsModule,
    StudentGroupsModule,
    AttendanceModule,
    TeachersModule,
    AssessmentsModule,
    AssessmentSheetsModule,
    SetupFormModule
  ],
  providers: [AuthGuardService],
  exports: [RouterModule],
  declarations: [
    HomeComponent,
    ProfileComponent
  ]
})
export class AppRoutingModule { }
