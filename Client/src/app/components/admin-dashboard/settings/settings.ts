import { Component, input, output, model, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.html',
  styleUrl: './settings.css',
})
export class SettingsComponent {
  adminName = input.required<string>();
  userRole = input.required<string>();
  changePasswordForm = model.required<any>();
  changePwdLoading = input.required<boolean>();
  
  showCurrentPwd = signal(false);
  showNewPwd = signal(false);
  showConfirmPwd = signal(false);

  onChangePassword = output<void>();

  changePassword() {
    this.onChangePassword.emit();
  }
}
