import { Module } from '@nestjs/common';
import { PlayerController } from './player.controller.js';

@Module({
  controllers: [PlayerController],
})
export class PlayerModule {}
