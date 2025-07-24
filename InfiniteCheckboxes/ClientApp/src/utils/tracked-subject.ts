import { Subject, Subscription } from 'rxjs';

export function createTrackedSubject<T>(factory: () => Subject<T>, subscribed: () => void, unsubscribed: () => void): Subject<T> {
  const subject = factory();
  const originalSubscribe = subject.subscribe.bind(subject);

  subject.subscribe = (...args: any[]): Subscription => {
    subscribed();
    const subscription = originalSubscribe(...args);

    const originalUnsubscribe = subscription.unsubscribe.bind(subscription);
    subscription.unsubscribe = () => {
      unsubscribed();
      originalUnsubscribe();
    };

    return subscription;
  };

  return subject;
}
