package intent

import (
	"context"
	"encoding/json"
	"errors"
	"log"
	"time"

	"github.com/redis/go-redis/v9"
)

const QueueKey = "intents:pending"

type PlayerIntent struct {
	PlayerID       string    `json:"playerId"`
	ActionType     string    `json:"actionType"`
	FortressID     string    `json:"fortressId"`
	BuildingTypeID string    `json:"buildingTypeId"`
	IdempotencyKey string    `json:"idempotencyKey"`
	EnqueuedAt     time.Time `json:"enqueuedAt"`
}

var drainQueueScript = redis.NewScript(`
local items = redis.call('LRANGE', KEYS[1], 0, -1)
redis.call('DEL', KEYS[1])
return items
`)

func DrainQueue(ctx context.Context, rdb *redis.Client) ([]PlayerIntent, error) {
	raw, err := drainQueueScript.Run(ctx, rdb, []string{QueueKey}).StringSlice()
	if errors.Is(err, redis.Nil) || len(raw) == 0 {
		return []PlayerIntent{}, nil
	}
	if err != nil {
		return nil, err
	}

	intents := make([]PlayerIntent, 0, len(raw))
	for _, s := range raw {
		var intent PlayerIntent
		if err := json.Unmarshal([]byte(s), &intent); err != nil {
			log.Printf("[intent] skipping malformed payload: %v", err)
			continue
		}
		intents = append(intents, intent)
	}

	return intents, nil
}
