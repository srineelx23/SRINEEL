import { Component, input, output, signal, model } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-vehicles',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './vehicles.html',
  styleUrl: './vehicles.css',
})
export class VehiclesComponent {
  filteredVehicles = input.required<any[]>();
  vehiclePremiumsMap = input.required<Map<number, number>>();
  vehicleClaimsMap = input.required<Map<number, number>>();
  
  selectedVehicle = input.required<any | null>();
  vehiclePolicies = input.required<any[]>();
  vehicleTransactions = input.required<any[]>();
  vehicleClaims = input.required<any[]>();
  vehicleDocuments = input.required<any[]>();

  vehicleSearchFilter = model<string>('');
  vehicleStatusFilter = model<string>('All');
  showStatusDropdown = signal(false);

  onViewVehicleDetails = output<any>();
  onBackToVehicles = output<void>();
  onDownloadReport = output<number>();
  onDownloadInvoice = output<number>();

  viewVehicleDetails(v: any) {
    this.onViewVehicleDetails.emit(v);
  }

  backToVehicles() {
    this.onBackToVehicles.emit();
  }
}
