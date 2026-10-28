import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { DesignComponent } from './shared/components/design.component/design.component';

export const routes: Routes = [
  { path: '', component: HomeComponent, title: 'pageTitle.home' },
  { path: 'design', component: DesignComponent },
  {
    path: 'mittartskart',
    title: 'pageTitle.mittArtskart',
    loadComponent: () => import('./pages/mitt-artskart/mitt-artskart.component').then(m => m.MittArtskartComponent)
  },
];
