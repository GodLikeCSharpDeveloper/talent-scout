# Workspace updates, campaign editing and locations

All `/api/*` requests require `X-Api-Key`. These routes extend the generated `/openapi/v1.json`; the older checked-in snapshot may not contain them.

## Live updates

`POST /api/realtime/ticket` returns `{ "ticket": "…", "expiresInSeconds": 60 }` with `Cache-Control: no-store`. Use the ticket (never the long-lived API key) for the SignalR WebSocket at `/hubs/workspace?access_token=…`. The ticket is purpose-scoped, time-limited and invalidated for new connections when the API key changes. The UI obtains a fresh ticket on reconnect. This is the existing single-workspace/shared-key authorization model.

Events:

- `WorkspaceChanged`: `{ "entities": ["Campaign", "Enrollment", "Message", …] }` after successful EF saves. Consumers refetch authorized read models; events contain no candidate content or credentials.
- `OperationProgress`: `{ "campaignId": "uuid", "enrollmentId": "uuid or null", "operation": "Search or Screening", "state": "Running, Completed or Failed", "added": 3, "scanned": 8, "occurredAt": "ISO timestamp" }`. Counts are optional. Search publishes progress between pages; AI screening publishes its start/end. Progress is transient; durable results remain in candidates, search batches, messages and audit events.

- `WorkspaceNotification`: `{ "id": "search-batch uuid", "type": "SearchCompleted", "campaignId": "uuid", "campaignName": "Backend team", "count": 50, "automaticApproval": false }`, emitted after the completed search batch is saved. Clients translate the structured payload and distinguish manual approval from automatic approval of suitable profiles. This does not claim that AI screening has already finished.

UI notices dismiss after five seconds. Hovering or keyboard focus cancels the current timer; leaving starts a fresh five-second timer. A new notice also starts a fresh timer. Notification IDs deduplicate push events for the current connection.

Events are best effort within one API instance, not a durable queue. Reconnect performs a snapshot refresh; the UI polls every 30 seconds while push is unavailable. Multiple API instances would require a SignalR backplane and shared data-protection keys. No external calls are repeated automatically because of a socket reconnect.

## Edit campaign

`PUT /api/campaigns/{id}` accepts the same fields and validation as campaign creation, including optional `locationId`. Editable only while `Draft` or `Paused`; active/stopped campaigns return 409. Status is unchanged. Existing assessments, approvals, messages and bookings remain; edited requirements/prompts apply to subsequent operations. The UI offers **Pause and edit** for an active campaign. A stopped campaign remains read-only.

## Location autocomplete

`GET /api/integrations/linkedin/locations?query=Kyiv` accepts 2–100 characters and returns `[ { "id": "provider-id", "name": "Kyiv, Ukraine" } ]`. Results are cached in PostgreSQL for seven days by account and normalized query. Provider errors propagate; fabricated fallback results are never returned in live mode.

Creation/editing saves both `location` (label) and `locationId` (LinkedIn ID). Search uses the selected ID directly and verifies it against continuation cursors. Existing campaigns without an ID keep the previous text-resolution behavior.

Migration `AddLocationCacheAndCampaignLocationId` adds the nullable campaign field and location-search cache; startup applies it through the existing migration mechanism.

## Reassess selected profiles

`POST /api/enrollments/{id}/rescreen-profile` explicitly runs AI again using the saved candidate profile and the campaign's current title, description, requirements and system prompt. It replaces the score, explanation and screening status using the current minimum score and AI mandatory-criteria decision. It does not fetch LinkedIn again or send messages. The existing `screen-profile` command remains idempotent.

Allowed for Found, Screened and Rejected profiles before outreach starts, including while a campaign is paused. Excluded candidates, stopped campaigns and profiles with started outreach/messages/invitations return 409. Screening progress and saved changes use the existing SignalR events.

The UI confirms the selected profiles, submits them sequentially, refreshes results after each and displays individual failures. Stop/navigation prevents remaining submissions; an in-flight assessment may finish. Transport/auth/provider failures stop the remaining batch without automatic retries.

