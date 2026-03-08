package tick

import (
	"context"
	"log"
	"time"

	"github.com/cloudsmithr/the-self-we-shed-alpha/worker/intent"
	"github.com/cloudsmithr/the-self-we-shed-alpha/worker/publisher"
	"github.com/cloudsmithr/the-self-we-shed-alpha/worker/state"
)

type Loop struct {
	redis    *state.RedisClient
	interval time.Duration
}

func NewLoop(redis *state.RedisClient, interval time.Duration) *Loop {
	return &Loop{redis: redis, interval: interval}
}

func (l *Loop) Run(ctx context.Context) {
	ticker := time.NewTicker(l.interval)
	defer ticker.Stop()

	log.Printf("Tick loop started (interval: %s)", l.interval)

	for {
		select {
		case <-ctx.Done():
			log.Printf("Tick loop stopping: context cancelled")
			return
		case t := <-ticker.C:
			l.tick(ctx, t)
		}
	}
}

func (l *Loop) tick(ctx context.Context, t time.Time) {
	log.Printf("[tick] %s", t.Format(time.RFC3339))

	intents, err := intent.DrainQueue(ctx, l.redis.Client())
	if err != nil {
		log.Printf("[tick] error draining intent queue: %v", err)
		return
	}
	if len(intents) > 0 {
		log.Printf("[tick] drained %d intent(s)", len(intents))
	}

	// TODO: apply intents to game state
	// TODO: unit production
	// TODO: capture resolution
	// TODO: write updated state to Redis

	if err := publisher.PublishTick(ctx, l.redis.Client(), t); err != nil {
		log.Printf("[tick] error publishing tick event: %v", err)
	}
}
