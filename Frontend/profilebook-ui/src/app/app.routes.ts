import { Routes } from '@angular/router';

import { WelcomeComponent } from './pages/welcome/welcome';
import { LoginComponent } from './pages/login/login';
import { SignupComponent } from './pages/signup/signup';
import { ForgotPasswordComponent } from './pages/forgot-password/forgot-password';
import { ResetPasswordComponent } from './pages/reset-password/reset-password';

import { AdminDashboardComponent } from './pages/admin-dashboard/admin-dashboard';
import { UserDashboardComponent } from './pages/user-dashboard/user-dashboard';
import { UserMessagesComponent } from './pages/user-messages/user-messages';
import { UserNotificationsComponent } from './pages/user-notifications/user-notifications';
import { UserFriendRequestsComponent } from './pages/user-friend-requests/user-friend-requests';
import { UserSearchUsersComponent } from './pages/user-search-users/user-search-users';
import { UserProfileComponent } from './pages/user-profile/user-profile';

import { ManageUsersComponent } from './pages/manage-users/manage-users';
import { ReportsComponent } from './pages/reports/reports';
import { ReviewPostsComponent } from './pages/review-posts/review-posts';
import { GroupsComponent } from './pages/groups/groups';

import { AdminGuard } from './services/admin.guard';
import { MainAdminGuard } from './services/main-admin.guard';
import { UserGuard } from './services/user.guard';

export const routes: Routes = [
  { path: '', component: WelcomeComponent },

  { path: 'login', component: LoginComponent },

  { path: 'signup', component: SignupComponent },

  { path: 'forgot-password', component: ForgotPasswordComponent },

  { path: 'reset-password', component: ResetPasswordComponent },

  { path: 'user-dashboard', component: UserDashboardComponent, canActivate: [UserGuard] },
  { path: 'user/profile', component: UserProfileComponent, canActivate: [UserGuard] },
  { path: 'user/messages', component: UserMessagesComponent, canActivate: [UserGuard] },
  { path: 'user/notifications', component: UserNotificationsComponent, canActivate: [UserGuard] },
  { path: 'user/friend-requests', component: UserFriendRequestsComponent, canActivate: [UserGuard] },
  { path: 'user/search-users', component: UserSearchUsersComponent, canActivate: [UserGuard] },

  {
    path: 'admin',
    canActivate: [AdminGuard],
    children: [
      { path: 'dashboard', component: AdminDashboardComponent },
      { path: 'manage-users', component: ManageUsersComponent, canActivate: [MainAdminGuard] },
      { path: 'groups', component: GroupsComponent, canActivate: [MainAdminGuard] },
      { path: 'reports', component: ReportsComponent },
      { path: 'review-posts', component: ReviewPostsComponent }
    ]
  }
];
