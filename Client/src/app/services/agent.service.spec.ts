import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AgentService, ReviewVehicleApplicationDTO } from './agent.service';

describe('AgentService', () => {
    let service: AgentService;
    let httpMock: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [AgentService]
        });
        service = TestBed.inject(AgentService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        httpMock.verify();
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    it('should review application', () => {
        const dto: ReviewVehicleApplicationDTO = { Approved: true, InvoiceAmount: 10000 };
        const appId = 1;
        const mockResponse = 'Success';

        service.reviewApplication(appId, dto).subscribe(res => expect(res).toBe(mockResponse));

        const req = httpMock.expectOne(`https://localhost:7257/api/Agent/vehicle-application/${appId}/review`);
        expect(req.request.method).toBe('PUT');
        req.flush(mockResponse);
    });

    it('should fetch pending applications', () => {
        service.getPendingApplications().subscribe();
        const req = httpMock.expectOne('https://localhost:7257/api/Agent/pending-applications');
        expect(req.request.method).toBe('GET');
        req.flush([]);
    });

    it('should fetch customers', () => {
        service.getCustomers().subscribe();
        const req = httpMock.expectOne('https://localhost:7257/api/Agent/customers');
        req.flush([]);
    });

    it('should fetch all applications', () => {
        service.getApplications().subscribe();
        const req = httpMock.expectOne('https://localhost:7257/api/Agent/applications');
        req.flush([]);
    });
});
