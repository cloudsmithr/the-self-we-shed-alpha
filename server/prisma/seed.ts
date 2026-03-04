import { PrismaClient } from '../generated/prisma/client';
import { PrismaPg } from '@prisma/adapter-pg';
import { Pool } from 'pg';
import { SYSTEM_PLAYER_ID } from '@shared/types/constants';

if (!process.env.DATABASE_URL) {
  throw new Error('DATABASE_URL is not defined');
}

const pool = new Pool({
  connectionString: process.env.DATABASE_URL,
});

const adapter = new PrismaPg(pool);
const prisma = new PrismaClient({ adapter });

async function main() {
  await prisma.player.upsert({
    where: { id: SYSTEM_PLAYER_ID },
    update: {},
    create: {
      id: SYSTEM_PLAYER_ID,
      displayName: 'System',
      oauthProvider: 'System',
      oauthId: 'system',
    },
  });

  console.log('seeded system player successfully');
}

main()
  .catch((err) => {
    console.error(err);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
