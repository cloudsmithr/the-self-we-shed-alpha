ALTER TABLE "Army" ADD CONSTRAINT "army_location_exclusive"
    CHECK (
        ("fortressId" IS NULL AND "routeId" IS NOT NULL) OR
        ("fortressId" IS NOT NULL AND "routeId" IS NULL) OR
        ("fortressId" IS NULL AND "routeId" IS NULL)
        );