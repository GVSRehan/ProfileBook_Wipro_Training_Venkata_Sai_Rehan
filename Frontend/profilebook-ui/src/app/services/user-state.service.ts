import { Injectable } from '@angular/core';
import { BehaviorSubject, map } from 'rxjs';
import { Post } from '../models/post';

interface UserState {
  posts: Post[];
  notifications: any[];
}

type UserAction =
  | { type: 'SET_POSTS'; payload: Post[] }
  | { type: 'SET_NOTIFICATIONS'; payload: any[] };

const initialState: UserState = {
  posts: [],
  notifications: []
};

function userReducer(state: UserState, action: UserAction): UserState {
  switch (action.type) {
    case 'SET_POSTS':
      return { ...state, posts: [...action.payload] };
    case 'SET_NOTIFICATIONS':
      return { ...state, notifications: [...action.payload] };
    default:
      return state;
  }
}

@Injectable({
  providedIn: 'root'
})
export class UserStateService {
  private readonly stateSubject = new BehaviorSubject<UserState>(initialState);

  readonly state$ = this.stateSubject.asObservable();
  readonly posts$ = this.state$.pipe(map((state) => state.posts));
  readonly notifications$ = this.state$.pipe(map((state) => state.notifications));

  dispatch(action: UserAction): void {
    const nextState = userReducer(this.stateSubject.value, action);
    this.stateSubject.next(nextState);
  }

  setPosts(posts: Post[]): void {
    this.dispatch({ type: 'SET_POSTS', payload: posts });
  }

  setNotifications(notifications: any[]): void {
    this.dispatch({ type: 'SET_NOTIFICATIONS', payload: notifications });
  }
}
