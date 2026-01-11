import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from './api-client';
import {
  ClaimGroupResponseDto,
  ConfigDto,
  GroupDto,
  SearchResultDto,
  SubmitRequestDto,
} from './types';

@Injectable({
  providedIn: 'root',
})
export class RsvpApi {
  constructor(private readonly api: ApiClient) {}

  getConfig(): Observable<ConfigDto> {
    return this.api.get<ConfigDto>('/config');
  }

  startSession(code: string): Observable<{ ok: boolean; expiresAtUtc: string } | { ok: true }> {
    return this.api.post('/session/start', { code });
  }

  startSessionFromQr(k: string): Observable<{ ok: boolean; expiresAtUtc: string } | { ok: true }> {
    return this.api.get('/session/from-qr', { k });
  }

  search(q: string): Observable<SearchResultDto[]> {
    return this.api.get<SearchResultDto[]>('/search', { q });
  }

  claimGroup(groupId: string): Observable<ClaimGroupResponseDto> {
    return this.api.post<ClaimGroupResponseDto>(`/groups/${encodeURIComponent(groupId)}/claim`);
  }

  getGroup(groupId: string, sessionId: string): Observable<GroupDto> {
    return this.api.get<GroupDto>(`/groups/${encodeURIComponent(groupId)}`, { sessionId });
  }

  submitGroup(groupId: string, sessionId: string, payload: SubmitRequestDto): Observable<{ ok: true } | { ok: boolean }> {
    return this.api.post(`/groups/${encodeURIComponent(groupId)}/submit`, payload, { sessionId });
  }
}
