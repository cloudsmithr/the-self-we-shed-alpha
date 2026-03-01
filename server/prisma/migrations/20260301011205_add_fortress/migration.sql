-- CreateEnum
CREATE TYPE "GameWorldStatus" AS ENUM ('active', 'completed', 'paused', 'removed');

-- CreateEnum
CREATE TYPE "FortressOwner" AS ENUM ('human', 'enemy');

-- CreateTable
CREATE TABLE "GameWorld" (
    "id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "status" "GameWorldStatus" NOT NULL DEFAULT 'active',
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "completedAt" TIMESTAMP(3),

    CONSTRAINT "GameWorld_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Fortress" (
    "id" TEXT NOT NULL,
    "gameWorldId" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "positionX" DOUBLE PRECISION NOT NULL,
    "positionY" DOUBLE PRECISION NOT NULL,
    "owner" "FortressOwner" NOT NULL DEFAULT 'enemy',
    "baseEnemyGarrison" INTEGER NOT NULL,
    "difficultyTier" INTEGER NOT NULL,
    "isFinalObjective" BOOLEAN NOT NULL DEFAULT false,

    CONSTRAINT "Fortress_pkey" PRIMARY KEY ("id")
);

-- AddForeignKey
ALTER TABLE "Fortress" ADD CONSTRAINT "Fortress_gameWorldId_fkey" FOREIGN KEY ("gameWorldId") REFERENCES "GameWorld"("id") ON DELETE RESTRICT ON UPDATE CASCADE;
