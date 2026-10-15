# ADR-0016: Keep the history of edited and deleted comments

- **Status:** Accepted
- **Date:** 2026-10-15
- **Deciders:** Gustav Gybäck (sole developer)

## Context

ADR-0013 made comment threads a shared `CommentThread` aggregate. It puts
every rule about comments in that aggregate so the rules apply the same way
in every feature. It also fixed the shape of a comment: an author, a
plain-text body, the time it was posted and, if edited, the time it was last
edited. It left the rules themselves open: how long a body may be, who may
edit a comment and how, and what deleting a comment does.

ADR-0013 also decided that an owner deletes its thread when the owner is
deleted, and treated that as removing the thread and its comments. It did
not consider whether a thread is a record worth keeping after its owner is
gone.

Forces:

- **Threads will carry disputes about money.** Threads are where relatives
  sort out overlapping stays (ADR-0005). In upkeep, they will be where
  relatives discuss expenses and costs split between owners, and ADR-0003
  requires balances that stay auditable "when relatives disagree about
  money". A comment such as "I agreed to pay half" matters in that
  argument. If it can be reworded, removed, or deleted together with the
  expense afterwards, the discussion next to an expense cannot be relied
  on.
- **People make mistakes and change their minds.** Typos, a wrong date, or
  something said in irritation. The users are family, and most of them are
  not technical. They expect to fix or take back what they wrote, as in any
  chat app.
- **Building history in later loses it.** Edits and deletions made before
  history is recorded are gone for good. The booking feature is the first
  user of threads, so the rules it ships with are the rules upkeep
  inherits.
- **Authorisation follows ownership.** ADR-0015 gives every permission
  inside a facility to a facility role, checked in Domain. Which roles exist
  and what they allow is the facility's business. The thread does not know
  its owner (ADR-0013), so it cannot look up a role itself, and it can only
  be reached through its owner.
- **Aggregates are loaded whole.** Commands load the whole thread through
  its repository (ADR-0014). Queries project the thread into one shared
  response shape through a shared read helper (ADR-0013).
- **Storage.** The database is Azure SQL (ADR-0008). Strings longer than
  4,000 UTF-16 code units need `nvarchar(max)`, which is stored differently
  and cannot be indexed.
- **Solo developer.** Every mechanism must pay for itself. History should
  not need a second storage model or an audit framework.

## Decision

We will never overwrite or destroy what someone wrote in a comment. Editing
keeps every earlier version as a linked row in the same table, and every
member can read that history. Deleting a comment is a soft delete that
leaves a placeholder in the thread, and only moderators can read what a
deleted comment said. Threads are soft-deleted with their owner instead of
removed, which replaces ADR-0013's lifecycle rule.

### Body

- A body is plain text, as ADR-0013 says. It is not rendered as Markdown or
  HTML. The frontend escapes it and keeps line breaks.
- Leading and trailing whitespace is trimmed. A body that is empty after
  trimming is rejected.
- A body is at most **4,000 characters**, counted as .NET `string.Length`
  (UTF-16 code units). `nvarchar(4000)` uses the same unit, so the column is
  `nvarchar(4000)` and the limit is enforced in Domain and by the schema
  alike. 4,000 characters is several long paragraphs. Anything longer
  belongs in a document, not a comment.
- The same rules apply when posting and when editing.

### Editing

- **Only the author may edit** a comment. Anyone else gets
  `ForbiddenException` (ADR-0015).
- There is **no time limit**. A comment can be edited at any time while it
  is not deleted, because every earlier version is kept.
- Editing sets the comment's edited time (ADR-0013). Every client shows an
  edited comment as "edited", with the time, and lets the reader open its
  earlier versions.
- An edit that leaves the body unchanged is a no-op and records nothing.
- Editing does not move a comment. Threads are ordered by the time each
  comment was first posted, oldest first, with the comment id as the
  tie-breaker.

### Version history

