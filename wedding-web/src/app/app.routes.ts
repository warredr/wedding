import { Routes } from '@angular/router';
import { WelcomeScreenComponent } from './features/session/screens/welcome/welcome-screen.component';
import { SearchScreenComponent } from './features/search/screens/search/search-screen.component';
import { DoneScreenComponent } from './features/rsvp/screens/done/done-screen.component';
import { RsvpOverviewScreenComponent } from './features/rsvp/screens/rsvp-overview/rsvp-overview-screen.component';
import { RsvpWizardScreenComponent } from './features/rsvp/screens/rsvp-wizard/rsvp-wizard-screen.component';
import { unsavedChangesGuard } from './shared/guards/unsaved-changes.guard';
import { deadlineGuard } from './shared/guards/deadline.guard';

export const routes: Routes = [
	{ path: '', pathMatch: 'full', component: WelcomeScreenComponent },

	{ path: 'search', component: SearchScreenComponent, canActivate: [deadlineGuard] },
	{
		path: 'rsvp/:groupId',
		component: RsvpWizardScreenComponent,
		canDeactivate: [unsavedChangesGuard],
		canActivate: [deadlineGuard]
	},
	{
		path: 'rsvp/:groupId/overview',
		component: RsvpOverviewScreenComponent,
		canDeactivate: [unsavedChangesGuard],
		canActivate: [deadlineGuard]
	},
	{ path: 'done', component: DoneScreenComponent, canActivate: [deadlineGuard] },
	{ path: '**', redirectTo: '' },
];
