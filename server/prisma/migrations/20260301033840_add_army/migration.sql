-- CreateEnum
CREATE TYPE "ArmyStatus" AS ENUM ('idle', 'stationed', 'marching', 'attacking', 'defending', 'defeated');

-- CreateTable
CREATE TABLE "Army" (
    "id" TEXT NOT NULL,
    "gameWorldId" TEXT NOT NULL,
    "routeId" TEXT,
    "fortressId" TEXT,
    "totalUnits" INTEGER NOT NULL,
    "departedAt" TIMESTAMP(3),
    "status" "ArmyStatus" NOT NULL DEFAULT 'idle',

    CONSTRAINT "Army_pkey" PRIMARY KEY ("id")
);

-- AddForeignKey
ALTER TABLE "Army" ADD CONSTRAINT "Army_gameWorldId_fkey" FOREIGN KEY ("gameWorldId") REFERENCES "GameWorld"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Army" ADD CONSTRAINT "Army_routeId_fkey" FOREIGN KEY ("routeId") REFERENCES "Route"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Army" ADD CONSTRAINT "Army_fortressId_fkey" FOREIGN KEY ("fortressId") REFERENCES "Fortress"("id") ON DELETE SET NULL ON UPDATE CASCADE;
