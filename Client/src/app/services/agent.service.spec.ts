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

    it('should fetch pending applications', () => {
        const mockApps = [{ id: 1, registrationNumber: 'KA01-1234' }];
        service.getPendingApplications().subscribe(apps => {
            expect(apps).toEqual(mockApps);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Agent/pending-applications');
        expect(req.request.method).toBe('GET');
        req.flush(mockApps);
    });

    it('should review application', () => {
        const applicationId = 101;
        const dto: ReviewVehicleApplicationDTO = {
            Approved: true,
            InvoiceAmount: 1000000
        };
        service.reviewApplication(applicationId, dto).subscribe(res => {
            expect(res).toBe('Success');
        });

        const req = httpMock.expectOne(`https://localhost:7257/api/Agent/vehicle-application/${applicationId}/review`);
        expect(req.request.method).toBe('PUT');
        expect(req.request.body).toEqual(dto);
        req.flush('Success');
    });
});
