import { Injectable } from '@nestjs/common';
import { PassportStrategy } from '@nestjs/passport';
import { ExtractJwt, Strategy } from 'passport-jwt';
import { Request } from 'express';
import { JwtPayload, PlayerFromJwt } from '@/auth/auth.service';

function extractJwtFromCookie(req: Request): string | null {
  const cookies = req.cookies as Record<string, string | undefined>;
  return cookies['access_token'] ?? null;
}

@Injectable()
export class JwtStrategy extends PassportStrategy(Strategy, 'jwt') {
  constructor() {
    super({
      jwtFromRequest: ExtractJwt.fromExtractors([
        extractJwtFromCookie,
        ExtractJwt.fromAuthHeaderAsBearerToken(),
      ]),
      ignoreExpiration: false,
      secretOrKey: process.env['JWT_SECRET']!,
    });
  }
  validate(payload: JwtPayload): PlayerFromJwt {
    return { id: payload.sub, displayName: payload.displayName };
  }
}
