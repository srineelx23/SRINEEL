import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { CustomerService, CalculateQuoteDTO, RenewPolicyDTO } from './customer.service';

describe('CustomerService', () => {
    let service: CustomerService;
    let httpMock: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [CustomerService]
        });
        service = TestBed.inject(CustomerService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        httpMock.verify();
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    it('should fetch my policies', () => {
        const mockData = [{ id: 1, policyNumber: 'P-123' }];
        service.getMyPolicies().subscribe(res => {
            expect(res).toEqual(mockData);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Customer/my-policies');
        expect(req.request.method).toBe('GET');
        req.flush(mockData);
    });

    it('should calculate quote', () => {
        const dto: CalculateQuoteDTO = {
            InvoiceAmount: 1000000,
            ManufactureYear: 2023,
            FuelType: 'Petrol',
            VehicleType: 'Car',
            KilometersDriven: 5000,
            PolicyYears: 1,
            PlanId: 1
        };
        const mockRes = { premium: 20000 };

        service.calculateQuote(dto).subscribe(res => {
            expect(res).toEqual(mockRes);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Customer/calculate-quote');
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual(dto);
        req.flush(mockRes);
    });

    it('should cancel policy', () => {
        const policyId = 123;
        service.cancelPolicy(policyId).subscribe(res => {
            expect(res).toBe('Cancelled');
        });

        const req = httpMock.expectOne(`https://localhost:7257/api/Customer/policy/cancel/${policyId}`);
        expect(req.request.method).toBe('POST');
        req.flush('Cancelled');
    });
});
