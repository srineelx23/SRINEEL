import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
    providedIn: 'root'
})
export class ClaimsOfficerService {
    private http = inject(HttpClient);
    private apiUrl = 'https://localhost:7257/api/ClaimsOfficer';

    getMyAssignedClaims(): Observable<any> {
        return this.http.get(`${this.apiUrl}/my-claims`);
    }

    getClaimDetails(claimId: number): Observable<any> {
        return this.http.get(`${this.apiUrl}/claim/${claimId}`);
    }

    decideClaim(claimId: number, dto: any, approve: boolean): Observable<any> {
        return this.http.post(`${this.apiUrl}/decide/${claimId}?approve=${approve}`, dto);
    }

    getPayoutBreakdown(claimId: number, dto: any): Observable<any> {
        return this.http.post(`${this.apiUrl}/payout-breakdown/${claimId}`, dto);
    }
}
