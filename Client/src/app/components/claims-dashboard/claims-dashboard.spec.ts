import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ClaimsDashboard } from './claims-dashboard';

describe('ClaimsDashboard', () => {
    let component: ClaimsDashboard;
    let fixture: ComponentFixture<ClaimsDashboard>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [ClaimsDashboard]
        }).compileComponents();

        fixture = TestBed.createComponent(ClaimsDashboard);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });
});
