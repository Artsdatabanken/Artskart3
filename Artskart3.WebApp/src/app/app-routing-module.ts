import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MapComponent } from './shared/components/map.component/map.component';
import { DesignComponent } from './shared/components/design.component/design.component';

const routes: Routes = [
  { path: '', component: MapComponent },
  { path: 'design', component: DesignComponent },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
