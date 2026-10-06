# ADR-0005: Model bookings as non-blocking stays

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** Gustav Gybäck (sole developer)

## Context

The first feature area is vacation coordination for cabins shared within an
extended family. The cabins are large, and family members routinely stay at
the same time. A classic reservation model, where a booking makes the cabin
unavailable to everyone else, does not match how the cabins are used and
would generate conflicts that do not exist in practice.

At the same time, some coordination needs are real:

- People want to see who is planning to be there and when, early, so they
  can plan around each other.
- Sleeping capacity is finite; too many people on the same night is a
  problem that is better spotted in advance.
- Occasionally someone needs the whole cabin, for example when hosting
  guests from outside the family or a large gathering.
- Bed-level or person-level allocation is too rigid: sleeping arrangements
  are decided on arrival and nobody would keep them updated in an app.

The users are family, not paying guests. The system's job is to make plans
visible and surface conflicts, not to arbitrate them. Conversations about
overlaps happen in the stay's comment thread.

## Decision

We will model a booking as a **stay**: a declaration that a person will be
at a facility for a set of nights with a given party size. Stays do not block
each other by default.

Concretely:

- A stay has a facility, an owner (the family member), a first and last
  night, a party size, a status of **tentative** or **confirmed**, an
  optional note, and a comment thread.
- Overlapping stays are normal and are shown together on the calendar,
  tentative ones styled differently from confirmed ones.
- Each facility has a **capacity** in people. When the sum of party sizes on
  any night would exceed it, the API returns a warning and the frontend asks
  for confirmation. The stay is still saved. A per-facility setting can make
  the check a hard refusal later if the family wants it.
- A stay can be marked **exclusive**. An exclusive stay cannot coexist with
  any other stay on the same nights, in either direction. This is the only
  hard block in the model. It is enforced by materialising one row per
  exclusive night in a table with a unique index on facility and night, and
  by checking for existing stays inside the same transaction.
- Room claims (a stay optionally claiming specific rooms, exclusive per
  night via a unique index on room and night) are deferred until the family
  asks for them. The model leaves space for them without requiring them.

## Options considered

### Option 1: Non-blocking stays with capacity warning and exclusive flag

- Pros: Matches actual use; near-zero friction for the common case; the
  only hard invariant is a unique index, which is engine-neutral; tentative
  status captures early plans without commitment.
- Cons: Relies on people reading the calendar; over-capacity nights are
  possible if warnings are ignored; the exclusive flag can be misused
  socially, which the comment thread rather than the system must resolve.

### Option 2: Exclusive reservation per facility

- Pros: Simple mental model; no double bookings.
- Cons: Wrong for large shared cabins; would force sequential visits that
  the family does not want; every overlapping plan becomes a conflict.

### Option 3: Bed- or room-level reservations

- Pros: Precise capacity control.
- Cons: Rigid; sleeping arrangements change on arrival; data goes stale
  quickly; heavy setup per facility. Kept as an optional later extension at
  room level only.

### Option 4: Capacity as a hard constraint from the start

- Pros: Guarantees no over-capacity night.
- Cons: Refuses plans that the family may be happy to accommodate with
  extra mattresses; harder to model as a database constraint, so it would be
  application logic anyway. Retained as an optional per-facility setting.

## Consequences

### Positive

- The booking feature is mostly presentation and coordination, so the first
  vertical slice is small.
- No engine-specific database feature is needed, so the database engine can
  be chosen on cost and hosting alone.
- Tentative stays give the family early visibility, which is most of the
  value.

### Negative

- A soft system is only as good as the calendar's clarity. Overlaps and
  over-capacity nights must be visually obvious.
- The model is explicitly wrong for paying guests. If a facility is ever
  rented out, that use case needs its own ADR.
- Occupancy per night is computed on read, not stored. Fine at this scale;
  revisit if reporting grows.

### Follow-up

- Design the calendar view to show overlapping stays, tentative status and
  capacity warnings clearly.
- Add the exclusive-nights table and its unique index in the first bookings
  migration.
- Decide whether comment threads are a shared component with the upkeep
  feature.

## References

- [ADR-0004: Use a relational database](0004-use-a-relational-database.md)
- [ADR-0008: Use Azure SQL Database](0008-use-azure-sql-database.md)
