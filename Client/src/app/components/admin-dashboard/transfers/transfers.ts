import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-transfers',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './transfers.html',
  styleUrl: './transfers.css',
})
export class TransfersComponent {
  transfers = input.required<any[]>();
  onDownloadReport = output<number>();
}