## Automatic first contact and three independent workers

`ProfileScreeningWorker` processes Found profiles, `MessageSendingWorker` sends connection requests/introductions/interview links, and `ReplyAnalysisWorker` analyzes incoming replies. Each is an independent hosted worker with its own explicit repository query. `PipelineExecutionService` shares only timer execution, per-item scopes, scheduling and failure recovery; no custom worker base class or queue selector is used. All run inside the API, sharing `Pipeline:WorkerEnabled`, `PollIntervalSeconds` and `BatchSize` (per worker). Incoming replies do not wait on outreach delays; outgoing messages wait for unprocessed replies to be analyzed.

After approval, an eligible candidate who can receive messages follows the existing introduction flow. Otherwise the outreach worker sends a connection request with an AI-generated note of 1–200 characters. The note is saved before dispatch and returned as `connectionNote` by `GET /api/enrollments/{id}/linkedin/conversation`. An explicit request can also use `POST /api/enrollments/{id}/linkedin/connection-request` with `{ "generateNote": true }`; existing manual `note` inputs remain supported.

Invitation dispatch rechecks current campaign/approval permissions, shares campaign outbound spacing and has a daily attempt cap. An atomic claim prevents duplicate invitations. Sent, pending, sending and unknown requests are never automatically resent. The UI exposes pending/unknown outcomes and the saved note. Acceptance webhooks wake outreach; periodic contact checks provide a fallback. Once connected, the full introduction is sent. If the candidate replies to the note first, the saved note becomes the initial conversation entry and the reply is analyzed without sending a duplicate introduction.

Migration `AddLinkedInConnectionNote` adds the nullable saved-note field. Test coverage uses a real local PostgreSQL database, synthetic LinkedIn responses and the existing HTTP OpenAI fixture; it does not establish real provider delivery or AI note quality.

## Sorted candidate pages

`GET /api/campaigns/{id}/candidates/page` returns `{ "items": [...], "total": 25, "page": 2, "pageSize": 8 }`. Query parameters: `page`, `pageSize` (1–100), `sortBy` (`Name`, `FitScore`, `Status`, `FirstContact`, `LinkedInStatus`), `descending`, optional `search`, `status`, `needsDecision` and the existing approval/search-batch filters. Search and all filters apply before counting, sorting and pagination. Score is descending by default; missing scores are last in both directions. Status uses pipeline enum order; first-contact stages are pending, automatic, approved, started, excluded. Every order has an enrollment ID tie-breaker.

Count and rows use a consistent PostgreSQL snapshot. If a page disappears after filtering/live changes, the response clamps to the last available page. The existing `/candidates` array response remains available for consumers that need all records.

The table query key includes campaign, page, filters and sort. SignalR cancels in-flight candidate-page requests and refetches active views; no event prepends a row. Returned page contents replace the old array, so newly added/reassessed candidates appear only where the server ordering places them. Changing sort/search/filter returns to page one and clears selection. Moving between pages clears selection too.

## LinkedIn synchronization, links and explicit retry

`LinkedInSynchronizationWorker` checks tracked conversations on startup and every ten minutes while `Pipeline:WorkerEnabled` is enabled. `Pipeline:SynchronizationIntervalMinutes` / `PIPELINE_SYNC_INTERVAL_MINUTES` configures the interval. Due conversations are processed in batches until all are checked; failures are recorded and deferred. A database lease serializes synchronization for a candidate and prevents a new send while synchronization is applying its snapshot.

`POST /api/enrollments/{id}/linkedin/sync` runs the same `LinkedInSynchronizationService` immediately, including for paused campaigns. It checks the current relationship and imports incoming and manual outgoing messages. Message synchronization is insert-only: read provider models, load existing `ExternalId` values in one database query, skip known IDs and insert missing IDs. A unique database index also protects `ExternalId`. Existing message content, delivery results and campaign stages are left unchanged; there is no matching by text or reconstruction of first contact. Provider edit/deletion flags are stored when a message is first imported. New text replies from the current outreach are left for the reply worker; historical replies are stored without triggering AI analysis.

