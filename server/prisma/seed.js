"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const client_1 = require("../generated/prisma/client");
const adapter_pg_1 = require("@prisma/adapter-pg");
const pg_1 = require("pg");
const constants_1 = require("../../shared/types/constants");
if (!process.env.DATABASE_URL) {
    throw new Error('DATABASE_URL is not defined');
}
const pool = new pg_1.Pool({
    connectionString: process.env.DATABASE_URL,
});
const adapter = new adapter_pg_1.PrismaPg(pool);
const prisma = new client_1.PrismaClient({ adapter });
async function main() {
    await prisma.player.upsert({
        where: { id: constants_1.SYSTEM_PLAYER_ID },
        update: {},
        create: {
            id: constants_1.SYSTEM_PLAYER_ID,
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
//# sourceMappingURL=seed.js.map