import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private api = "http://localhost:5072/api/User";

  constructor(private http: HttpClient) {}

  register(data:any):Observable<any>{
    return this.http.post(`${this.api}/register`, data);
  }

  login(data:any):Observable<any>{
    return this.http.post(`${this.api}/login`, data);
  }

}