import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfirmModalHostComponent } from './shared/components/confirm-modal-host/confirm-modal-host.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ConfirmModalHostComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  title = 'wedding';
}
