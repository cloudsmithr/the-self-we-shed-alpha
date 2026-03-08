package main

import (
	"context"
	"log"
	"os"
	"os/signal"
	"strconv"
	"syscall"
	"time"

	"github.com/cloudsmithr/the-self-we-shed-alpha/worker/state"
	"github.com/cloudsmithr/the-self-we-shed-alpha/worker/tick"
)

func main() {
	log.Println("Worker starting...")

	redisAddr := envOrDefault("REDIS_ADDR", "localhost:6379")
	tickInterval := envDurationOrDefault("TICK_INTERVAL_SECONDS", 5*time.Second)

	redisClient := state.NewRedisClient(redisAddr)
	defer func() {
		if err := redisClient.Close(); err != nil {
			log.Printf("error closing Redis client: %v", err)
		}
	}()

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	if err := redisClient.Ping(ctx); err != nil {
		log.Fatalf("failed to connect to Redis: %v", err)
	}
	log.Printf("Connected to Redis at %s", redisAddr)
	log.Printf("Tick interval: %s", tickInterval)

	loop := tick.NewLoop(redisClient, tickInterval)
	loop.Run(ctx)

	log.Println("Worker stopped")
}

func envOrDefault(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func envDurationOrDefault(key string, fallback time.Duration) time.Duration {
	v := os.Getenv(key)
	if v == "" {
		return fallback
	}

	seconds, err := strconv.Atoi(v)
	if err != nil || seconds <= 0 {
		log.Printf("invalid %s=%q, using default %s", key, v, fallback)
		return fallback
	}

	return time.Duration(seconds) * time.Second
}
