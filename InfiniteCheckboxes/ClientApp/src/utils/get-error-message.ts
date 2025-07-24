import { HttpErrorResponse } from '@angular/common/http';

export function getErrorMessage(error: any): string {
  if (error instanceof HttpErrorResponse) {
    if (error.error?.title) {
      return error.error.title;
    }
  }

  return error.message ?? 'Something went wrong';
}
