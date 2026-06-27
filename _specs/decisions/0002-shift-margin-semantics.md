# Decision 0002 - Shift Margin Semantics

Status: Accepted
Date: 2026-06-26

## Context

`Shift.GrossMargin` could mean either hourly spread (`BillRate - PayRate`) or total shift margin (`(BillRate - PayRate) * BillableHours`). The current Domain code and tests already implement total shift margin.

## Decision

Keep `Shift.GrossMargin` as total shift gross margin:

```csharp
GrossMargin = (BillRate - PayRate) * BillableHours
```

`GrossMarginPercentage` remains the total margin divided by total shift revenue. Since hours cancel out, this is equivalent to the hourly rate spread percentage when `BillableHours > 0`:

```csharp
GrossMarginPercentage = BillRate > 0 && BillableHours > 0
    ? (GrossMargin / (BillRate * BillableHours)) * 100
    : 0
```

If future dashboard/contracts work needs the hourly spread as a separate metric, it must use an explicit name such as `HourlyGrossMargin`:

```csharp
HourlyGrossMargin = BillRate - PayRate
```

## Consequences

- Domain code and tests remain aligned with current behavior.
- UI/API labels must display `GrossMargin` as total shift gross margin, not hourly spread.
- Billing and margin dashboards must use `HourlyGrossMargin` only when they intentionally need per-hour spread.