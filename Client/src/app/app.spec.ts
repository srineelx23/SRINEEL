import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { RouterTestingModule } from '@angular/router/testing';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App, RouterTestingModule],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it(`should have signal title as 'Client'`, () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    // Access protected member via casting if necessary or check signal value
    expect((app as any).title()).toEqual('Client');
  });
});
