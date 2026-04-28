## EDI Adaptive Runtime Tuning

### Tunable parameters

- `EdiAdaptiveLimiter:ControlLoopIntervalMs`
  - Lower values react faster but cost more control churn.
  - Recommended production starting range: `1500-3000`.

- `EdiAdaptiveLimiter:ScaleUpStep`
  - Recommended: `1`.
  - Increase only after proving backlog clears too slowly under healthy load.

- `EdiAdaptiveLimiter:ScaleDownStep`
  - Recommended: `1-2`.
  - Keep >= scale-up to prioritize memory/CPU protection.

- `EdiAdaptiveLimiter:CooldownTicks`
  - Recommended: `1-3`.
  - Prevents oscillation by requiring idle ticks before next target adjustment.

- `EdiOperational:ProcessingMetricSampleRate`
  - Recommended: `0.1-0.25` in production.
  - Keep full (`1.0`) only during diagnostics windows.

### Safeguards

- Lower bound (`MinConcurrency`) prevents full pipeline starvation.
- Upper bound (`MaxConcurrency`) caps runaway parallelism.
- Cooldown + step-based changes prevent abrupt permit swings.
- Queue depth + wait-time alerts detect slow reaction or over-throttling.

### Validation workflow (live-like)

1. Sustained load (steady 10-15 min) and monitor:
   - queue depth trend
   - p95 processing latency
   - heap growth/GC pause
2. Burst load (2-3x steady volume for 2-5 min) and verify:
   - target concurrency increases gradually
   - no unbounded queue growth
   - system recovers to baseline after burst

