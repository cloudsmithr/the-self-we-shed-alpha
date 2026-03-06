import { Controller, Get, UseGuards } from '@nestjs/common';
import { Player } from '@prisma/client';
import { CurrentPlayer } from '@/auth/decorators/current-player.decorator';
import { JwtAuthGuard } from '@/auth/guards/jwt-auth.guard';
import { PrismaService } from '../../prisma/prisma.service';
import type { PlayerFromJwt } from '@/auth/auth.service';

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
