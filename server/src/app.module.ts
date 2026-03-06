import { Module } from '@nestjs/common';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { CommonModule } from '@/common/common.module';
import { AuthModule } from '@/auth/auth.module';
import { PrismaModule } from '../prisma/prisma.module';
import { PlayerModule } from '@/player/player.module';

@Module({
  imports: [PrismaModule, CommonModule, AuthModule, PlayerModule],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule {}
