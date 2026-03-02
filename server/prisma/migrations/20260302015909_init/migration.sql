-- CreateTable
CREATE TABLE "players" (
    "id" TEXT NOT NULL,
    "display_name" TEXT NOT NULL,
    "oauth_provider" TEXT NOT NULL,
    "oauth_id" TEXT NOT NULL,
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "last_active_at" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "players_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "game_worlds" (
    "id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "status" TEXT NOT NULL DEFAULT 'active',
    "created_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "completed_at" TIMESTAMP(3),
    "config" JSONB NOT NULL,

    CONSTRAINT "game_worlds_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "fortresses" (
    "id" TEXT NOT NULL,
    "game_world_id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "position_x" DOUBLE PRECISION NOT NULL,
    "position_y" DOUBLE PRECISION NOT NULL,
    "owner" TEXT NOT NULL DEFAULT 'enemy',
    "parent_id" TEXT,
    "capture_threshold" INTEGER NOT NULL,
    "difficulty_tier" INTEGER NOT NULL,
    "captured_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "fortresses_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "building_types" (
    "id" TEXT NOT NULL,
    "game_world_id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "production_rate" DOUBLE PRECISION NOT NULL,
    "unlock_tier" INTEGER NOT NULL,
    "description" TEXT NOT NULL,

    CONSTRAINT "building_types_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "buildings" (
    "id" TEXT NOT NULL,
    "fortress_id" TEXT NOT NULL,
    "building_type_id" TEXT NOT NULL,
    "built_by" TEXT NOT NULL,
    "built_at" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "unit_count" INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT "buildings_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "players_oauth_provider_oauth_id_key" ON "players"("oauth_provider", "oauth_id");

-- AddForeignKey
ALTER TABLE "fortresses" ADD CONSTRAINT "fortresses_game_world_id_fkey" FOREIGN KEY ("game_world_id") REFERENCES "game_worlds"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "fortresses" ADD CONSTRAINT "fortresses_parent_id_fkey" FOREIGN KEY ("parent_id") REFERENCES "fortresses"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "building_types" ADD CONSTRAINT "building_types_game_world_id_fkey" FOREIGN KEY ("game_world_id") REFERENCES "game_worlds"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "buildings" ADD CONSTRAINT "buildings_fortress_id_fkey" FOREIGN KEY ("fortress_id") REFERENCES "fortresses"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "buildings" ADD CONSTRAINT "buildings_building_type_id_fkey" FOREIGN KEY ("building_type_id") REFERENCES "building_types"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "buildings" ADD CONSTRAINT "buildings_built_by_fkey" FOREIGN KEY ("built_by") REFERENCES "players"("id") ON DELETE RESTRICT ON UPDATE CASCADE;
