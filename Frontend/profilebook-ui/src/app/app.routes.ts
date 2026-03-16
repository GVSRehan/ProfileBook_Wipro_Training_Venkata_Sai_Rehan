import { Routes } from '@angular/router';

import { WelcomeComponent } from './pages/welcome/welcome';
import { LoginComponent } from './pages/login/login';
import { SignupComponent } from './pages/signup/signup';

import { AdminDashboardComponent } from './pages/admin-dashboard/admin-dashboard';
import { UserDashboardComponent } from './pages/user-dashboard/user-dashboard';

import { ManageUsersComponent } from './pages/manage-users/manage-users';
import { ReviewPostsComponent } from './pages/review-posts/review-posts';
import { ReportsComponent } from './pages/reports/reports';

export const routes: Routes = [

  { path:'', component: WelcomeComponent },

  { path:'login', component: LoginComponent },

  { path:'signup', component: SignupComponent },

  { path:'admin-dashboard', component: AdminDashboardComponent },

  { path:'user-dashboard', component: UserDashboardComponent },

  { path:'manage-users', component: ManageUsersComponent },

  { path:'review-posts', component: ReviewPostsComponent },

  { path:'reports', component: ReportsComponent }

];