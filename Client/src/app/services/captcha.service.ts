import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class CaptchaService {
  private currentCaptcha: string = '';

  generateCaptcha(): string {
    const chars = '0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ';
    let result = '';
    for (let i = 0; i < 6; i++) {
      result += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    this.currentCaptcha = result;
    return result;
  }

  validateCaptcha(userInput: string): boolean {
    return userInput === this.currentCaptcha;
  }

  getCaptchaText(): string {
    return this.currentCaptcha;
  }
}
