import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'limit',
  pure: true
})
export class LimitPipe implements PipeTransform {
  transform<T>(arr: T[], limit: number): T[] {
    if (limit === 0) {
      return arr;
    }

    // Returns the original array with a modified Iterator
    return {
      [Symbol.iterator]: () => {
        let count = 0;
        const iterator = arr[Symbol.iterator]();

        return {
          next: () => {
            if (count >= limit) {
              return { done: true };
            }

            count++;
            return iterator.next();
          }
        };
      }
    } as any;
  }
}
