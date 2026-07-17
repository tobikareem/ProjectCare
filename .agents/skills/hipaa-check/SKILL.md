---
name: "hipaa-check"
description: "Run a risk-based HIPAA/PHI exposure review for CarePath changes. Use the full review when code or configuration touches patient/client data, PHI entities or DTOs, healthcare endpoints, authorization, audit events, logging, exceptions, telemetry, storage, exports, messaging, SignalR, EF mappings/migrations, retention, deletion, or third-party providers, and before merging any PHI-adjacent feature. For changes with no plausible PHI impact, perform only the lightweight impact gate."
---

# hipaa-check

Perform a targeted engineering review for PHI exposure and CarePath compliance controls. This is a code safeguard, not a legal certification or a substitute for organizational risk analysis, policies, training, vendor agreements, or deployment verification.

## 1. Choose the Review Level

Run this lightweight impact gate for every implementation:

1. Does the change read, create, update, transmit, display, cache, export, delete, or derive patient/client information?
2. Does it touch a PHI entity, healthcare DTO, endpoint, authorization rule, audit event, log, exception, telemetry, storage path, migration, message, notification, search, file, photo, or external provider?
3. Could it change who can see healthcare data, what they can see, how access is recorded, or how long records remain available?

If every answer is demonstrably no, record `No PHI impact identified` in the implementation summary and stop. Do not run the full checklist for formatting, spelling, non-sensitive documentation, or isolated code that cannot affect PHI handling.

If any answer is yes or uncertain, run the full review below. Always run it before merging a PHI-adjacent feature.

## 2. Establish Scope

- Read `AGENTS.md`, `_specs/lessons.md`, and the applicable approved requirements, design, and task specs.
- For uncommitted work, inspect both `git diff` and `git diff --staged`, including new files not represented by a normal diff.
- Trace affected code across contracts, mapping, Application services, Infrastructure, WebApi, UI, tests, background jobs, messaging, and provider integrations.
- Review only affected entities and their indirect data flows. Do not claim coverage for areas not inspected.

Treat these CarePath records as PHI or PHI-bearing clinical context:

- `Client`, `CarePlan`, `Shift`, `VisitNote`, `VisitPhoto`, `CaregiverCertification`
- `DischargeDocument`, `TransitionPlan`, `TransitionInstruction`, `TransitionCheckIn`
- Any DTO, file, event, cache entry, notification, search term, or telemetry item derived from those records

Also treat caregiver credentials and workforce records as sensitive PII even when they are not patient PHI. Apply the stricter project controls where `AGENTS.md` designates them as protected.

## 3. Review Data Classification and Minimum Necessary Use

- Identify every sensitive field entering, leaving, or being derived by the changed flow.
- Verify request, response, list, detail, event, and export contracts contain only fields required for the authorized purpose.
- Reject Domain entity serialization, broad AutoMapper flattening, unbounded navigation loading, and DTO reuse across audiences with different permissions.
- Treat a name, identifier, appointment, location, or shift as PHI when its context links a person to healthcare.
- Keep financial rates and compensation out of general healthcare DTOs unless an explicitly approved role-scoped workflow requires them.

## 4. Review Authentication and Authorization

Require all of the following for PHI access:

1. Authentication at the controller, endpoint, hub, job, or message-consumer boundary.
2. Approved role or policy authorization for the operation.
3. Object-level authorization proving ownership, assignment, care-team access, facility scope, or an explicit access grant for the specific record.
4. Authorization before business-state checks or distinguishable errors that could reveal whether a record exists.
5. Minimum-necessary field and operation access after authorization succeeds.

Accept role attributes, authorization policies, resource handlers, and service-layer access checks when their combined behavior satisfies the approved spec. Do not require a literal `[Authorize(Roles = "...")]` when a stricter policy-based design is used.

Flag:

- `[AllowAnonymous]` on a PHI flow
- Authentication without role/policy restriction
- Role checks without record-level access checks
- Client-supplied user, caregiver, facility, or client IDs trusted as authorization proof
- Background jobs, hubs, signed links, or provider callbacks that bypass the normal access decision
- Missing webhook signature verification or replay protection

## 5. Review URLs, Errors, and Response Semantics

- Never place names, diagnoses, document text, symptoms, addresses, GPS coordinates, policy numbers, or other PHI in routes, query strings, redirects, file names, SignalR group names, or signed-link metadata.
- A `Guid` is an opaque identifier, not proof that a URL is safe. Require object authorization and PHI-safe access logging for ID-based routes.
- Prefer request bodies for sensitive searches, with authorization, size limits, validation, and safe server logging.
- Ensure unauthorized and missing PHI resources use indistinguishable response semantics where existence disclosure is a risk.
- Keep PHI and attempted values out of validation responses, exception messages, problem details, trace bodies, and redirect parameters.

## 6. Review Logs, Telemetry, and Audit Events

Ordinary application logs and telemetry must not contain PHI. Flag:

