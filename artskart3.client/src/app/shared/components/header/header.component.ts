import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export interface MenuItem {
  label: string;
  href: string;
  ariaLabel?: string;
}

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './header.component.html',
  styleUrls: ['./header.component.css']
})
export class HeaderComponent {
  @Input() projectName: string = 'Artskart';
  @Input() menuItems: MenuItem[] = [
    { label: 'Menypunkt 1', href: '#', ariaLabel: 'Go to Menypunkt 1' },
    { label: 'Menypunkt 2', href: '#', ariaLabel: 'Go to Menypunkt 2' },
    { label: 'Menypunkt 3', href: '#', ariaLabel: 'Go to Menypunkt 3' }
  ];

  isMenuOpen = false;

  toggleBurgerMenu(): void {
    this.isMenuOpen = !this.isMenuOpen;
  }

  closeBurgerMenu(): void {
    this.isMenuOpen = false;
  }

  onMenuItemClick(): void {
    if (this.isMenuOpen) {
      this.closeBurgerMenu();
    }
  }
}

