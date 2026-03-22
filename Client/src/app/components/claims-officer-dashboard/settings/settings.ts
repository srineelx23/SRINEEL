import { Component, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.html',
  styleUrl: './settings.css'
})
export class SettingsComponent {
  officerName = input.required<string>();
  userRole = input.required<string>();
  changePwdLoading = input.required<boolean>();
  
  changePasswordForm = {
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  };

  showCurrentPwd = false;
  showNewPwd = false;
  showConfirmPwd = false;

  onChangePassword = output<any>();

  submitChangePassword() {
    this.onChangePassword.emit(this.changePasswordForm);
  }
}
