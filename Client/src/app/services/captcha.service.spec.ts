import { TestBed } from '@angular/core/testing';
import { CaptchaService } from './captcha.service';

describe('CaptchaService', () => {
  let service: CaptchaService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(CaptchaService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should generate a 6-character captcha', () => {
    const captcha = service.generateCaptcha();
    expect(captcha.length).toBe(6);
  });

  it('should validate correct captcha', () => {
    const captcha = service.generateCaptcha();
    expect(service.validateCaptcha(captcha)).toBeTrue();
  });

  it('should invalidate incorrect captcha', () => {
    service.generateCaptcha();
    expect(service.validateCaptcha('wrong')).toBeFalse();
  });

  it('should return current captcha text', () => {
    const captcha = service.generateCaptcha();
    expect(service.getCaptchaText()).toBe(captcha);
  });
});
