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
        service.getMyPolicies().subscribe();
        const req = httpMock.expectOne('https://localhost:7257/api/Customer/my-policies');
        expect(req.request.method).toBe('GET');
        req.flush([]);
    });

    it('should calculate quote', () => {
        const dto: CalculateQuoteDTO = {
            InvoiceAmount: 1000, ManufactureYear: 2020, FuelType: 'Petrol',
            VehicleType: 'Private', KilometersDriven: 5000, PolicyYears: 1, PlanId: 1
        };
        service.calculateQuote(dto).subscribe();
        const req = httpMock.expectOne('https://localhost:7257/api/Customer/calculate-quote');
        expect(req.request.method).toBe('POST');
        req.flush({});
    });

    it('should submit claim', () => {
        const formData = new FormData();
        formData.append('PolicyId', '1');
        service.submitClaim(formData).subscribe();
        const req = httpMock.expectOne('https://localhost:7257/api/Customer/claim/submit');
        expect(req.request.method).toBe('POST');
        req.flush({});
    });

    it('should initiate transfer', () => {
        const policyId = 1;
        const email = 'test@test.com';
        service.initiateTransfer(policyId, email).subscribe();
        const req = httpMock.expectOne('https://localhost:7257/api/Customer/transfer/initiate');
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual({ policyId, recipientEmail: email });
        req.flush({});
    });

    it('should accept transfer', () => {
        const transferId = 1;
        const file = new File([''], 'rc.pdf');
        service.acceptTransfer(transferId, file).subscribe();
        const req = httpMock.expectOne(`https://localhost:7257/api/Customer/transfer/${transferId}/accept`);
        expect(req.request.method).toBe('POST');
        req.flush({});
    });

    it('should download invoice', () => {
        const paymentId = 1;
        service.downloadInvoice(paymentId).subscribe(res => {
            expect(res instanceof Blob).toBeTrue();
        });
        const req = httpMock.expectOne(`https://localhost:7257/api/Customer/invoice/download/${paymentId}`);
        req.flush(new Blob());
    });
});
