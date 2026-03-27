import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ChatbotRequest {
    query: string;
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

    ask(query: string): Observable<ChatbotResponse> {
        const payload: ChatbotRequest = { query };
        return this.http.post<ChatbotResponse>(this.backendUrl, payload);
    }
}
