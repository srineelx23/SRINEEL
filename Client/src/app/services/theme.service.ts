import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  isDarkMode = signal(true);

  constructor() {
    const storedTheme = localStorage.getItem('theme');
    if (storedTheme === 'light') {
      this.isDarkMode.set(false);
      document.documentElement.setAttribute('data-theme', 'light');
    } else {
      this.isDarkMode.set(true);
      document.documentElement.setAttribute('data-theme', 'dark');
    }
  }

  toggleTheme() {
    const newMode = !this.isDarkMode();
    this.isDarkMode.set(newMode);
    const newTheme = newMode ? 'dark' : 'light';

    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('theme', newTheme);
  }
}
