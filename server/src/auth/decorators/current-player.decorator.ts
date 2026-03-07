import { createParamDecorator, ExecutionContext } from '@nestjs/common';
import { Request } from 'express';
import { PlayerFromJwt } from '../auth.service.js';

export const CurrentPlayer = createParamDecorator(
  (_data: unknown, ctx: ExecutionContext): PlayerFromJwt => {
    const request = ctx.switchToHttp().getRequest<Request>();
    return request.user as PlayerFromJwt;
  },
);
