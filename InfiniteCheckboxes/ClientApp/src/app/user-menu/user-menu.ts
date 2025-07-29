import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CdkConnectedOverlay, CdkOverlayOrigin } from '@angular/cdk/overlay';
import { RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { UserService } from '#userService';
import { setLocalUser } from '#userUtils';
import { Accordion } from './accordion/accordion';
import { AccordionPanel } from './accordion/accordion-panel/accordion-panel';
import { Top10Highscore } from './top10-highscore/top10-highscore';
import { getErrorMessage } from '../../utils/get-error-message';
import { LocalUser } from '../../services/models/local-user';

@Component({
  selector: 'app-user-menu',
  imports: [
    CdkOverlayOrigin,
    CdkConnectedOverlay,
    FormsModule,
    RouterLink,
    Accordion,
    AccordionPanel,
    Top10Highscore
  ],
  templateUrl: './user-menu.html',
  styleUrl: './user-menu.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserMenu implements OnInit, OnDestroy {

  isMenuOpen = false;
  localUser = signal<LocalUser | null>(null);

  private userService = inject(UserService);

  // Keep track of subscriptions to clean up when component destroys.
  private ngUnsubscribe = new Subject<void>();

  ngOnInit(): void {
    this.userService.localUser
      .pipe(takeUntil(this.ngUnsubscribe))
      .subscribe(localUser => this.localUser.set(localUser));
  }

  ngOnDestroy(): void {
    this.ngUnsubscribe.next();
    this.ngUnsubscribe.complete();
  }

  protected whenSaveButtonClick = async (): Promise<void> => {
    const localUser = this.localUser();
    if (!localUser) {
      return;
    }

    setLocalUser(localUser);

    try {
      await this.userService.setUserDetails(localUser);
      location.reload();
    } catch (error: any) {
      alert(getErrorMessage(error));
    }
  }

}
