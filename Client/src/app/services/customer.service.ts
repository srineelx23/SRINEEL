import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CalculateQuoteDTO {
    InvoiceAmount: number;
    ManufactureYear: number;
    FuelType: string;
    VehicleType: string;
    KilometersDriven: number;
    PolicyYears: number;
    PlanId: number;
}

export interface RenewPolicyDTO {
    NewPlanId: number;
    SelectedYears: number;
}

@Injectable({
    providedIn: 'root'
})
export class CustomerService {
    private http = inject(HttpClient);
    private backendUrl = 'https://localhost:7257/api/Customer';

    constructor() { }

    // Policies
    getMyPolicies(): Observable<any> {
        return this.http.get(`${this.backendUrl}/my-policies`);
    }

    getPolicy(policyId: number): Observable<any> {
        return this.http.get(`${this.backendUrl}/policy/${policyId}`);
    }

    getPolicyStatus(policyId: number): Observable<any> {
        return this.http.get(`${this.backendUrl}/policy/${policyId}/status`);
    }

    getPolicyPaymentStatus(policyId: number): Observable<any> {
        return this.http.get(`${this.backendUrl}/policy/${policyId}/payment-status`);
    }

    getPolicyYears(policyId: number): Observable<any> {
        return this.http.get(`${this.backendUrl}/policy/${policyId}/years`);
    }

    getAllPolicyPlans(): Observable<any> {
        return this.http.get(`${this.backendUrl}/all-policy-plans`);
    }

    payAnnualPremium(policyId: number): Observable<any> {
        return this.http.post(`${this.backendUrl}/pay-annual/${policyId}`, {}, { responseType: 'text' });
    }

    renewPolicy(policyId: number, dto: RenewPolicyDTO): Observable<any> {
        return this.http.post(`${this.backendUrl}/renew/${policyId}`, dto, { responseType: 'text' });
    }

    cancelPolicy(policyId: number): Observable<any> {
        return this.http.post(`${this.backendUrl}/policy/cancel/${policyId}`, {}, { responseType: 'text' });
    }

    // Applications & Quotes
    calculateQuote(dto: CalculateQuoteDTO): Observable<any> {
        return this.http.post(`${this.backendUrl}/calculate-quote`, dto);
    }

    createVehicleApplication(formData: FormData): Observable<any> {
        return this.http.post(`${this.backendUrl}/vehicle-application`, formData, { responseType: 'text' });
    }

    getMyApplications(): Observable<any> {
        return this.http.get(`${this.backendUrl}/my-applications`);
    }

    // Claims
    getMyClaims(): Observable<any> {
        return this.http.get(`${this.backendUrl}/claims/my`);
    }

    getMyPayments(): Observable<any> {
        return this.http.get(`${this.backendUrl}/payments/my`);
    }

    getClaim(claimId: number): Observable<any> {
        return this.http.get(`${this.backendUrl}/claim/${claimId}`);
    }

    submitClaim(formData: FormData): Observable<any> {
        return this.http.post(`${this.backendUrl}/claim/submit`, formData);
    }
}
