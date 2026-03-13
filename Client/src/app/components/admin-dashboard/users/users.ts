import { Component, input, output, model, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './users.html',
  styleUrl: './users.css',
})
export class UsersComponent {
  filteredUsers = input.required<any[]>();
  rolesList = input.required<string[]>();
  userRoleFilter = model<string>('All');
  
  isCreatingAgent = input.required<boolean>();
  isCreatingClaimsOfficer = input.required<boolean>();
  registerForm = model.required<any>();

  showRoleDropdown = signal(false);

  onOpenUserForm = output<string>();
  onSubmitUserRegistration = output<void>();
  onSwitchTab = output<string>();

  openUserForm(role: string) {
    this.onOpenUserForm.emit(role);
  }

  submitUserRegistration() {
    this.onSubmitUserRegistration.emit();
  }

  cancelCreation() {
    this.onSwitchTab.emit('users');
  }
}