- Patient/client names, DOB, address, diagnosis, symptoms, medications, vitals, document text, GPS, insurance data, photos, signatures, or sensitive search terms
- Entity/DTO destructuring such as `{@Client}` or `{@VisitNote}`
- Request/response body logging, query-string capture, breadcrumb capture, or exception messages containing PHI
- Metrics labels, span attributes, analytics events, crash reports, or SignalR diagnostics containing PHI or high-cardinality record identifiers

Prefer stable event names and the minimum identifiers needed for operational correlation. Record PHI access in the dedicated audit system, not ordinary logs.

For each PHI read, create, update, soft delete, export, download, or external disclosure, verify a separate append-only audit event records at least:

- Actor/user ID and timestamp
- Action and outcome
- Entity type and entity ID or approved non-PHI correlation reference
- Access channel or purpose when required by the approved design

Audit events must not contain PHI values. Verify bulk operations produce sufficient per-record or approved batch-to-record traceability and that internal reads of high-risk fields such as `RawContent`, `SourceText`, and `ResponsesJson` are audited.

## 7. Review Persistence, Retention, and Migrations

- Reject `Remove`, `RemoveRange`, raw `DELETE`, `TRUNCATE`, cascade deletion, or destructive migration rollback for every project-designated PHI or protected PII record and its relationships.
- Verify soft deletion and the effective global query filter in the EF model. CarePath may apply filters centrally; do not require every entity configuration file to repeat `HasQueryFilter`.
- Verify administrative queries that intentionally bypass filters are authorized, audited, and narrowly scoped.
- Preserve the six-year minimum retention rule, legal holds, relationships, and audit history.
- Treat PHI-bearing migrations as forward-only after real use unless an approved retention-safe plan proves no protected record or audit history can be destroyed.
- Review indexes, constraints, defaults, backfills, seed data, and generated SQL for accidental PHI copies or exposure.

## 8. Review Storage, Encryption, and Secrets

Distinguish the controls:

- Connection encryption (`Encrypt=True`/strict TLS) protects data in transit to SQL Server.
- SQL Server TDE or the platform equivalent protects database and transaction-log files at rest.
- Application/column encryption may be required by the approved threat model for especially sensitive fields.

Do not treat a connection-string flag as proof that TDE is enabled. Verify deployment evidence separately and mark it `Not verifiable from code` when unavailable.

For files, photos, signatures, exports, and temporary artifacts, require private encrypted storage, access control, malware scanning where applicable, short-lived authorized access, safe object names, retention enforcement, and cleanup. Reject public containers and durable public URLs.

Verify secrets come from approved secret storage and never appear in source, logs, generated reports, client bundles, or exception output.

## 9. Review External Disclosures and Messaging

Before PHI reaches SMS, voice, email, AI/OCR, analytics, storage, or another provider, verify:

- The approved spec permits the disclosure and applies the minimum-necessary rule.
- HIPAA readiness and required BAA/provider gates are documented.
- Consent, opt-out, channel restrictions, and content minimization are enforced where applicable.
- Webhooks are authenticated, replay-protected, rate-limited, and processed without logging bodies.
- Retries, dead-letter queues, caches, prompts, model traces, and provider dashboards cannot retain unapproved PHI.
- Transition reminders are not delivered before the plan is active.

## 10. Verify Tests and Operational Evidence

Require focused tests for the affected risks, including as applicable:

- Allowed and denied roles
- Ownership/assignment and IDOR attempts
- Missing-versus-denied response equivalence
- Audit creation for reads, writes, soft deletes, exports, and failed access
- DTO field allowlists or sensitive-field denylist guards
- PHI-free logs, validation errors, exceptions, and problem details
- Soft-delete filters and destructive migration guards
- Signed-link expiry, webhook verification, and message minimization

Do not claim TDE, backups, legal holds, private provider configuration, BAA status, or operational monitoring from source code alone. List required deployment or organizational evidence as a residual verification item.

## Output

Return the report in the response by default. Save it under `_specs/hipaa-reviews/` only when the user explicitly requests a persistent report or an approved task requires one.

Use this structure:

```markdown
# HIPAA/PHI Engineering Review
Scope: [changed files, feature, or PR]
Impact gate: [Triggered / No PHI impact identified]

## Findings
### Critical
[Unauthorized disclosure, data destruction, public storage, missing object authorization]

### Warnings
[Weak controls, missing tests, unverifiable deployment requirements]

### Information
[Non-blocking hardening suggestions]

## Coverage
| Affected flow/entity | Minimum necessary | Authorization | Audit | Logging/errors | Retention/storage | Tests |
|---|---|---|---|---|---|---|

## Residual verification
[Deployment, BAA, TDE, backup, legal-hold, or operational evidence not verifiable from code]

## Recommendation
[PASS / PASS WITH WARNINGS / FAIL]
```

Use `Verified`, `Finding`, `Not applicable`, or `Not verified` in coverage cells. Include a concise file/test/evidence reference for each `Verified` value; explain every `Finding` and `Not verified` value in the findings or residual-verification sections.

Order findings by severity and include exact file paths, line numbers, impact, and concrete fixes. Do not state that CarePath is HIPAA-compliant; state only what the reviewed engineering scope demonstrates.
