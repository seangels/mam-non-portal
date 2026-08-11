import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { LoginFormComponent, SetupFormComponent, SetupFormModule } from './shared/components';
import { AuthGuardService, PublicOnlyGuard, RoleGuard } from './shared/services';
import { SetupCompletedGuard, SetupRequiredGuard } from './core/services/setup.service';
import { HomeComponent } from './pages/home/home.component';
import { ProfileComponent } from './pages/profile/profile.component';
import { UsersComponent, UsersModule } from './pages/users/users.component';
import { StudentsComponent, StudentsModule } from './pages/students/students.component';

const routes: Routes = [
  {
    path: 'setup',
    component: SetupFormComponent,
    canActivate: [SetupRequiredGuard]
  },
  {
    path: 'users',
    component: UsersComponent,
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
  imports: [RouterModule.forRoot(routes, { useHash: true }), UsersModule, StudentsModule, SetupFormModule],
  providers: [AuthGuardService],
  exports: [RouterModule],
  declarations: [
    HomeComponent,
    ProfileComponent
  ]
})
export class AppRoutingModule { }