- Every version of a comment is a row in the same `Comments` table. The
  **current version keeps the comment's id**, so links, API requests and
  ordering stay stable across edits.
- An edit inserts a new row holding the replaced body and the time that
  body was written, and marks it superseded with the time of the edit. The
  current row takes the new body, and its `PreviousVersionId` points at the
  new row. That row's own `PreviousVersionId` points at the version before
  it, so the history is a chain from newest to oldest. A unique index on
  `PreviousVersionId` keeps the chain linear.
- A superseded row belongs to the same thread and author, and is never
  changed again. Only the current row of a comment is ever updated.
- Superseded rows are not part of the aggregate. The repository loads only
  current rows. `Comment.Edit` creates the row for the replaced version,
  and commands never read history.

### Deleting a comment

- The **author** may delete their own comment.
- A **moderator** may delete anyone's comment. The thread does not decide
  who is a moderator. The owning feature's handler decides and passes it in,
  for example `thread.Delete(commentId, by: userId, asModerator: isAdmin)`.
  For stays, a moderator is an administrator of the stay's facility. Each
  later feature states its own rule, normally the same one.
- Deleting is a **soft delete**. The comment's row and its whole version
  chain are kept unchanged. The current row records when it was deleted and
  by whom.
- A deleted comment cannot be edited. Deleting it again succeeds and
  changes nothing, so a retried request does no harm. There is no restore.

### Deleting a thread's owner

This replaces ADR-0013's rule that the owner's delete handler removes its
thread.

- A thread is never removed from the database by the application. When its
  owner is deleted, the owner's delete handler **soft-deletes the thread**
  in the same unit of work, recording when and by whom. A deleted thread
  accepts no new comments, edits or deletes.
- Because a thread can only be reached through its owner (ADR-0013), an
  owner with a thread is **soft-deleted too**, not removed. It disappears
  from members' views, but its moderators can still find it and read its
  thread. Rows that only enforce the owner's rules may still be removed;
  for example, a deleted exclusive stay frees its nights by removing its
  rows from the exclusive-nights table (ADR-0005).
- Removing data for real, such as when a user leaves, is an operator task
  outside the API (ADR-0015) and needs its own rule.

### Reading

Every reader must first be allowed to read the thread, which the owning
feature checks (ADR-0013, ADR-0015).

- **Members** see every comment in posted order. A deleted comment appears
  as a **placeholder** in its place, with its author and posted time but no
  body. It reads "Comment deleted", or "Comment removed by an
  administrator" when a moderator deleted someone else's comment.
- **Members** can read the full version history of any comment that is not
  deleted. Members can tell what a reply was answering, and an edit cannot
  quietly change what was agreed.
- **Moderators** can also read the body and version history of deleted
  comments, who deleted them and when, and the threads of deleted owners.
  The owning feature exposes this as its own query, for example
  `GetStayCommentHistoryForModerator`, and decides who counts as a
  moderator in the same way as for deleting.
- What each reader may see is decided in one place: the shared read
  helpers (ADR-0013). One projects a thread for members, with placeholders
  instead of deleted bodies. One projects a single comment's versions for
  members, and refuses deleted comments. One projects everything for
  moderators. There is no EF Core global query filter, because the
  aggregate needs to see deleted comments to refuse edits and to make a
  repeated delete harmless.
- The frontend tells users plainly that every version of a comment stays
  visible and that administrators can still read deleted comments.

## Options considered

### Recording edits

#### Option 1: Earlier versions as linked rows in the same table (chosen)

- Pros: One table and one entity shape for every version; the comment's id
  never changes; earlier versions are written once and never updated;
  history is a simple chain to walk; current comments are read with one
  filter.
- Cons: Every edit is an insert plus an update; superseded rows share the
  table with current ones, so every thread query must exclude them.

#### Option 2: A new row per edit that replaces the old one

Each edit inserts a row with a new id and links back to the version it
replaces.

