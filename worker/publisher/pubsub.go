package publisher

import (
	"context"
	"encoding/json"
	"time"

	"github.com/redis/go-redis/v9"
)

const TickChannel = "game:tick"

type TickEvent struct {
	Timestamp time.Time `json:"timestamp"`
}

func PublishTick(ctx context.Context, rdb *redis.Client, t time.Time) error {
	event := TickEvent{
		Timestamp: t,
	}
	payload, err := json.Marshal(event)
	if err != nil {
		return err
	}
	return rdb.Publish(ctx, TickChannel, payload).Err()
}
