import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AdminChatRequest {
  question: string;
  history?: string[];
  sessionId?: string;
}

export interface AdminChatResponse {
  answer: string;
  reasoning: string;
  rulesApplied: string[];
  confidence: 'HIGH' | 'MEDIUM' | 'LOW' | string;
}

@Injectable({
  providedIn: 'root'
})
export class AdminChatService {
  private readonly http = inject(HttpClient);
  private readonly backendUrl = 'https://localhost:7257/api/admin/chat';

  ask(question: string, history: string[] = [], sessionId?: string): Observable<AdminChatResponse> {
    const payload: AdminChatRequest = { question, history, sessionId };
    return this.http.post<AdminChatResponse>(this.backendUrl, payload);
  }
}
