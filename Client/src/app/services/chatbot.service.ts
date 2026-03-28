import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ChatbotRequest {
    query: string;
    history?: ChatbotHistoryItem[];
}

export interface ChatbotHistoryItem {
    role: 'user' | 'assistant';
    content: string;
}

export interface ChatbotResponse {
    response: string;
}

@Injectable({
    providedIn: 'root'
})
export class ChatbotService {
    private http = inject(HttpClient);
    private readonly backendUrl = 'https://localhost:7257/api/chat';

    ask(query: string, history: ChatbotHistoryItem[] = []): Observable<ChatbotResponse> {
        const payload: ChatbotRequest = { query, history };
        return this.http.post<ChatbotResponse>(this.backendUrl, payload);
    }
}
