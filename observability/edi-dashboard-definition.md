## EDI Operational Dashboard Definitions

- Throughput
  - `sum(rate(edi_processing_ms_count[5m])) by (flow, outcome)`
  - Purpose: processed files per second by flow and outcome.

- Latency (p50/p95/p99)
  - `histogram_quantile(0.50, sum(rate(edi_processing_ms_bucket[5m])) by (le, flow, outcome))`
  - `histogram_quantile(0.95, sum(rate(edi_processing_ms_bucket[5m])) by (le, flow, outcome))`
  - `histogram_quantile(0.99, sum(rate(edi_processing_ms_bucket[5m])) by (le, flow, outcome))`
  - Purpose: end-to-end EDI persistence latency.

- Error Rate
  - `sum(rate(edi_failure_count_total[5m])) by (operation, outcome)`
  - `sum(increase(edi_validation_failure_count_total[15m])) by (rule)`
  - Purpose: hard failures and validation-quality regressions.

- Retry Trend
  - `sum(increase(edi_retry_count_total[15m])) by (operation)`
  - Purpose: early signal of transient dependency instability.

- Queue and Concurrency
  - `max_over_time(edi_queue_depth{queue="inbound"}[5m])`
  - `avg_over_time(edi_queue_wait_ms_sum[5m]) / clamp_min(avg_over_time(edi_queue_wait_ms_count[5m]), 1)`
  - `max_over_time(edi_concurrency_in_use{limiter="inbound"}[5m])`
  - Purpose: saturation visibility and adaptive controller behavior.

- Runtime Health Overlay
  - `rate(process_runtime_dotnet_gc_pause_time_seconds_total[5m])`
  - `process_runtime_dotnet_gc_heap_size_bytes`
  - Purpose: memory pressure correlation with limiter downscale events.

