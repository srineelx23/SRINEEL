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
    private referralUrl = 'https://localhost:7257/api/Referral';

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

    downloadInvoice(paymentId: number): Observable<Blob> {
        return this.http.get(`${this.backendUrl}/invoice/download/${paymentId}`, { responseType: 'blob' });
    }

    downloadClaimReport(claimId: number): Observable<Blob> {
        return this.http.get(`${this.backendUrl}/claim/download/${claimId}`, { responseType: 'blob' });
    }

    getClaim(claimId: number): Observable<any> {

        return this.http.get(`${this.backendUrl}/claim/${claimId}`);
    }

    submitClaim(formData: FormData): Observable<any> {
        return this.http.post(`${this.backendUrl}/claim/submit`, formData);
    }

    // Policy Transfer
    initiateTransfer(policyId: number, recipientEmail: string): Observable<any> {
        return this.http.post(`${this.backendUrl}/transfer/initiate`, { policyId, recipientEmail });
    }

    getIncomingTransfers(): Observable<any[]> {
        return this.http.get<any[]>(`${this.backendUrl}/transfer/incoming`);
    }

    getOutgoingTransfers(): Observable<any[]> {
        return this.http.get<any[]>(`${this.backendUrl}/transfer/outgoing`);
    }

    acceptTransfer(transferId: number, rcFile: File): Observable<any> {
        const fd = new FormData();
        fd.append('rcDocument', rcFile);
        return this.http.post(`${this.backendUrl}/transfer/${transferId}/accept`, fd);
    }

    rejectTransfer(transferId: number): Observable<any> {
        return this.http.post(`${this.backendUrl}/transfer/${transferId}/reject`, {});
    }

    downloadPolicyContract(policyId: number): Observable<Blob> {
        return this.http.get(`${this.backendUrl}/policy/download/${policyId}`, { responseType: 'blob' });
    }

    requestRoadsideAssistance(data: any): Observable<any> {
        return this.http.post(`${this.backendUrl}/roadside-assistance`, data);
    }

    applyReferralCode(referralCode: string): Observable<any> {
        return this.http.post(`${this.referralUrl}/apply`, { referralCode });
    }

    getReferralHistory(): Observable<any[]> {
        return this.http.get<any[]>(`${this.referralUrl}/history`);
    }

    getWalletBalance(): Observable<{ balance: number }> {
        return this.http.get<{ balance: number }>(`${this.referralUrl}/wallet/balance`);
    }
}
