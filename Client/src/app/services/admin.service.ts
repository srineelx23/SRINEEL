import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
    providedIn: 'root'
})
export class AdminService {
    private http = inject(HttpClient);
    private apiUrl = 'https://localhost:7257/api/Admin';

    createAgent(dto: any): Observable<any> {
        return this.http.post(`${this.apiUrl}/createAgent`, dto);
    }

    createClaimsOfficer(dto: any): Observable<any> {
        return this.http.post(`${this.apiUrl}/createClaimsOfficer`, dto);
    }

    createPolicyPlan(plan: any): Observable<any> {
        return this.http.post(`${this.apiUrl}/createPolicyPlan`, plan);
    }

    getAllPolicyPlans(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/policy-plans`);
    }

    getPolicyPlanById(id: number): Observable<any> {
        return this.http.get<any>(`${this.apiUrl}/policy-plan/${id}`);
    }

    getAllUsers(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/users`);
    }

    getAllClaims(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/claims`);
    }

    getAllPayments(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/payments`);
    }

    getAllPolicies(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/policies`);
    }

    getAllVehicleApplications(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/vehicle-applications`);
    }

    deactivatePlan(id: number): Observable<any> {
        return this.http.put(`${this.apiUrl}/deactivate/${id}`, {});
    }

    activatePlan(id: number): Observable<any> {
        return this.http.put(`${this.apiUrl}/activate/${id}`, {});
    }

    getAuditLogs(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/audit-logs`);
    }

    getAllTransfers(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/transfers`);
    }

    downloadClaimReport(claimId: number): Observable<Blob> {
        return this.http.get(`${this.apiUrl}/claim/download/${claimId}`, { responseType: 'blob' });
    }

    downloadInvoice(paymentId: number): Observable<Blob> {
        return this.http.get(`${this.apiUrl}/invoice/download/${paymentId}`, { responseType: 'blob' });
    }

    downloadTransferReport(transferId: number): Observable<Blob> {
        return this.http.get(`${this.apiUrl}/transfer/download/${transferId}`, { responseType: 'blob' });
    }
    
    downloadPolicy(policyId: number): Observable<Blob> {
        return this.http.get(`${this.apiUrl}/policy/download/${policyId}`, { responseType: 'blob' });
    }

    getAllGarages(): Observable<any[]> {
        return this.http.get<any[]>(`${this.apiUrl}/garages`);
    }

    getGarageById(id: number): Observable<any> {
        return this.http.get<any>(`${this.apiUrl}/garage/${id}`);
    }

    addGarage(garage: any): Observable<any> {
        return this.http.post(`${this.apiUrl}/garage`, garage);
    }

    updateGarage(id: number, garage: any): Observable<any> {
        return this.http.put(`${this.apiUrl}/garage/${id}`, garage);
    }

    deleteGarage(id: number): Observable<any> {
        return this.http.delete(`${this.apiUrl}/garage/${id}`);
    }
}
