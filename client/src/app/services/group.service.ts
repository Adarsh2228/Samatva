import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  GroupDto, CreateGroupRequest, UpdateGroupRequest, AddMemberRequest,
  GuestLinkResponse, GroupMemberDto
} from '../models/interfaces';

@Injectable({ providedIn: 'root' })
export class GroupService {
  private readonly API = `${environment.apiUrl}/groups`;

  constructor(private http: HttpClient) {}

  getMyGroups(): Observable<GroupDto[]> {
    return this.http.get<GroupDto[]>(this.API);
  }

  getGroup(id: string): Observable<GroupDto> {
    return this.http.get<GroupDto>(`${this.API}/${id}`);
  }

  createGroup(req: CreateGroupRequest): Observable<GroupDto> {
    return this.http.post<GroupDto>(this.API, req);
  }

  updateGroup(id: string, req: UpdateGroupRequest): Observable<GroupDto> {
    return this.http.put<GroupDto>(`${this.API}/${id}`, req);
  }

  deleteGroup(id: string): Observable<any> {
    return this.http.delete(`${this.API}/${id}`);
  }

  addMember(groupId: string, req: AddMemberRequest): Observable<GroupMemberDto> {
    return this.http.post<GroupMemberDto>(`${this.API}/${groupId}/members`, req);
  }

  removeMember(groupId: string, userId: string): Observable<any> {
    return this.http.delete(`${this.API}/${groupId}/members/${userId}`);
  }

  generateGuestLink(groupId: string): Observable<GuestLinkResponse> {
    return this.http.post<GuestLinkResponse>(`${this.API}/${groupId}/guest-link`, {});
  }

  getGroupByGuestLink(token: string): Observable<GroupDto> {
    return this.http.get<GroupDto>(`${this.API}/guest`, { params: { token } });
  }

  generateInviteLink(groupId: string): Observable<any> {
    return this.http.post<any>(`${this.API}/${groupId}/invite-link`, {});
  }

  joinViaToken(token: string): Observable<any> {
    return this.http.post<any>(`${this.API}/join`, { token });
  }

  generateInviteCode(groupId: string): Observable<any> {
    return this.http.post<any>(`${this.API}/${groupId}/invite-code`, {});
  }

  joinViaCode(code: string): Observable<any> {
    return this.http.post<any>(`${this.API}/join-code`, { code });
  }
}