Provider invitation strings are translated inside `UnipileContactsAdapter` into our `ConnectionInvitationStatus` enum (`Pending`, `Declined`, `Withdrawn`); unknown values remain unknown. Business services depend on the integration interfaces and our typed models, without parsing provider responses.

The Unipile adapter delegates to typed `GetMessagesAsync`, `GetChatsForAttendeeAsync` and `GetSentInvitationsAsync` API methods. Cursor pagination must finish successfully before a snapshot is applied; repeated cursors, malformed flags, cross-account chats and ambiguous multiple chats fail visibly. The existing API v1 routes are retained.

Connection states: `Unknown`, `NotConnected`, `Pending`, `Connected`, `Declined`, `Withdrawn`, `Inactive`, `ActionRequired`. The profile API sent-invitation status `IGNORED` maps to `Declined` and `WITHDRAWN` maps to `Withdrawn`. Both block automatic outreach. Only `Withdrawn` permits an explicit manual resend. Disappearance from the pending list without an explicit reason is `Inactive`; it does not establish who cancelled, and retries are blocked. Known decline/withdrawal reasons survive later responses that omit the reason. `LastSynchronizedAt` and `SynchronizationError` describe the last check. Incoming/outgoing message timestamps and explicit deleted/edited flags are retained.

`POST /api/enrollments/{id}/linkedin/retry` with `{ "confirmNotSent": false }` first synchronizes, then explicitly retries only that candidate's eligible action. Already confirmed sent messages/pending invitations are not repeated. Declined and unexplained inactive invitations cannot be retried. A confirmed withdrawal can be resent only by this explicit action; LinkedIn may still enforce its withdrawal cooldown. Unresolved delivery attempts require waiting five minutes and `{ "confirmNotSent": true }` after checking LinkedIn; a failed synchronization prevents the retry. Campaign approvals and send limits remain enforced.

The UI provides a manual-sync icon with a hover explanation, colored approval/contact states and a retry dialog from the failure icon. Profile links open the actual profile. Pending invitation links open LinkedIn's Sent requests list (no fabricated per-invitation deep link). Connected chats use a verified native provider conversation ID. Manual messages are labeled `You · LinkedIn`.

Migration `AddLinkedInSynchronization` adds synchronization metadata and message edit/deletion flags and permits multiple manual outgoing messages. Tests use synthetic LinkedIn snapshots and a local Kestrel HTTP upstream, not real message sending.

## Deploy this update

From the API repository: `docker compose up --build -d`. From the UI repository: `npm run build`, then reload the extension at `chrome://extensions` / `edge://extensions` and reload the workspace tab. The settings panel still points to `http://localhost:5262`; keep the Unipile webhook tunnel running separately.

Candidate rows expose a computed `linkedInStatus`: NotStarted, Sending, Pending, Connected, Conversation, Withdrawn, Declined, Inactive or Failed. Sorting uses this same value in SQL before pagination, with an ID tie-breaker. Unknown delivery and current synchronization errors are Failed; historical audit failures do not override resolved states. The UI separates approval, LinkedIn status/actions, and external links into different columns. SignalR refetches the currently sorted page.

## Database query indexes

`AddWorkspaceQueryIndexes` adds four ordinary B-tree indexes: `Enrollments(Status, NextContactAt)` for worker queues, `LinkedInConversations(AccountId, NextSynchronizationAt)` for due synchronization, `Messages(EnrollmentId, CreatedAt, Id)` for all message kinds/history/pagination, and `AuditEvents(EnrollmentId, OccurredAt)` for per-candidate failure lookups. Existing unique external-ID, campaign/candidate and account/conversation indexes retain duplicate protection. No data rewrite or additional status table is introduced. Computed LinkedIn-status sorting runs in SQL before paging; it cannot be covered by a single ordinary index across the related tables.
