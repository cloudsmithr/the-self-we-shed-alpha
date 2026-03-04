import {
  Injectable,
  OnModuleInit,
  OnModuleDestroy,
  Logger,
} from '@nestjs/common';
import { createClient, RedisClientType } from 'redis';

@Injectable()
export class RedisService implements OnModuleInit, OnModuleDestroy {
  private readonly logger = new Logger(RedisService.name);

  private client!: RedisClientType;
  private subscriberClient!: RedisClientType;

  async onModuleInit() {
    this.client = createClient({
      url: process.env.REDIS_URL ?? 'redis://localhost:6379',
    });

    this.subscriberClient = this.client.duplicate() as RedisClientType;

    this.client.on('error', (err: any) =>
      this.logger.error('Redis client error', err),
    );
    this.subscriberClient.on('error', (err: any) =>
      this.logger.error('Redis subscriber error', err),
    );

    await this.client.connect();
    await this.subscriberClient.connect();

    this.logger.log('Redis connected');
  }

  async onModuleDestroy() {
    await this.client?.quit();
    await this.subscriberClient?.quit();
    this.logger.log('Redis disconnected');
  }

  // General purpose ------
  getClient(): RedisClientType {
    return this.client;
  }

  // Hot state reads ------

  async get(key: string): Promise<string | null> {
    return this.client.get(key);
  }

  async set(key: string, value: string): Promise<void> {
    await this.client.set(key, value);
  }

  // Pub / sub ----------
  async subscribe(
    channel: string,
    handler: (message: string) => void,
  ): Promise<void> {
    await this.subscriberClient.subscribe(channel, handler);
  }

  async unsubscribe(channel: string): Promise<void> {
    await this.subscriberClient.unsubscribe(channel);
  }
}
