import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ReviewVehicleApplicationDTO {
    Approved: boolean;
    RejectionReason?: string;
    InvoiceAmount: number;
}

export interface AgentApplicationValidationResultDTO {
    riskScore: number;
    errors: string[];
}

@Injectable({
    providedIn: 'root'
})
export class AgentService {
    private http = inject(HttpClient);
    private backendUrl = 'https://localhost:7257/api/Agent';

    constructor() { }

    reviewApplication(applicationId: number, dto: ReviewVehicleApplicationDTO): Observable<any> {
        return this.http.put(`${this.backendUrl}/vehicle-application/${applicationId}/review`, dto, { responseType: 'text' });
    }

    getPendingApplications(): Observable<any> {
        return this.http.get(`${this.backendUrl}/pending-applications`);
    }

    getCustomers(): Observable<any> {
        return this.http.get(`${this.backendUrl}/customers`);
    }

    getApplications(): Observable<any> {
        return this.http.get(`${this.backendUrl}/applications`);
    }

    validateApplicationDocuments(applicationId: number): Observable<AgentApplicationValidationResultDTO> {
        return this.http.get<AgentApplicationValidationResultDTO>(`${this.backendUrl}/vehicle-application/${applicationId}/validation`);
    }
}
