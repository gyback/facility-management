# ADR-0013: Share comment threads across features

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0005 gives every stay a comment thread, because overlaps and exclusive
stays are resolved by family members talking to each other, not by the
system. It left open whether that thread is specific to bookings or a
component other features share.

ADR-0003 describes the upkeep feature as work logs, recurring maintenance,
planned renovations, and costs split across several owners, with balances
that must be auditable "when relatives disagree about money". Disagreements
like that need somewhere to happen next to the thing being discussed: a
proposed renovation, an expense, a skipped maintenance task. Instructions
content may benefit from the same, though less obviously.

Forces:

- **Same shape everywhere.** A comment is an author, a body and timestamps,
  whatever it is attached to. Rules such as who may edit or delete a comment
  should not differ between features without a reason.
- **Referential integrity.** The data lives in a relational database
  (ADR-0004) accessed through EF Core (ADR-0009). A comment should not be
  able to point at something that no longer exists.
- **Authorisation belongs to the owner.** Whether someone may read or post
  in a thread depends on whether they may see the stay, expense or task it
  belongs to. The rules for that live in the owning feature.
- **Layering.** ADR-0003 organises the Application layer by feature and
  warns against speculative abstractions. A shared component must be small
  and concrete, not a generic framework.
- **Dispatch.** ADR-0011 makes each use case an explicit command or query
  with its own handler. Handlers do not call other handlers.
- **Only one consumer today.** Bookings is the first feature to be built.
  Upkeep does not exist yet, so the shared design is validated by one real
  use until upkeep arrives.

## Decision

We will model comment threads as one shared component, owned by no single
feature, and attach it to other entities through a foreign key from the
owner to the thread.

- **Domain.** A `Comments` folder in the Domain project holds a
  `CommentThread` aggregate root and its `Comment` entities. A comment has an
  author, a plain-text body, the time it was posted and, if edited, the time
  it was last edited. All rules about comments (body length, who may edit or
  delete, ordering) live in the aggregate and therefore apply identically
  everywhere.
- **The thread does not know its owner.** `CommentThread` has no owner type
  or owner id. Instead, each commentable entity (starting with `Stay`) has a
  required `CommentThreadId` that references it. A unique index on that
  column in each owner table keeps the relationship one-to-one.
- **Lifecycle.** The owner creates its thread when it is created, and
  deletes it when it is deleted, in the same unit of work (ADR-0011's
  transaction decorator). Because the foreign key points from owner to
  thread, the database cannot cascade the delete; the owner's delete handler
  removes the thread explicitly.
- **Use cases stay in the owning feature.** There are no comment endpoints of
  their own. Bookings exposes, for example, `PostStayComment` and
  `GetStayComments`. Their handlers load the stay, check access, then load
  the `CommentThread` by the stay's `CommentThreadId` and call its methods.
  The shared part is the aggregate, its persistence mapping, and a small
  read helper in an Application `Comments` folder that projects a thread
  into a common response shape.
- **Opt-in per entity.** Any feature may make an entity commentable by
  adding a `CommentThreadId`. Nothing forces it to.

## Options considered

### Option 1: Shared thread aggregate, foreign key from owner (chosen)

- Pros: Comment rules are written once and behave the same in every feature;
  real foreign keys, so a thread cannot reference a missing owner and an
  owner cannot reference a missing thread; authorisation stays with the
  owning feature; no type discriminator column; adding comments to a new
  entity is one column, one mapping and two thin handlers.
- Cons: The thread cannot tell which entity it belongs to, so cross-feature
  views ("latest comments everywhere") or notifications need the owner to
  supply that context; the owner must delete its thread explicitly; each
  feature still writes its own thin comment handlers and endpoints.

### Option 2: Shared comments with a polymorphic target

A single `Comments` table with `TargetType` and `TargetId` columns.

- Pros: Attaching comments to anything needs no schema change on the owner;
  cross-feature queries are a single table scan; the comment knows what it is
  about.
- Cons: No foreign key is possible, so orphaned comments and dangling
  targets are prevented only by application code; the type discriminator
  couples the shared component to the names of every owner type; deleting an
  owner does not remove its comments unless every feature remembers to.

### Option 3: Separate comment entities per feature

`StayComment` now, and `ExpenseComment`, `WorkLogComment` and so on later.

- Pros: Each feature is fully independent; each can evolve its comments
  differently; simplest for the first slice.
- Cons: The same entity, mapping, validation and rules are duplicated per
  feature and drift apart over time; any change to how comments work (edit
  rules, mentions, formatting) is made several times; the UI component that
  renders threads gets several slightly different APIs.

### Option 4: Build stay comments now, extract when upkeep needs them

- Pros: No shared design before there is a second consumer; avoids guessing
  wrong about upkeep's needs.
- Cons: The extraction is a data migration across live tables, which is the
  expensive part to defer; the upkeep need is already clear from ADR-0003;
  the shared design here is small enough that the cost of having it early is
  low.

## Consequences

### Positive

- Comments look and behave the same wherever they appear, and the frontend
  can use one thread component against one response shape.
- Integrity is enforced by the database, not by convention.
- The comment model is a small aggregate with no dependencies, unit-testable
  like the rest of the Domain project.
- Upkeep, and later any other feature, gets discussion threads with almost
  no new code.

### Negative

- The Domain and Application projects gain a folder that is not a feature
  in the user's sense. It must stay small; anything feature-specific belongs
  in the owning feature.
- A thread does not know its owner. Notifications, activity feeds or search
  across threads need that context from somewhere, for example the owning
  feature raising an event that carries it.
- Owner delete handlers must remember to delete the thread. A test per
  commentable entity should check that no thread is orphaned.
- The shared design is shaped by one consumer until upkeep is built. If
  upkeep turns out to need something the thread cannot give (structured
  replies, attachments, resolving a discussion), that is a change to the
  shared aggregate and affects bookings too.

### Follow-up

- Add the `CommentThread` aggregate, its EF Core mapping and the shared
  read helper, and use them for stays as part of the booking feature
  ([#8](https://github.com/gyback/facility-management/issues/8)).
- Decide the comment rules that are not settled here: body length, editing,
  deletion (soft or hard), and whether deleted comments leave a placeholder
  ([#32](https://github.com/gyback/facility-management/issues/32)).
- Notifications about new comments depend on how domain events are
  dispatched ([#27](https://github.com/gyback/facility-management/issues/27)).

## References

- Issue [#5: Decide whether comment threads are shared between bookings and upkeep](https://github.com/gyback/facility-management/issues/5)
- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0004: Use a relational database](0004-use-a-relational-database.md)
- [ADR-0005: Model bookings as non-blocking stays](0005-model-bookings-as-non-blocking-stays.md)
- [ADR-0009: Use Entity Framework Core for data access](0009-use-entity-framework-core-for-data-access.md)
- [ADR-0011: Use hand-rolled command and query handlers](0011-use-hand-rolled-command-and-query-handlers.md)
