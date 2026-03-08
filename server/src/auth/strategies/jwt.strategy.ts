import { Injectable } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';
import { Request } from 'express';
import { JwtPayload, PlayerFromJwt } from '@/auth/auth.service.js';

function extractJwtFromCookie(req: Request): string | null {
  const cookies = req.cookies as Record<string, string | undefined>;
  return cookies['access_token'] ?? null;
}

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy, 'jwt') {
  constructor() {
    const jwtSecret = process.env['JWT_SECRET'];
    if (!jwtSecret) {
      throw new Error('Missing JWT_SECRET');
    }

    // passport-jwt typings are loose, suppressing warnings
    // eslint-disable-next-line @typescript-eslint/no-unsafe-call
    super({
      // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment,@typescript-eslint/no-unsafe-call,@typescript-eslint/no-unsafe-member-access
      jwtFromRequest: ExtractJwt.fromExtractors([extractJwtFromCookie]),
      ignoreExpiration: false,
      secretOrKey: jwtSecret,
    });
  }
  validate(payload: JwtPayload): PlayerFromJwt {
    return { id: payload.sub };
  }
}
