import { Component, input, signal, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './settings.html',
  styleUrl: './settings.css'
})
export class Settings {
  agentName = input.required<string>();
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
