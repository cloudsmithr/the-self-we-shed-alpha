-- CreateTable
CREATE TABLE "Route" (
    "id" TEXT NOT NULL,
    "gameWorldId" TEXT NOT NULL,
    "sourceFortressId" TEXT NOT NULL,
    "targetFortressId" TEXT NOT NULL,
    "travelTimeSeconds" INTEGER NOT NULL,

    CONSTRAINT "Route_pkey" PRIMARY KEY ("id")
);

-- AddForeignKey
ALTER TABLE "Route" ADD CONSTRAINT "Route_gameWorldId_fkey" FOREIGN KEY ("gameWorldId") REFERENCES "GameWorld"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Route" ADD CONSTRAINT "Route_sourceFortressId_fkey" FOREIGN KEY ("sourceFortressId") REFERENCES "Fortress"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Route" ADD CONSTRAINT "Route_targetFortressId_fkey" FOREIGN KEY ("targetFortressId") REFERENCES "Fortress"("id") ON DELETE RESTRICT ON UPDATE CASCADE;
