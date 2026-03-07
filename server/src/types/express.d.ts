import type { PlayerFromJwt } from '@/auth/auth.service.js';

declare global {
  namespace Express {
    interface Request {
      user?: PlayerFromJwt;
    }
  }
}

export {};
