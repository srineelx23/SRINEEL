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

    it('should fetch all users', () => {
        const mockUsers = [{ userId: 1, fullName: 'Admin' }];
        service.getAllUsers().subscribe(users => {
            expect(users).toEqual(mockUsers);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Admin/users');
        expect(req.request.method).toBe('GET');
        req.flush(mockUsers);
    });

    it('should create an agent', () => {
        const dto = { FullName: 'Agent 1', Email: 'agent@vims.com', Password: 'Pass', Phone: '123' };
        const mockRes = { userId: 2, fullName: 'Agent 1' };
        service.createAgent(dto).subscribe(res => {
            expect(res).toEqual(mockRes);
        });

        const req = httpMock.expectOne('https://localhost:7257/api/Admin/createAgent');
        expect(req.request.method).toBe('POST');
        expect(req.request.body).toEqual(dto);
        req.flush(mockRes);
    });
});
