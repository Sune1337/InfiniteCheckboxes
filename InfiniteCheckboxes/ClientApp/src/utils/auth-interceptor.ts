import { HttpInterceptorFn } from '@angular/common/http';
import { getLocalUser } from '#userUtils';

export const authInterceptor: HttpInterceptorFn = (req, next) => {

  const isSameOrigin = req.url.startsWith('/') || new URL(req.url).origin === window.location.origin;
  if (!isSameOrigin) {
    return next(req);
  }

  const localUser = getLocalUser();
  const token = localUser.userId;
  if (token) {
    const authReq = req.clone({
      headers: req.headers.set('Authorization', `Bearer ${token}`)
    });

    return next(authReq);
  }

  return next(req);
};
