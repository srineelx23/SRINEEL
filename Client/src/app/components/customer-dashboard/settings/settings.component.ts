import { Component, Input, Output, EventEmitter, Signal, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.css',
})
export class SettingsComponent {
  @Input() customerName!: Signal<string>;
  @Input() referralCode!: Signal<string>;
  @Input() walletBalance!: Signal<number>;
  @Input() referralHistory!: Signal<any[]>;
  @Input() changePasswordForm!: any;
  @Input() changePwdLoading!: Signal<boolean>;

  @Output() onChangePassword = new EventEmitter<void>();
  @Output() onApplyReferralCode = new EventEmitter<string>();

  showCurrentPwd = signal(false);
  showNewPwd = signal(false);
  showConfirmPwd = signal(false);
  copySuccessMessage = signal('');
  applyReferralCodeInput = '';

  applyReferralCode() {
    const code = this.applyReferralCodeInput.trim();
    if (!code) {
      return;
    }

    this.onApplyReferralCode.emit(code);
    this.applyReferralCodeInput = '';
  }

  async copyReferralCode(): Promise<void> {
    const code = this.referralCode?.();
    if (!code) {
      return;
    }

    try {
      await navigator.clipboard.writeText(code);
    } catch {
      const textarea = document.createElement('textarea');
      textarea.value = code;
      textarea.style.position = 'fixed';
      textarea.style.opacity = '0';
      document.body.appendChild(textarea);
      textarea.focus();
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
    }

    this.copySuccessMessage.set('Referral code copied');
    setTimeout(() => this.copySuccessMessage.set(''), 2000);
  }
}
