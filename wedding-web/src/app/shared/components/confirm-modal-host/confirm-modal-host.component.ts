import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { ConfirmModalService } from '../../services/confirm-modal.service';

@Component({
  selector: 'app-confirm-modal-host',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './confirm-modal-host.component.html',
})
export class ConfirmModalHostComponent {
  constructor(readonly confirmModal: ConfirmModalService) {}
}
