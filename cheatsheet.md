# The Self We Shed Dev Cheatsheet

## Repo structure

- `server/` — NestJS backend
- `shared/` — shared TypeScript package used by backend/frontend
- `.env` — currently lives one directory above `server`

---

## Fresh setup

1. Create the root `.env`
2. Run `npm install` from repo root
3. Ensure PostgreSQL is running
4. Run Prisma generate/migrations from `server`
5. Start backend with `npm run start:dev`

---

## Module system

This project uses **ESM**, not CommonJS.

Important rules:

- Local backend imports use **relative paths**
- Relative imports in TS source use the **`.js` runtime extension**
- Do **not** use `@/` aliases for local backend files at runtime
- Shared code is imported from the real package: `@tsws/shared`

Examples:

```ts
import { AppService } from './app.service.js';
import { SYSTEM_PLAYER_ID } from '@tsws/shared';
```

---

## Prisma stuff

### seed:
npx prisma db seed

### prereq for seeding:
npm install @prisma/adapter-pg

npm install --save-dev @types/pg

npm install -D tsx

npx prisma init

npx prisma generate

---

## migrations:
### reset DB and apply all migrations:
 npx prisma migrate reset

### apply all existing migrations:
npx prisma migrate deploy

### apply new migration:
npx prisma migrate dev --name <migration_name>