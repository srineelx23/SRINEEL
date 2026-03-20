import { Component, OnInit, inject, signal, AfterViewChecked, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../../services/admin.service';
import * as L from 'leaflet';

@Component({
  selector: 'app-garages',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './garages.html',
  styleUrl: './garages.css'
})
export class GaragesComponent implements OnInit, AfterViewChecked, OnDestroy {
  private adminService = inject(AdminService);
  
  garages = signal<any[]>([]);
  isLoading = signal(false);
  showModal = signal(false);
  isEditMode = signal(false);
  
  private map: L.Map | null = null;
  private marker: L.Marker | null = null;
  private modalStateChanged = false;
  private searchTimeout: any;
  addressSearch = '';
  suggestions = signal<any[]>([]);

  private customIcon = L.icon({
    iconUrl: 'https://unpkg.com/leaflet@1.7.1/dist/images/marker-icon.png',
    iconRetinaUrl: 'https://unpkg.com/leaflet@1.7.1/dist/images/marker-icon-2x.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.7.1/dist/images/marker-shadow.png',
    iconSize: [25, 41],
    iconAnchor: [12, 41],
    popupAnchor: [1, -34],
    shadowSize: [41, 41]
  });

  currentGarage = signal<any>({
    garageId: 0,
    garageName: '',
    latitude: 20.5937, // Default center for India or anywhere
    longitude: 78.9629,
    phoneNumber: ''
  });

  ngOnInit() {
    this.loadGarages();
  }

  ngAfterViewChecked() {
    // Need to initialize map after view is stable when modal appears
    if (this.showModal() && this.modalStateChanged) {
       this.initMap();
       this.modalStateChanged = false;
    }
  }

  ngOnDestroy() {
    if (this.map) {
      this.map.remove();
    }
  }

  onSearchKeyUp() {
    const query = this.addressSearch.trim();
    if (query.length < 3) {
      this.suggestions.set([]);
      return;
    }

    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }

    this.searchTimeout = setTimeout(() => {
      // Using Photon API: Faster and better for Indian Landmark/Apartment names
      // Bias search towards current map location
      const bias = `&lat=${this.currentGarage().latitude}&lon=${this.currentGarage().longitude}`;
      
      fetch(`https://photon.komoot.io/api/?q=${encodeURIComponent(query)}&limit=5${bias}`)
        .then(res => res.json())
        .then(data => {
          if (data && data.features) {
            const formatted = data.features.map((f: any) => ({
              display_name: [
                f.properties.name,
                f.properties.street,
                f.properties.district,
                f.properties.city,
                f.properties.state
              ].filter(Boolean).join(', '),
              lat: f.geometry.coordinates[1],
              lon: f.geometry.coordinates[0]
            }));
            this.suggestions.set(formatted);
          }
        })
        .catch(err => console.error('Suggestion error:', err));
    }, 300);
  }

  selectSuggestion(suggestion: any, event?: Event) {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    const lat = parseFloat(suggestion.lat);
    const lon = parseFloat(suggestion.lon);
    
    // Zoom in highly to the selected landmark/apartment
    this.updateLocation(lat, lon, 18); 
    this.addressSearch = suggestion.display_name;
    this.suggestions.set([]);
  }

  searchAddress() {
    if (!this.addressSearch.trim()) return;

    fetch(`https://photon.komoot.io/api/?q=${encodeURIComponent(this.addressSearch)}&limit=1`)
      .then(res => res.json())
      .then(data => {
        if (data && data.features && data.features.length > 0) {
          const f = data.features[0];
          const lat = f.geometry.coordinates[1];
          const lon = f.geometry.coordinates[0];
          this.updateLocation(lat, lon, 16);
        }
        this.suggestions.set([]);
      })
      .catch(err => console.error('Geocoding error:', err));
  }

  initMap() {
    // If map already exists (e.g. from a previous modal open), destroy it first
    if (this.map) {
      this.map.remove();
      this.map = null;
    }

    const initialLat = this.currentGarage().latitude || 20.5937;
    const initialLng = this.currentGarage().longitude || 78.9629;

    this.map = L.map('garage-map').setView([initialLat, initialLng], 5);

    // UPGRADED: Google Maps Roadmap for high-detail Indian roads and landmarks
    L.tileLayer('https://{s}.google.com/vt/lyrs=m&x={x}&y={y}&z={z}', {
      maxZoom: 20,
      subdomains: ['mt0', 'mt1', 'mt2', 'mt3'],
      attribution: '&copy; Google Maps'
    }).addTo(this.map);

    // Initial marker if coordinates are present
    this.marker = L.marker([initialLat, initialLng], { icon: this.customIcon }).addTo(this.map);

    this.map.on('click', (e: L.LeafletMouseEvent) => {
      const { lat, lng } = e.latlng;
      this.updateLocation(lat, lng);
    });

    // Handle map resize on modal open
    setTimeout(() => {
      if (this.map) this.map.invalidateSize();
    }, 200);
  }

  updateLocation(lat: number, lng: number, zoom?: number) {
    // Precision limit for coordinates
    const fixedLat = parseFloat(lat.toFixed(6));
    const fixedLng = parseFloat(lng.toFixed(6));

    this.currentGarage.update(prev => ({
      ...prev,
      latitude: fixedLat,
      longitude: fixedLng
    }));

    if (this.marker) {
      this.marker.setLatLng([fixedLat, fixedLng]);
    }
    
    if (this.map) {
      if (zoom) {
        this.map.setView([fixedLat, fixedLng], zoom);
      } else {
        this.map.panTo([fixedLat, fixedLng]);
      }
    }
  }

  loadGarages() {
    this.isLoading.set(true);
    this.adminService.getAllGarages().subscribe({
      next: (res) => {
        this.garages.set(res);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  openAddModal() {
    this.isEditMode.set(false);
    this.currentGarage.set({
      garageId: 0,
      garageName: '',
      latitude: 20.5937,
      longitude: 78.9629,
      phoneNumber: ''
    });
    this.addressSearch = '';
    this.modalStateChanged = true;
    this.showModal.set(true);
  }

  openEditModal(garage: any) {
    this.isEditMode.set(true);
    this.currentGarage.set({ ...garage });
    this.addressSearch = '';
    this.modalStateChanged = true;
    this.showModal.set(true);
  }

  closeModal() {
    this.showModal.set(false);
    this.suggestions.set([]);
    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }
    if (this.map) {
      this.map.remove();
      this.map = null;
    }
  }

  saveGarage() {
    const garageData = this.currentGarage();
    if (this.isEditMode()) {
      this.adminService.updateGarage(garageData.garageId, garageData).subscribe({
        next: () => {
          this.loadGarages();
          this.closeModal();
        }
      });
    } else {
      this.adminService.addGarage(garageData).subscribe({
        next: () => {
          this.loadGarages();
          this.closeModal();
        }
      });
    }
  }

  deleteGarage(id: number) {
    if (confirm('Are you sure you want to delete this garage?')) {
      this.adminService.deleteGarage(id).subscribe({
        next: () => this.loadGarages()
      });
    }
  }
}
