import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home.component';
import { DesignComponent } from './shared/components/design.component/design.component';
import { authGuard } from './shared/guards/auth.guard';

const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'design', component: DesignComponent },
  { path: 'mittartskart', canActivate: [authGuard], loadComponent: () => import('./pages/mitt-artskart/mitt-artskart.component').then(m => m.MittArtskartComponent) },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