- Pros: No row is ever updated after it is written.
- Cons: The comment's id changes on every edit, so clients holding an older
  id address a superseded version; ordering must use the first version's
  posted time, which the current row does not hold.

#### Option 3: A separate history table

- Pros: Current comments stay alone in their table, so thread queries need
  no filter for versions.
- Cons: A second table and mapping with the same columns; two shapes of the
  same data to keep in step.

#### Option 4: No history, edits marked as edited

- Pros: Simplest; matches most chat apps.
- Cons: In a dispute about money, someone can change what they wrote after
  others replied, and readers see only that it was edited.

#### Option 5: No editing

- Pros: What was said stays said, with no history needed.
- Cons: Every typo becomes a delete and a repost; the edited time in
  ADR-0013's comment shape would be unused.

### Who may read edit history

#### Option 1: Every member who can read the thread (chosen)

- Pros: The people in the discussion can check for themselves what was
  said before an edit; nobody has to ask an administrator; edits stay
  honest because they are visible.
- Cons: An author cannot hide an embarrassing first wording by editing it;
  only deleting does that, and even then moderators can read it.

#### Option 2: Moderators only

- Pros: Fixing a mistake hides it from everyone but administrators.
- Cons: A member who suspects an edit changed what was agreed must ask an
  administrator; edits look the same as in an app with no history.

### Deleting a comment

#### Option 1: Soft delete with a placeholder, body readable by moderators (chosen)

- Pros: Nothing said in a dispute is lost; members can see that something
  was removed and by whom, so replies to it still make sense; deleting is
  idempotent; moderators can settle "but you said..." by reading what was
  said.
- Cons: Text the author wanted gone is still stored and readable by
  administrators; the placeholder shows that the author took something
  back.

#### Option 2: Soft delete without a placeholder

- Pros: Members see a clean thread.
- Cons: Replies to a deleted comment read oddly, and members cannot tell
  that anything was removed.

#### Option 3: Tombstone with the body erased

- Pros: The words are really gone, which is what a person deleting a
  comment may expect.
- Cons: The record of a dispute can be destroyed by one of the parties to
  it.

#### Option 4: Hard delete

- Pros: Simplest; nothing left behind.
- Cons: Same loss as a tombstone, and replies lose their context with no
  trace.

### Deleting a thread's owner

#### Option 1: Soft-delete both the owner and its thread (chosen)

- Pros: A discussion survives the deletion of what it was about, which is
  exactly when it may be needed; moderators reach it the same way as any
  other thread; follows the same rule as deleting a comment.
- Cons: Every commentable entity needs a deleted state, and its feature's
  member queries must exclude deleted ones.

#### Option 2: Keep the thread, remove the owner

- Pros: Owners need no deleted state.
- Cons: Nothing points at the thread any more, so it is reachable only by
  an operator with database access; that is the orphaned thread ADR-0013
  set out to prevent.

#### Option 3: Remove the thread with its owner (ADR-0013 as written)

- Pros: Simplest; nothing left behind.
- Cons: Deleting an expense deletes the discussion about it, which undoes
  everything the comment history is meant to protect.

### Who may delete other people's comments

#### Option 1: A moderator flag passed in by the owning feature (chosen)

- Pros: The thread stays ignorant of its owner, as ADR-0013 requires; the
  facility decides who is a moderator, as ADR-0015 requires; one rule in
  the aggregate covers every feature.
- Cons: Each owning feature must decide and pass the flag correctly; a
  handler that always passes `false` silently removes moderation.

#### Option 2: Only the author may delete

- Pros: Nobody's words are removed by someone else.
- Cons: A hurtful or misplaced comment stays up until its author agrees to
  remove it.

### Hiding what readers may not see

#### Option 1: Filters in the shared read helpers (chosen)

- Pros: The aggregate sees deleted comments and can enforce its rules;
  moderator queries need no escape hatch; each rule is written once,
  because every thread query goes through a helper.
- Cons: A query that bypasses the helpers could show a deleted body to
  members. Tests for the helpers and review are the safety net.

