import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { AdminService } from './admin.service';

describe('AdminService', () => {
    let service: AdminService;
    let httpMock: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            imports: [HttpClientTestingModule],
            providers: [AdminService]
        });
        service = TestBed.inject(AdminService);
        httpMock = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        httpMock.verify();
    });

    it('should be created', () => {
        expect(service).toBeTruthy();
    });

    it('should create an agent', () => {
        const dto = { name: 'Agent Smith' };
        service.createAgent(dto).subscribe(res => expect(res).toBeTruthy());
        const req = httpMock.expectOne('https://localhost:7257/api/Admin/createAgent');
        expect(req.request.method).toBe('POST');
        req.flush({});
    });

    it('should create a claims officer', () => {
        const dto = { name: 'Officer Jones' };
        service.createClaimsOfficer(dto).subscribe(res => expect(res).toBeTruthy());
        const req = httpMock.expectOne('https://localhost:7257/api/Admin/createClaimsOfficer');
        expect(req.request.method).toBe('POST');
        req.flush({});
    });

    it('should create a policy plan', () => {
        const plan = { name: 'Gold Plan' };
        service.createPolicyPlan(plan).subscribe(res => expect(res).toBeTruthy());
        const req = httpMock.expectOne('https://localhost:7257/api/Admin/createPolicyPlan');
        expect(req.request.method).toBe('POST');
        req.flush({});
    });

    it('should fetch all policy plans', () => {
        const plans = [{ id: 1, name: 'Plan A' }];
        service.getAllPolicyPlans().subscribe(res => expect(res).toEqual(plans));
        const req = httpMock.expectOne('https://localhost:7257/api/Admin/policy-plans');
        expect(req.request.method).toBe('GET');
        req.flush(plans);
    });

    it('should fetch all users', () => {
        const users = [{ id: 1, name: 'User' }];
        service.getAllUsers().subscribe(res => expect(res).toEqual(users));
        const req = httpMock.expectOne('https://localhost:7257/api/Admin/users');
        req.flush(users);
    });

    it('should fetch audit logs', () => {
        const logs = [{ action: 'login' }];
        service.getAuditLogs().subscribe(res => expect(res).toEqual(logs));
        const req = httpMock.expectOne('https://localhost:7257/api/Admin/audit-logs');
        req.flush(logs);
    });

    it('should deactivate a plan', () => {
        service.deactivatePlan(1).subscribe(res => expect(res).toBeTruthy());
        const req = httpMock.expectOne('https://localhost:7257/api/Admin/deactivate/1');
        expect(req.request.method).toBe('PUT');
        req.flush({});
    });

    it('should activate a plan', () => {
        service.activatePlan(1).subscribe(res => expect(res).toBeTruthy());
        const req = httpMock.expectOne('https://localhost:7257/api/Admin/activate/1');
        expect(req.request.method).toBe('PUT');
        req.flush({});
    });
});
