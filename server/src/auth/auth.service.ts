import { Injectable } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { PrismaService } from '../../prisma/prisma.service.js';

export interface JwtPayload {
  sub: string; // player's GUID
}

export interface PlayerFromJwt {
  id: string;
}

// --- Callsign generator ---

const WORDS_A = ['Shadow', 'Iron', 'Ghost', 'Storm', 'Ember'];
const WORDS_B = ['Fox', 'Wolf', 'Hawk', 'Bear', 'Lynx'];
const SUFFIX_CHARS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ123456789';

function randomItem<T>(arr: T[]): T {
  return arr[Math.floor(Math.random() * arr.length)];
}

function generateCallsign(): string {
  const a = randomItem(WORDS_A);
  const b = randomItem(WORDS_B);
  const suffix = Array.from(
    { length: 2 },
    () => SUFFIX_CHARS[Math.floor(Math.random() * SUFFIX_CHARS.length)],
  ).join('');
  return `${a}${b}${suffix}`;
}

// ---

@Injectable()
export class AuthService {
  constructor(
    private readonly prisma: PrismaService,
    private readonly jwtService: JwtService,
  ) {}

  private async generateUniqueCallsign(): Promise<string> {
    let callsign: string;
    let attempts = 0;

    do {
      callsign = generateCallsign();
      const existing = await this.prisma.player.findFirst({
        where: { displayName: callsign },
        select: { id: true },
      });

      if (!existing) return callsign;
      attempts++;
    } while (attempts < 10);

    // fallback, should never happen but this keeps signup from failing
    return `${callsign}${Date.now().toString(36).slice(-3)}`;
  }

  async findOrCreatePlayer(params: {
    oauthProvider: string;
    oauthId: string;
  }): Promise<PlayerFromJwt> {
    {
      const { oauthProvider, oauthId } = params;

      const existing = await this.prisma.player.findUnique({
        where: { oauthProvider_oauthId: { oauthProvider, oauthId } },
        select: { id: true, displayName: true },
      });

      if (existing) return existing;
      const displayName = await this.generateUniqueCallsign();
      const player = await this.prisma.player.create({
        data: { oauthProvider, oauthId, displayName },
        select: { id: true, displayName: true },
      });

      return player;
    }
  }

  signJwt(player: PlayerFromJwt): string {
    const payload: JwtPayload = {
      sub: player.id,
    };
    return this.jwtService.sign(payload);
  }
}
