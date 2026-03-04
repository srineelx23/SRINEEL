import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ClaimsOfficerService } from './claims-officer.service';

describe('ClaimsOfficerService', () => {
    let service: ClaimsOfficerService;
    let httpMock: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [ClaimsOfficerService]
        });
        service = TestBed.inject(ClaimsOfficerService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        httpMock.verify();
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    it('should fetch assigned claims', () => {
        const mockClaims = [{ id: 1, status: 'Submitted' }];

        service.getMyAssignedClaims().subscribe(claims => {
            expect(claims).toEqual(mockClaims);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/ClaimsOfficer/my-claims');
        expect(req.request.method).toBe('GET');
        req.flush(mockClaims);
    });

    it('should fetch claim details', () => {
        const mockClaim = { id: 1, status: 'Submitted' };
        const claimId = 1;

        service.getClaimDetails(claimId).subscribe(claim => {
            expect(claim).toEqual(mockClaim);
        });

        const req = httpMock.expectOne(`https://localhost:7257/api/ClaimsOfficer/claim/${claimId}`);
        expect(req.request.method).toBe('GET');
        req.flush(mockClaim);
    });

    it('should submit claim decision', () => {
        const claimId = 1;
        const dto = { remarks: 'Approved' };
        const approve = true;
        const mockResponse = { message: 'Success' };

        service.decideClaim(claimId, dto, approve).subscribe(res => {
            expect(res).toEqual(mockResponse);
        });

        const req = httpMock.expectOne(`https://localhost:7257/api/ClaimsOfficer/decide/${claimId}?approve=${approve}`);
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual(dto);
        req.flush(mockResponse);
    });
});
