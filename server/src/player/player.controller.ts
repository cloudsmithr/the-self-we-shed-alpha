import { Controller, Get, UseGuards } from '@nestjs/common';
import { Player } from '../generated/prisma/client.js';
import { CurrentPlayer } from '../auth/decorators/current-player.decorator.js';
import { JwtAuthGuard } from '../auth/guards/jwt-auth.guard.js';
import { PrismaService } from '../../prisma/prisma.service.js';
import type { PlayerFromJwt } from '@/auth/auth.service.js';

@Controller('player')
@UseGuards(JwtAuthGuard)
export class PlayerController {
  constructor(private readonly prisma: PrismaService) {}

  @Get('me')
  async getMe(@CurrentPlayer() player: PlayerFromJwt): Promise<Player> {
    return this.prisma.player.findUniqueOrThrow({
      where: { id: player.id },
    });
  }
}