#### Option 2: An EF Core global query filter

- Pros: Hidden by default everywhere, including queries that forget.
- Cons: Removes deleted comments entirely, but members must still see their
  placeholders; also hides them from the repository, so the aggregate
  cannot tell deleted from missing; moderator queries must turn the filter
  off.

### Body length

#### Option 1: 4,000 characters in `nvarchar(4000)` (chosen)

- Pros: Plenty for a comment; the database and Domain count the same unit;
  stays out of `nvarchar(max)`.
- Cons: Counts UTF-16 code units, so text heavy in emoji reaches the limit
  a little sooner than its visible length suggests.

#### Option 2: No limit, `nvarchar(max)`

- Pros: Never refuses a comment.
- Cons: Unbounded rows in a table that now also keeps every version; the
  limit then exists only in whatever the frontend enforces.

## Consequences

### Positive

- What anyone wrote in a thread can always be recovered, by members for
  edits and by moderators for deletions. Threads can serve as the record of
  a disagreement about money, even after the thing they discussed is
  deleted.
- Authors can still fix and take back comments with no time limit, and
  readers can always tell that they did.
- The comment's id is stable across edits, and the history needs no extra
  table.
- All comment rules live in the `CommentThread` aggregate and are
  unit-tested there. Features decide only who counts as a moderator.
- Deleting is idempotent, so a retried request is harmless.

### Negative

- Neither editing nor deleting removes text. Users must be told so, and
  removing it for real, for example when someone leaves, needs its own
  rule.
- Every commentable entity must be soft-deleted, starting with stays. Their
  member queries, such as the calendar, must exclude deleted ones, and
  their moderators need a way to find them.
- The `Comments` table grows with every edit and deletion, and no thread is
  ever removed. At family scale that does not matter, and commands load
  only current rows.
- Members' views must never show a deleted body. Only the shared read
  helpers decide that, and their tests must cover placeholders, refused
  history and the moderator view.
- Every handler that deletes comments or reads deleted ones must pass the
  moderator flag correctly. Tests per owning feature should cover the
  author, moderator and other-member cases.
- ADR-0013 stays accepted, but its lifecycle rule and the test it asked for
  (that no thread is orphaned) are replaced here. Readers of ADR-0013 need
  to follow its link to this ADR.

### Follow-up

- Implement the body, edit and delete rules, the version chain, the soft
  delete of comments and threads, the read helpers, and the history
  queries for members and moderators in the booking feature, with stays
  soft-deleted and administrators of the stay's facility as moderators
  ([#8](https://github.com/gyback/facility-management/issues/8)).
- Map the "comment is deleted" and "thread is deleted" domain errors to
  problem responses in the error contract
  ([#43](https://github.com/gyback/facility-management/issues/43)).
- Decide what happens to a user's comments, including earlier versions and
  deleted comments, when they remove their account
  ([#51](https://github.com/gyback/facility-management/issues/51)).

## References

- Issue [#32: Decide the rules for comments](https://github.com/gyback/facility-management/issues/32)
- [ADR-0003: Use Clean Architecture](0003-use-clean-architecture.md)
- [ADR-0005: Model bookings as non-blocking stays](0005-model-bookings-as-non-blocking-stays.md)
- [ADR-0008: Use Azure SQL Database](0008-use-azure-sql-database.md)
- [ADR-0013: Share comment threads across features](0013-share-comment-threads-across-features.md)
- [ADR-0014: Use aggregate repositories for commands and projections for queries](0014-use-aggregate-repositories-for-commands-and-projections-for-queries.md)
- [ADR-0015: Authenticate with Entra External ID and link identities to users by invitation](0015-authenticate-with-entra-external-id-and-invitation-linked-users.md)
- Microsoft, [nchar and nvarchar (Transact-SQL)](https://learn.microsoft.com/sql/t-sql/data-types/nchar-and-nvarchar-transact-sql)
