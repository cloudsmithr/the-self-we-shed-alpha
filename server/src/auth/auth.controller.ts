import {
  Controller,
  Get,
  Post,
  Req,
  Res,
  UnauthorizedException,
  UseGuards,
} from '@nestjs/common';
import type { Request, Response } from 'express';
import { AuthService } from './auth.service.js';
import { GoogleAuthGuard } from './guards/google-auth.guard.js';

const COOKIE_MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000;

@Controller('auth')
export class AuthController {
  constructor(private readonly authService: AuthService) {}

  @Get('login/google')
  @UseGuards(GoogleAuthGuard)
  googleLogin(): void {
    return;
  }

  @Get('callback/google')
  @UseGuards(GoogleAuthGuard)
  googleCallback(@Req() req: Request, @Res() res: Response): void {
    if (!req.user) {
      throw new UnauthorizedException();
    }

    const isProduction = process.env['NODE_ENV'] === 'production';
    const player = req.user;
    const token = this.authService.signJwt(player);

    res.cookie('access_token', token, {
      httpOnly: true,
      secure: isProduction,
      sameSite: 'lax',
      maxAge: COOKIE_MAX_AGE_MS,
    });

    const redirectionUrl =
      process.env['FRONTEND_URL'] ?? 'http://localhost:5173';

    res.redirect(redirectionUrl);
  }

  @Get('logout')
  logout(@Res() res: Response): void {
    res.clearCookie('access_token', {
      httpOnly: true,
      secure: process.env['NODE_ENV'] === 'production',
      sameSite: 'lax',
    });
    res.json({ message: 'Logged out' });
  }
}
