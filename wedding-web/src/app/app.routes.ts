import { Routes } from '@angular/router';
import { BootComponent } from './features/session/screens/boot/boot.component';
import { LockedScreenComponent } from './features/session/screens/locked/locked-screen.component';
import { WelcomeScreenComponent } from './features/session/screens/welcome/welcome-screen.component';
import { SearchScreenComponent } from './features/search/screens/search/search-screen.component';
import { DoneScreenComponent } from './features/rsvp/screens/done/done-screen.component';
import { RsvpOverviewScreenComponent } from './features/rsvp/screens/rsvp-overview/rsvp-overview-screen.component';
import { RsvpWizardScreenComponent } from './features/rsvp/screens/rsvp-wizard/rsvp-wizard-screen.component';
import { unsavedChangesGuard } from './shared/guards/unsaved-changes.guard';

export const routes: Routes = [
	{ path: '', pathMatch: 'full', component: BootComponent },
	{ path: 'qr', component: BootComponent },
	{ path: 'qr/:k', component: BootComponent },
	{ path: 'locked', component: LockedScreenComponent },
	{ path: 'welcome', component: WelcomeScreenComponent },
	{ path: 'search', component: SearchScreenComponent },
	{ path: 'rsvp/:groupId', component: RsvpWizardScreenComponent, canDeactivate: [unsavedChangesGuard] },
	{ path: 'rsvp/:groupId/overview', component: RsvpOverviewScreenComponent, canDeactivate: [unsavedChangesGuard] },
	{ path: 'done', component: DoneScreenComponent },
	{ path: '**', redirectTo: '' },
];
