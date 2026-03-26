import { Injectable } from '@angular/core';
import { BehaviorSubject, map } from 'rxjs';

type DashboardStats = { totalUsers: number; pendingPosts: number; reports: number };

interface AdminState {
  dashboardStats: DashboardStats;
  managedUsers: any[];
  reports: any[];
  groups: any[];
  expiringAdmins: any[];
}

type AdminAction =
  | { type: 'SET_DASHBOARD_STATS'; payload: DashboardStats }
  | { type: 'SET_MANAGED_USERS'; payload: any[] }
  | { type: 'SET_REPORTS'; payload: any[] }
  | { type: 'SET_GROUPS'; payload: any[] }
  | { type: 'SET_EXPIRING_ADMINS'; payload: any[] };

const initialState: AdminState = {
  dashboardStats: {
    totalUsers: 0,
    pendingPosts: 0,
    reports: 0
  },
  managedUsers: [],
  reports: [],
  groups: [],
  expiringAdmins: []
};

function adminReducer(state: AdminState, action: AdminAction): AdminState {
  switch (action.type) {
    case 'SET_DASHBOARD_STATS':
      return { ...state, dashboardStats: action.payload };
    case 'SET_MANAGED_USERS':
      return { ...state, managedUsers: [...action.payload] };
    case 'SET_REPORTS':
      return { ...state, reports: [...action.payload] };
    case 'SET_GROUPS':
      return { ...state, groups: [...action.payload] };
    case 'SET_EXPIRING_ADMINS':
      return { ...state, expiringAdmins: [...action.payload] };
    default:
      return state;
  }
}

@Injectable({
  providedIn: 'root'
})
export class AdminStateService {
  private readonly stateSubject = new BehaviorSubject<AdminState>(initialState);

  readonly state$ = this.stateSubject.asObservable();
  readonly dashboardStats$ = this.state$.pipe(map((state) => state.dashboardStats));
  readonly managedUsers$ = this.state$.pipe(map((state) => state.managedUsers));
  readonly reports$ = this.state$.pipe(map((state) => state.reports));
  readonly groups$ = this.state$.pipe(map((state) => state.groups));
  readonly expiringAdmins$ = this.state$.pipe(map((state) => state.expiringAdmins));

  dispatch(action: AdminAction): void {
    const nextState = adminReducer(this.stateSubject.value, action);
    this.stateSubject.next(nextState);
  }

  setDashboardStats(stats: DashboardStats): void {
    this.dispatch({ type: 'SET_DASHBOARD_STATS', payload: stats });
  }

  setManagedUsers(users: any[]): void {
    this.dispatch({ type: 'SET_MANAGED_USERS', payload: users });
  }

  setReports(reports: any[]): void {
    this.dispatch({ type: 'SET_REPORTS', payload: reports });
  }

  setGroups(groups: any[]): void {
    this.dispatch({ type: 'SET_GROUPS', payload: groups });
  }

  setExpiringAdmins(admins: any[]): void {
    this.dispatch({ type: 'SET_EXPIRING_ADMINS', payload: admins });
  }
}
