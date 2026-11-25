# CarePath Health: independent second-opinion review

> Recorded verbatim from the external reviewer's report on 2026-10-22. The team's response, the
> fixes landed the same day and where each remaining finding is tracked are in
> `CODE_REVIEW_TODO.md` ("Second-opinion review action plan — 2026-10-22"), the CP-05/CP-06/CP-07
> draft requirement specs, and `_specs/lessons.md`.

Date: 2026-10-22 (America/Los_Angeles)  
Repository: `tobikareem/ProjectCare`  
Branch: `claude/friendly-brown-v1nolz`  
Reviewed commit: `4b5db1d66e9d3803925387e3b89aa0c6016b50d3`  
Comparison: `origin/main...HEAD` (217 files; 20,800 insertions, 2,055 deletions).  
Scope: architecture, CP-04, operational services, contracts, web, persistence, tests, deployment, specifications, and Nigeria/US launch readiness. Read-only assessment; suggested fixes are not implemented.

## Executive summary

**Nigeria: NOT READY for production patient data or a paying agency. United States/Maryland: NOT READY for production patient data or multi-agency SaaS.** The US workflow is substantially closer to a controlled pilot; Nigeria needs a product adaptation as well as the same security and operating controls. Synthetic-data demonstrations are appropriate now. Running the company's own business with real patients still requires the production gates below.

The implementation has a credible foundation: inward project dependencies, client-safe contracts, explicit object access checks, soft-delete conventions, restricted clinical foreign keys, clinician approval of discharge instructions, and host-bound JWTs. On .NET SDK 10.0.301, the solution builds with **zero warnings/errors**, and **all 1,189 tests pass**. NuGet reports no known vulnerable direct or transitive packages against its configured source. These results demonstrate useful engineering discipline, not deployment or compliance readiness.

The principal blockers are substantive. **CP-04 binds authentication to a host but still sends operational requests to one configured database. Already-issued tokens remain usable after membership deactivation or role downgrade. PHI audits are ordinary application log messages rather than a durable, independently protected audit system.** The control plane can identify clients and their healthcare relationships; calling it universally non-PHI is unsafe. Concurrent scheduling can double-book caregivers, and repeated payment requests can duplicate receipts. The HomeLab stack supplies neither a demonstrated TLS edge nor production recovery evidence.

Nigeria is not a currency-symbol change: invoices lack currency, workforce classifications and credentials encode US assumptions, organization time zones do not govern scheduling inputs, and a usable offline caregiver workflow is absent. Privacy governance must cover Nigerian processing, international transfers, rights requests, retention, and messaging. Validate local agency workflows before expanding Transitions, AI, and white-label features.

**Keep ADR 0003's database-per-organization direction**, with explicit regional deployment and sensitive-identity boundaries. At ten organizations, automate admission, provisioning, migrations, backups, and restore checks. At a hundred, connection budgets, control-plane availability, tenant placement, and fleet change management become operational products. The ADR mostly recognizes these issues; the implementation has not earned its production exit gate.

Spend the next two sprints on authoritative authorization/audit and safe tenant routing plus one narrowly scoped market pilot. Do not promise that two sprints finish mobile, automated clinical messaging, US payer integration, and a Nigeria launch. Build/test success is real; the current completion/readiness claims are broader than the evidence.

## Method, evidence, and tools

Evidence labels below mean **Ran** (observed command/test/browser result), **Read** (direct source/configuration/spec inspection), or **Inferred** (a consequence of inspected code that still needs the named reproduction). Severity describes production impact; an acknowledged deferral can still be a release blocker. Findings are not claims that production has already suffered a breach.

Read the repository instructions, `_specs/lessons.md`, progress and task trackers, previous review TODOs and reviewer memory; ADRs 0001–0003; architecture and UI design/wireframe; CP-04 requirements/design/tasks and Sprint 8 evidence; earlier sprint/spec material at relevant feature boundaries. Traced the request pipeline, identity, memberships, bootstrap, object authorization, client/care-plan, scheduling, visit-photo, billing, Transitions, client/web configuration, and EF mappings. Compared CP-04 against main. This is a broad risk-based review, **not a claim that every line of every historical spec or generated migration was independently audited**.

Read `.agents/skills/hipaa-check/SKILL.md`, `code-review`, `migration`, `dotnet-code`, and `.claude/agents/dotnet-code-reviewer.md`. Applied the HIPAA engineering checklist; used the other files as house-rule context, not as authority to implement, alter memory, or rerun a pre-commit workflow against an empty diff. The requested output/location overrides their default output structures.

**MiroFish limitation:** plugin discovery returned no matching installed/available plugin. The existing sibling `MiroFish/README.md` describes graph building, persona generation, OASIS social simulation, reports, and interviews, requiring external LLM and Zep services. Local dependency directories and non-placeholder key settings exist, but neither documented local service responded on ports 3000/5001. No MiroFish job was launched, no project code was uploaded to those cloud services, and no simulation/interview result is claimed. The five stakeholder analyses below are explicit human-style scenario walkthroughs based on code, not MiroFish output or customer interviews. This part of the requested review remains unverified.

### Verification actually performed

| Check | Result | Boundary |
|---|---|---|
| `dotnet build CarePath.sln` | Pass; 0 warnings, 0 errors | SDK 10.0.301 on macOS |
| `dotnet test CarePath.sln --no-build` | 1,189 pass; 0 fail; 0 skipped | Domain 297; Application 647; Infrastructure 177; Web 68 |
| `dotnet list CarePath.sln package --vulnerable --include-transitive` | No known vulnerable packages reported | NuGet v3 source; not an image, secret, JavaScript, or zero-day assessment |
| `dotnet run --project WebApi --no-build` | Fail during startup | `PlatformNotSupportedException: LocalDB is not supported on this platform`, Program.cs:152 |
| `dotnet run --project CarePath.Web --no-build` | Starts; HTTP 200 on port 5283 | Browser renders `/login`, labeled email/password, disabled empty-form submit; no authenticated workflow run |
| TwoOrganizationIntegrationTests | Pass as part of full suite | In-process custom host, synthetic identities, EF InMemory control plane and one shared tenant DB |
| Official legal/regulatory research | Maryland statute/regulation, HHS, Nigerian Act publication and regulator sources consulted | Some NDPC pages/final-GAID PDF returned 403; see sources/limitations |

No live HomeLab or production database was accessed, no database update was applied, no destructive Compose commands were used, and no source changes, commits, or PRs were made. Build artifacts are ignored verification outputs.

## Findings

### Critical

#### C1 — Host-bound authentication does not isolate operational data

- **Location:** `Infrastructure/DependencyInjection.cs:86–111`; `Application/Auth/Sprint4ObjectAuthorizationService.cs:27–31`; `Application.Tests/Integration/ControlPlaneTestHost.cs:92,122–126`.
- **Evidence: Read + Ran tests.** DI captures one configured tenant connection string; the resolved organization is not used to choose `CarePathDbContext`. The object guard immediately approves Admin/Coordinator without a tenant precondition. The integration host gives both organizations one tenant database. Its probe demonstrates host/token binding, not isolation of patient records in two operational databases.
- **Impact:** Both markets. If unrelated organizations are admitted to this deployment, an appropriately privileged user at either own host can operate over the same data. A caregiver's reach remains constrained by assignment checks, but those records and profiles are still shared. Cross-host 401 is insufficient proof.
- **Fix:** Until CP-06, enforce a configured single operational organization for all PHI endpoints, allowing other organizations only control-plane/synthetic scenarios. Implement organization-bound scoped context creation, least-privilege secrets, independent database identity/region/schema admission, and no default-DB fallback. Add two distinct real data-plane databases and cross-ID API tests for every role, including Admin. Include files, jobs, cache and restore identity tests before enabling their tenant use.

### High

#### H1 — Membership changes do not invalidate existing access tokens

- **Location:** `WebApi/Security/AuthenticationServiceCollectionExtensions.cs:32–40,89–116`; `Application/Organization/Services/OrganizationMembershipService.cs:215–237`; `Infrastructure/Auth/JwtTokenService.cs:83–99`; `Application.Tests/Integration/TwoOrganizationIntegrationTests.cs:178–190`.
- **Evidence: Read + Ran.** Validation checks token kind, host organization and a parseable membership ID; it never loads authoritative membership/user state or compares `SecurityVersion`. The passing test explicitly expects HTTP 200 after deactivation. The default token lifetime is 60 minutes. Org suspension is separately enforced and works; it is not member revocation.
- **Impact:** Both. Removed staff retain access, downgraded admins retain old role claims, and stolen tokens survive local sign-out until expiry. Platform operator removal also lacks an authoritative request-time check.
- **Fix:** Finish CP-05 before real use: bind subject, membership, organization, active state, role/security version and session epoch on protected requests. Include platform identity revocation and fail-closed outages. Add next-request denial tests for downgrade, deactivation, password/session revocation, and multiple API instances. Short lifetimes reduce exposure but do not implement revocation.

#### H2 — PHI audits are lossy operational logs, with misleading operation semantics

- **Location:** `Infrastructure/DependencyInjection.cs:121`; `Infrastructure/Audit/LoggingPhiAuditLogger.cs:29–47`; `Application/Auth/IdorGuard.cs:41–44,83–91`; `Application/Clients/Services/ClientOperationsService.cs:338`.
- **Evidence: Read.** The registered PHI audit implementation calls `ILogger.LogInformation` then completes. It does not persist the entry's event timestamp or background-job identity explicitly. There is no demonstrated retention, immutability, durable acknowledgment, or recovery-independent preservation for this sink. The guard emits Create/Update/Delete before the business operation succeeds; service reads also emit duplicate success entries.
- **Impact:** Both. Logging settings/rotation can erase the only trail; reviewers can mistake an authorized attempt for a committed clinical change. Identifier-linked logs also remain sensitive. The control-plane append-only logger is a better pattern but does not solve tenant PHI auditing.
- **Fix:** Provide a durable tenant audit/outbox and independently preserved restricted sink, with event time, actor, organization, action, outcome, and correlation. Decide one owner per successful logical read; distinguish authorization decisions, failed attempts and committed changes. Test exact event count, rollback, sink failure, retention and restore continuity. HHS requires mechanisms to record and examine system activity; this review does not assert that a particular storage technology is legally mandatory. [HHS audit protocol](https://www.hhs.gov/hipaa/for-professionals/compliance-enforcement/audit/protocol/index.html)

#### H3 — The "non-PHI control plane" assumption is contradicted by client identity links

- **Location:** `Infrastructure/ControlPlane/Identity/PlatformUser.cs:14–18`; `Infrastructure/ControlPlane/Bootstrap/ControlPlaneBootstrapper.cs:113`; `_specs/02-design/cp-04-control-plane-foundation.md:540`; `Domain.Tests/Platform/ControlPlaneEntityClassificationTests.cs:14–71`.
- **Evidence: Read.** Identity includes name/email/phone and membership links an identified person to an agency and Client role. The design acknowledges this but asks for "sensitive-but-non-clinical" acceptance. The test scans property names on six Domain types; it excludes Infrastructure `PlatformUser` and cannot reason about joins or free text. It is a schema regression guard, not classification proof.
- **Impact:** USA: identifiable care relationships may be PHI in the relevant covered-entity/business-associate context. Nigeria: personal/sensitive relationship information remains regulated even without diagnoses. A sign-off cannot make a legally protected relationship unprotected.
- **Fix:** Classify actual joined records and processing purposes with the compliance owner. Either govern this identity plane as potentially health-identifying data with appropriate contracts, region, audit and recovery controls, or keep patient/family identities region/tenant local and share workforce identity only. Add joined-data-flow classification tests and a review of operator access. HHS defines the protected electronic information by its context, not by column names. [HHS Security Rule summary](https://www.hhs.gov/hipaa/for-professionals/security/laws-regulations/index.html)

#### H4 — Scheduling eligibility checks race concurrent writes

- **Location:** `Application/Scheduling/Services/ShiftOperationsService.cs:65–97,101–149,362–387`; `Infrastructure/Persistence/Configurations/Scheduling/ShiftConfiguration.cs:56–61`; `Infrastructure/Persistence/Repositories/EfUnitOfWorkBase.cs:41`.
- **Evidence: Read + Inferred; not reproduced against SQL Server/PostgreSQL.** Both create/update check competing shifts before entering the write transaction. No interval exclusion constraint or shared caregiver lock is configured. Two coordinators can both observe an available caregiver and commit overlapping shifts. Row-versioning only the individual shift would not prevent two different inserted rows.
- **Impact:** Both. Missed care, invalid staffing commitments, financial disputes. Single-call rejection tests do not establish concurrent safety.
- **Fix:** Serialize reservation decisions by caregiver in the database and recheck inside the transaction; use provider-appropriate locks/serializable predicates or PostgreSQL exclusion constraints with a SQL Server equivalent. Add synchronized two-connection overlapping create/update tests. Keep one eligibility rule; fix atomicity around it. [Microsoft concurrency guidance](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)

#### H5 — Repeated payment submissions create duplicate settled payments

- **Location:** `Application/Billing/Services/BillingOperationsService.cs:290–325`; `Infrastructure/Persistence/Configurations/Billing/PaymentConfiguration.cs:19,35`.
- **Evidence: Read + Inferred.** Each request constructs a fresh Payment, immediately marks it Settled, and appends it. ReferenceNumber has a nonunique index; no request idempotency key is enforced. Repeating a request after a dropped response records the same receipt twice. Concurrent additions also compute invoice status from independently loaded child lists.
- **Impact:** Both; particularly relevant to intermittent-data retry design. This is ledger duplication, not evidence of duplicate gateway debits: no gateway was exercised.
- **Fix:** Add an organization/invoice-scoped idempotency contract and unique persistence constraint; return the original result for matching retries and reject mismatched reuse. Reconcile invoice status under appropriate concurrency control. Distinguish manually verified receipts from unverified external payment notifications; authenticate and deduplicate future provider callbacks. Test lost responses and parallel submissions.

#### H6 — Care-plan authorization still contradicts the clinician workflow

- **Location:** `WebApi/Controllers/CarePlansController.cs:28–33`; `Application/Auth/Sprint4ObjectAuthorizationService.cs:93–103`; `Application/Clients/Services/ClientOperationsService.cs:251,302,488–496`; `CODE_REVIEW_TODO.md:19–34`.
- **Evidence: Read.** Controller permits Clinician; object authorization can approve the clinical relationship; the service helper permits only Admin/Coordinator. Clinician create/update then fails. This is the previously documented duplicated-rule defect, still present.
- **Impact:** Both. Clinicians cannot complete an advertised clinical workflow and may rely on shared/admin credentials or manual workarounds.
- **Fix:** Make service enforcement delegate to the authoritative object rule, preserving defense at both entry points. Add clinician allowed/denied create and update tests, including audit and rollback. Do not simply remove the second enforcement point or broaden unrelated clinician access.

#### H7 — The supplied HomeLab deployment is not a production security boundary

- **Location:** `nginx/nginx.conf:10–12,31–44`; `docker-compose.yml:16–18,79–82`; `WebApi/appsettings.HomeLab.json:43–46`.
- **Evidence: Read.** nginx listens on HTTP and publishes 5240:80. PostgreSQL publishes 5432 on host interfaces. No TLS termination, network firewall, encrypted-volume/backup or restore arrangement is demonstrated here. Trusted proxy lists are empty. An external TLS edge might exist, but it was not provided or verified.
- **Impact:** Both. Without another verified edge, credentials/health traffic can cross the LAN unencrypted; proxy/HTTPS settings can also fail when deployed. A Raspberry Pi is useful for synthetic staging, not proof of production availability or safeguards.
- **Fix:** Before PHI use, document and test the actual TLS boundary, trusted proxy addresses and spoof resistance; restrict DB ingress and runtime credentials; prove encrypted storage/backups, monitoring, recovery and incident operations. Keep HomeLab synthetic unless that environment independently meets the chosen production requirements. Do not infer that `Encrypt=True` encrypts files/backups.

#### H8 — Country privacy/retention policy cannot be expressed or enforced

- **Location:** `Domain/Entities/Platform/Organization.cs:34–37`; `Application/Platform/Services/PlatformOrganizationService.cs:145–146`; `README.md:149`; `CLAUDE.md:201`; `AGENTS.md:178`.
- **Evidence: Read + official-source research.** Region is a mutable string; no runtime routing uses it. No country/jurisdiction policy, effective retention schedule, legal hold/disposition workflow, or rights-request process is demonstrated. The blanket "Maryland six years" assertion is inaccurate as a general legal rule.
- **Impact:** USA: the current §4-403 text has a seven-year default absent notice, with minor-specific rules; provider applicability matters. The home-health regulation separately uses a discharge-based five-year/minor rule and refers to the statute. Six years of HIPAA-required documentation is a different subject from medical-record retention. Nigeria: keeping everything forever via soft deletion is not a complete privacy lifecycle.
- **Fix:** Obtain a provider/license-specific retention matrix and model record category, start event, minority, legal hold and authorized destruction/export. Preserve existing records while reconciling conflicting rules. Make region changes a controlled migration/admission workflow; include identity, audit, files, backups and providers. Implement privacy operations with a designated owner rather than requiring every request to be self-service.
- **Sources:** [Maryland §4-403](https://mgaleg.maryland.gov/mgawebsite/Laws/StatuteText?article=ghg&section=4-403), [COMAR 10.07.05.15](https://regs.maryland.gov/us/md/exec/comar/10.07.05.15), [HHS medical-record retention FAQ](https://www.hhs.gov/hipaa/for-professionals/faq/does-hipaa-require-covered-entities-to-keep-medical-records-for-any-period/index.html). Exact applicability and effective-date history require legal verification for each business license.

#### H9 — Nigeria financial records have no currency identity

- **Location:** `Domain/Entities/Billing/Invoice.cs:73`; `Domain/Entities/Billing/Payment.cs:38`; `Domain/Entities/Scheduling/Shift.cs:78–85`; `CarePath.Contracts/Billing/InvoicePreviewRowDto.cs:23`; `CarePath.Web/Shared/BusinessFormatting.cs:11–15`; `CarePath.Web/Pages/Caregivers.razor:140`.
- **Evidence: Read.** Amounts/rates are decimal and described as USD; contracts lack currency. Shared business formatting hardcodes `$`, while another profile uses browser-culture `C` formatting. A Nigerian invoice can therefore be numerically valid but economically ambiguous.
- **Impact:** Nigeria launch blocker; also affects US agencies with foreign payments. Decimal arithmetic itself is appropriate for USD/NGN and is not the defect.
- **Fix:** Add immutable ISO currency to invoices, payments and rate snapshots, with explicit organization default and historical USD backfill only where justified. Validate same-currency allocation and rounding; format from currency plus locale. Support cash/bank-transfer receipts first; validate a local provider's settlement/callback requirements before integration. Keep FX explicit and out of scope for a single-currency pilot. Do not rename existing enum values or reinterpret old amounts in place.

### Medium

#### M1 — Organization time zone is metadata; scheduling follows the operator's device

- **Location:** `CarePath.Web/Pages/ShiftCreate.razor:272`; `CarePath.Web/Pages/Schedule.razor:359–360`; `CarePath.Web/Pages/TransitionsPlanDetail.razor:443`; `Domain/Entities/Platform/Organization.cs:34`.
- **Evidence: Read + Inferred.** Input conversions use `DateTimeKind.Local` / `ToUniversalTime`; organization DefaultTimeZone is stored/mapped but not consumed by these workflows. A Los Angeles coordinator scheduling a Lagos agency uses the wrong local basis if entering agency wall-clock time.
- **Impact:** Both, including cross-state US operations and DST ambiguity.
- **Fix:** Expose authorized organization locale/time-zone settings, label entry zones, convert using that zone with ambiguous/nonexistent-time handling, and retain UTC instants. Use DateOnly for actual civil dates where contract migration allows. Test Lagos, Maryland DST transitions, and remote coordinators.

#### M2 — Caregiver workflow cannot survive loss of connectivity

- **Location:** `Application/Scheduling/Services/ShiftOperationsService.cs:175–198`; `CarePath.Web/Auth/TokenAuthenticationStateProvider.cs:60–65,98–99`; `_specs/sprints/sprint-07-mobile-notifications-ai-hardening.md` (planned mobile/offline work).
- **Evidence: Read + Ran login shell only.** No MAUI app/offline queue is implemented. Check-in captures server receipt time as actual start, so an offline capture submitted later cannot preserve visit-time meaning without a new protocol. In-memory web sessions are intentionally ephemeral.
- **Impact:** Nigeria pilot blocker for offline field work; US rural/home-care visits also affected. Device prevalence, data budgets and acceptable outage duration remain customer-research questions.
- **Fix:** Specify a minimal encrypted local work packet/queue, per-user/organization partitions, capture and receipt timestamps, device clock/GPS provenance, conflict and supervisor correction rules, idempotency, expired-assignment handling and wipe policy. Test airplane mode, process death, revoked membership on reconnect and low-end Android memory. Do not silently mark unsynced actions complete. Use neutral SMS/voice fallback only after channel governance; USSD is an optional provider-dependent workflow, not an assumed secure clinical channel.

#### M3 — US employment/credential enums and account assumptions block local workflows

- **Location:** `Domain/Enumerations/EmploymentType.cs:7–22`; `Domain/Enumerations/CertificationType.cs:1–82`; `Domain/Entities/Identity/User.cs:38–42`; `Application/Clients/Validators/CreateClientRequestValidator.cs:12–13`.
- **Evidence: Read.** Employment has only W2Employee/Contractor1099. Credentials encode Maryland categories; address defaults to Maryland; each client requires an email. Phone validation is mostly nonempty/max-length, **not a demonstrated US-only regex**. The prompt's claimed Money/PhoneNumber value objects are not implemented in the Domain file inventory reviewed.
- **Impact:** Nigeria. Relatives may coordinate/pay without the patient having email; local professional registration cannot safely be inferred from a US RN label. US worker status is also not established merely by assigning a 1099 enum.
- **Fix:** Separate workforce engagement from country tax classification; add jurisdiction/issuer/license/scope/verification state to credential rules. Include Nigeria RN/RM and other practitioner types only with approved clinical scope. Add country/region/LGA/postal/landmark address support and E.164 normalization with country context; verify number ownership separately. Decouple patient records from mandatory login/email using explicit family/proxy grants. Preserve legacy enum numeric contracts during migration. [NMCN licensing](https://www.nmcn.gov.ng/licensing.html), [NMCN verification portal](https://local-verification.nmcn.gov.ng/).

#### M4 — Creating memberships does not require the existing identity holder's acceptance

- **Location:** `Application/Organization/Services/OrganizationMembershipService.cs:87–109,263–274`; `_specs/02-design/cp-04-control-plane-foundation.md:619`.
- **Evidence: Read.** An agency Admin can attach an existing platform identity by email immediately. The submitted temporary password does not prove control of an existing account. The resulting response also includes the stable platform-linked user identifier. This is not evidence the admin learns the existing password or can impersonate that user.
- **Impact:** Both. Unwanted affiliations, inaccurate access-review records and cross-agency correlation; patient identity links make it more sensitive.
- **Fix:** Pending invitations, expiry, signed one-use acceptance tied to verified identity, invitation audit and revocation. Keep authorization inactive until acceptance and permitted provisioning complete. Continue uniform response semantics and avoid exposing global subject IDs when a membership ID suffices.

#### M5 — Last-active-Admin protection is a race, not a database invariant

- **Location:** `Application/Organization/Services/OrganizationMembershipService.cs:209–217,229–237,369–383`; `Infrastructure/ControlPlane/Persistence/Configurations/OrganizationMembershipConfiguration.cs:32`.
- **Evidence: Read + Inferred; no two-connection reproduction.** Each change counts admins and saves separately. Two admins can concurrently see a count of two and deactivate/demote different memberships, leaving none. SecurityVersion is not configured as a concurrency token; a token on individual rows alone would not stop this cross-row race.
- **Impact:** Both; agency lockout and manual operator intervention.
- **Fix:** Serialize administrative membership changes on an organization row or use a serializable transaction around the count and update. Add synchronized tests against each supported relational provider, with safe audited recovery.

#### M6 — Connection-string separation check compares text rather than database identity

- **Location:** `Infrastructure/ControlPlane/ControlPlaneServiceCollectionExtensions.cs:167–182`.
- **Evidence: Read.** Removing whitespace/lowercasing does not canonicalize key ordering, SQL Server synonyms, different credentials or optional parameters. Two strings targeting the same server/catalog can pass the inequality check. The README's claim that startup refuses the same database is stronger than this guard.
- **Impact:** Both; configuration mistakes can collapse identity/clinical separation and migration ownership.
- **Fix:** Parse with provider builders and compare canonical server/database identity; independently verify database markers and use separate DB grants. Resolve aliases in deployment validation where textual canonicalization cannot. Test reordered keys, synonyms, different users and equivalent targets; never print connection secrets.

#### M7 — Fixed absolute API hosts defeat per-agency web routing

- **Location:** `CarePath.Web/Program.cs:17–31`; `CarePath.Web/wwwroot/appsettings.Production.json:3`; `CarePath.Web/wwwroot/appsettings.HomeLab.json:3`.
- **Evidence: Read.** Code supports same-origin, but shipped environment settings select one absolute API host. Navigating an agency-specific web host still resolves branding/auth against that common API host, as D-CP04-14 acknowledges. Production currently names a shared Azure hostname.
- **Impact:** Both; incorrect agency context or failed login as a second agency is introduced. It does not bypass server token binding.
- **Fix:** Package same-origin API routing at each verified agency host, including CORS/TLS deployment tests; test two browser entry URLs resolve distinct branding and token organizations. Explicit development overrides may remain. Branding support alone is not a complete white-label product.

#### M8 — Refresh rotation needs session-family, atomicity and outage hardening

- **Location:** `Infrastructure/ControlPlane/Auth/PlatformIdentityService.cs:144–188,196–205,217–226`; `_specs/sprints/sprint-08-tasks.md` deferred list.
- **Evidence: Read.** Conditional relational revoke prevents two winners, but revoke and replacement insert are separate operations without an enclosing atomic rotation transaction. Failure between them spends the client token without delivering a successor. Each rotation extends seven days with no absolute session limit; reuse revokes all active tokens for that membership rather than a distinct login family. Revoked-token handling also precedes binding validation.
- **Impact:** Both. Network/provider failures force sign-in; reuse of one old session can sign out other sessions. Access tokens still survive independently under H1. These are distinct from signature strength, which is not the issue.
- **Fix:** Add explicit session/family IDs, absolute lifetime, revocation and bounded retention cleanup; bind before family effects; transact compare-and-swap plus replacement insert. Define a secure retry policy for response loss rather than promising retry transparency. Test database failures, two connections, stale replay, cross-host replay and legitimate independent sessions.

#### M9 — Transitions has records and review, not a proven delivery/response service

- **Location:** `Application/Transitions/Services/TransitionsService.cs:477–524,551–599`; `_specs/sprints/sprint-07-mobile-notifications-ai-hardening.md`; `task.md` cross-sprint channel gates.
- **Evidence: Read.** Scheduling persists a Scheduled reminder; check-ins create escalation records. No working external delivery worker/on-call notification path was found in the reviewed Application/Infrastructure source. AI extraction is explicitly deterministic/stubbed. These are planned capabilities, not hidden completed integrations.
- **Impact:** Both. Selling "reminders and escalation" without operational distinction could create unsafe expectations. A coordinator queue is useful only with named coverage and follow-up procedures.
- **Fix:** Pilot with a staffed coordinator work queue, response targets, acknowledgement aging, downtime and emergency instructions. Preserve clinician approval and human contact decisions. Add an audited delivery outbox, consent/opt-out, authenticated callbacks, retries/deduplication and vendor agreements before sending. Localize recipient language and shared-phone behavior. Do not add autonomous clinical advice or emergency calls to fill the gap.

#### M10 — Test coverage and completion evidence overstate release assurance

- **Location:** `Application.Tests/Integration/ControlPlaneTestHost.cs:122–126`; `Application.Tests/Integration/TwoOrganizationIntegrationTests.cs:178–190`; `_specs/sprints/sprint-06-tasks.md:559–560`; `_specs/PROGRESS.md`; `task.md`.
- **Evidence: Read + Ran.** Green integration tests use InMemory and a custom pipeline, not full Program startup. They do not execute relational rotation's ExecuteUpdate path. Some other tests correctly use SQLite; the suite is not all InMemory. Sprint 6 is called complete while its normative bUnit/accessibility/browser-safety rows remain Pending. The TODO still lists real defects and stale completed work together.
- **Impact:** Both; misleading go/no-go decisions. 1,189 assertions/test cases are not measured code coverage, a penetration test or clinical validation.
- **Fix:** Keep fast unit tests; add production-provider integration gates, actual startup/bootstrap and upgrade tests, tenant clinical API tests and browser critical flows. Reconcile completion documents from command evidence, keeping genuinely deferred items open. Report coverage only after measurement.

### Low

#### L1 — Old model-length deviations remain unresolved

- **Location:** `Infrastructure/Persistence/Configurations/Identity/UserConfiguration.cs:31`; `Infrastructure/Persistence/Configurations/Billing/InvoiceConfiguration.cs:28,34–35`; `CODE_REVIEW_TODO.md` infrastructure section.
- **Evidence: Read.** Address remains 200 versus the documented 500; invoice number remains 50 versus 20; some audit actor columns remain 100 versus 256. The latter has a potential failure path when actor fallback is a long identity name. Wider invoice length alone is not harmful.
- **Impact:** Both; address write failures/spec drift and avoidable audit-update errors.
- **Fix:** Reconcile approved lengths and validators; apply forward migrations only after checking deployed schemas/data. Do not regenerate historical migrations or shrink populated columns casually.

#### L2 — Invoice list performs repeated child/profile queries

- **Location:** `Application/Billing/Services/BillingOperationsService.cs:341–353,425`.
- **Evidence: Read.** For each paged invoice, child collections and client/user details are loaded separately. Paging bounds the result but the round trips grow with page size.
- **Impact:** Both; higher latency/cost, especially across slow regional links. No load-test latency is claimed.
- **Fix:** Project/batch the exact summary data and retain one logical per-record audit. Measure query count and page latency before broader optimization; preserve no-tracking where appropriate.

#### L3 — Orientation and smoke-test documentation are materially stale

- **Location:** `README.md:11–14,124`; `Documentation/Architecture.md:39`; `Documentation/Postman/README.md:10–14,27–30`; `task.md` Current Known State.
- **Evidence: Read.** README still describes .NET/EF 9 and implemented projects as planned. The Postman collection/environment named in the README are absent in the reviewed directory; the documented database-drop command also omits the now-required context.
- **Impact:** Both; onboarding errors and misleading validation instructions.
- **Fix:** Separate current versus planned architecture, restore a synthetic-only smoke kit, and replace reset guidance with explicit disposable-database/context instructions. Link one current release evidence page instead of duplicating status narratives.

## Architecture and decisions

### Agreements

- **Agree with ADR 0003's primary boundary.** A shared workforce/control service with separate operational databases is sensible for tens of agencies, tenant-local recovery and eventual regional placement. It reduces ubiquitous row-filter dependence, but application routing and operations remain security-critical.
- **Agree with one application build, explicit memberships and one active organization per token.** Avoid per-customer forks. Keep branding subordinate to verified domain and access controls.
- **Agree with authoritative host resolution, no security-state cache by default, a separate platform role, private media defaults, and staged identity migration.** Unknown/disabled hosts fail closed; the tests demonstrate the intended generic bodies and untrusted-forwarded-host behavior. Empty trusted-proxy lists correctly do not mean trust everyone.
- **Agree with ADR 0002's total-shift margin definition and explicit clinician verification.** Preserve financial snapshot semantics and human accountability.
- **Clean Architecture is substantially observed.** Domain has no external project references; Application depends on Domain and Contracts; clients do not depend on server entities. Optional validator constructors and large service classes are maintainability concerns, not reasons for a rewrite. Existing central UTC/soft-delete conventions and restricted clinical foreign keys are useful controls.

### Decisions I disagree with

| Decision | Disagreement | Alternative |
|---|---|---|
| D-CP04-05: defer effective membership/token revocation while exposing membership controls | Acceptable only for synthetic development; unsafe for actual workforce offboarding | Make authoritative invalidation a production prerequisite, including platform sessions |
| "Non-PHI control plane" as a universal invariant | Joined patient identity and care affiliation contradict the label | Workforce-only shared plane or govern health-identifying identity regionally with equivalent safeguards |
| Treat tenant audit persistence as a later SaaS concern | Own-company patient data needs defensible audits too | Deliver durable audit before live single-agency use |
| Automatically attach existing identities without acceptance | Agency intent is not the identity holder's agreement | Pending, verified invitations and an explicit patient/proxy relationship model |
| USD and US tax/credential categories as universal domain facts | Bakes one market into contracts and eligibility | Explicit currency/jurisdiction policy with backward-compatible snapshots |
| A broad Sprint 7 combining mobile, notifications, AI/OCR, storage and hardening | Too many independent safety/vendor dependencies for one acceptance gate | Split a safe operational pilot from later AI and automated clinical channels |

### Ten and one hundred organizations

**At ten:** a managed pool/database fleet is feasible, but manual provisioning, one app-wide connection string, per-replica startup bootstrap, ad hoc migration execution and untested restores are not. Require a single idempotent provisioner/migrator with leases, tenant database markers, schema compatibility, private storage namespace, independent backups/audit and synthetic admission probes. Approve RPO/RTO and support ownership. Reject admission if any required asset is absent. No actual cost or throughput benchmark was performed.

**At one hundred:** measure connection-pool multiplication across replicas and tenant catalogs; cap per-tenant work and reserve control-plane capacity. Add placement/region and noisy-neighbor telemetry, canary fleet upgrades, retries with stop thresholds, independent restore drills and a practiced control-plane recovery protocol. The shared identity/control service remains a cross-agency availability and privacy boundary. The ADR already anticipates much of this; those paragraphs are requirements, not implemented capabilities.

Do not switch to shared-schema tenancy merely to reduce database count without measuring operator cost and customer economics. Conversely, the ADR's Azure SQL standard is a deployment choice, not demonstrated Nigerian market economics or residency compliance. Validate the smallest profitable regional deployment before making commitments.

## Country readiness and policy requirements

### United States/Maryland

HIPAA applicability follows the actual covered-entity/business-associate relationships, not the word "healthcare" in the product name. A vendor that maintains regulated ePHI can have obligations even without routinely viewing it. A signed BAA, risk analysis, workforce processes, access reviews and operational safeguards must accompany the engineering work. [HHS Security Rule summary](https://www.hhs.gov/hipaa/for-professionals/security/laws-regulations/index.html)

Minimum-necessary engineering is visible in summary/detail contracts, Admin financial endpoints, explicit assignment/proxy checks, PHI-safe generic errors and hiding durable media URLs. However, no full least-privilege role study, live audit review, break-glass procedure, encrypted backup/restore evidence, incident exercise or vendor BAA was verified. H1–H3 and H7–H8 prevent a production assurance claim.

Confirm which Maryland licenses cover home health, residential service agency work and facility staffing. Do not assume the cited home-health COMAR applies identically to all three. Check payer-specific electronic visit verification/claims needs before promising Medicaid reimbursement; the current PaymentMethod enum and GPS check-in are not proof of payer acceptance. Likewise, assigning facility workers a 1099 value does not establish lawful worker classification. These are validation tasks, not legal conclusions about the owner's businesses.

### Nigeria

The **NDPA 2023** applies to relevant Nigerian processing, including covered overseas processing. Required work includes lawful basis plus sensitive-health-data conditions, notices, DPIA, processor contracts, children's/proxy authority, access/correction/erasure-or-restriction handling, portability and objection workflows. DPO and major-importance registration duties require an explicit applicability assessment. Section 40 provides processor escalation and controller notification to the Commission within 72 hours where the statutory risk threshold is met; high-risk affected-person communication has a separate trigger. Sections 41–43 govern international-transfer grounds and safeguards: the Act is not a blanket command that every health database remain physically in Nigeria. Record the transfer mechanism, recipients and destination protections, including identity, telemetry, backups and remote support. [Official Gazette via NCC](https://old.ncc.gov.ng/documents/1193-nigeria-data-protection-act-2023/file)

NDPR-only checklists are insufficient. NDPC's official 2025 report states that GAID 2025 entered force on 19 September 2025. Its final PDF was discoverable but returned 403 during this review; article-specific interpretation, current thresholds, filing duties and any subsequent amendments are **not fully verified**. Have the designated Nigerian compliance owner review the final current instrument rather than treating an older draft as law. [NDPC 2025 annual report](https://ndpc.gov.ng/wp-content/uploads/2026/02/Print_NDPC-Annual-Report-2025-1.pdf), [final GAID publication](https://ndpc.gov.ng/wp-content/uploads/2025/03/NDP-ACT-GAID-2025-MARCH-20TH.pdf).

NHIA/HMO/state-scheme participation introduces provider eligibility, authorization, covered-service, tariff and claims requirements that cannot be represented by relabeling Medicaid as insurance. NHIA publishes operational guidelines, but the linked document could not be fully opened here; no NHIA accreditation or reimbursable home-care scope is asserted. Also verify National Health Act confidentiality/record duties, state facility licensing and applicable professional regulators for the precise services offered. [NHIA operational guidelines page](https://www.nhia.gov.ng/operational-guideline/)

### Configuration and model boundaries

| Concern | Current expression | Required design |
|---|---|---|
| Jurisdiction/service license | Mostly implicit Maryland | Versioned organization jurisdiction/service policy, including provider class and responsible legal owner |
| Data region | Editable `Organization.DataRegion` string | Provisioned/verified placement for DB, identity, audit, files, backups and subprocessors; migration workflow to change it |
| Retention | Soft delete + blanket prose | Record-class schedules, lawful basis, legal holds, verified export/destruction and offboarding |
| Identity and rights | Email/password and role memberships | Patient without login, accepted invitations, verified family authority, rights case register and accountable operator |
| Communications | Reminder channel/status records | Purpose/consent/opt-out/language, minimal content, provider/country delivery policy, signed callbacks and suppression |
| Money | Decimal USD assumptions | Currency snapshots + validated settlement rail; separate taxation and pricing policy |
| Time | UTC storage; device-local entry | Organization-zone input/output and DST validation; distinct civil dates |
| Credentials | US enum and expiry | Jurisdiction, issuer, professional scope and verification evidence; training separate from license |
| Address/phone | US defaults, loose strings | Country-aware address/contact validation without excluding local formats; ownership separate from syntax |

The existing layer boundaries can accommodate these changes. They cannot be accomplished safely as country-specific `if` statements scattered through Razor pages and validators.

## Five stakeholder scenario walkthroughs

These are reasoned scenarios, not observed interviews, clinical trials or MiroFish simulations.

| Persona and scenario | Code-grounded outcome | Pilot acceptance test |
|---|---|---|
| Nigerian home-care agency owner: register a patient whose daughter arranges/pays for care; bill in naira; verify a local nurse | Mandatory patient email, US workforce/credential categories and currency-free invoices create workarounds | Register patient without login, accept verified proxy, verify NMCN license evidence, schedule/bill/receipt entirely in NGN |
| Maryland compliance officer: dismiss a caregiver and investigate prior patient access | Member's existing JWT remains usable; PHI audit is operational logging; retention prose is unreliable | Offboarding denies next request across replicas; independent audit survives restore; approved provider/minor/hold retention matrix |
| Caregiver on low-end Android: check in during outage, write a note, reconnect after reassignment | No native/offline capture; server timestamps receipt as visit time; repeated writes lack general replay contract | Offline capture visibly pending, encrypted queue, reconnect reauthorization, idempotent sync, disputed-time review and no PHI in notifications |
| Platform operator: onboard agency two with correct region and independent restore | Registration/control-plane branding exists; operational routing/provisioning and recovery are absent | No Active/admitted tenant until DB marker/schema/region/files/backup/audit probes pass; restore agency two without agency one impact |
| Attacker with valid caregiver login for agency one: spoof X-Forwarded-Host, replay token at agency two, guess another patient's ID, keep token after dismissal | Existing tests reject cross-host tokens and untrusted forwarded hosts. Own-host object rules constrain unassigned records. Shared DB and stale membership remain material gaps; no two-real-DB clinical proof | Real API matrix over separate DBs; malformed/duplicate/trusted-proxy host cases; cross-ID generic denial; same-host post-revocation denial; zero unintended PHI queries |

Connectivity constraints, caregiver device budgets, willingness to use SMS/USSD, staffing practices, naira pricing, family sponsorship and willingness to pay are **hypotheses to validate**, not population statistics established by this review. Interview Nigerian agencies and caregivers in the actual launch city. Start with a bounded private-pay workflow; add NHIA claims only after verified demand/eligibility. Validate local bank transfer/cash reconciliation and provider settlement before introducing cross-border payer complexity.

## Previous-review closure and process assessment

`CODE_REVIEW_TODO.md` remains useful but mixes old and current evidence. The clinician care-plan denial still exists (H6); duplicate authorization rules and audit ownership remain unresolved (H2). Length deviations persist (L1). Ordered repository overloads and authentication middleware are present and should not be rediscovered as missing. The 68 passing web tests are useful, but Sprint 6's own browser/coverage acceptance tasks still say Pending; neither blanket "no tests" nor "sprint fully proven" is justified.

The team's strongest recurring lesson—one rule, multiple enforcement points—is still violated by clinical authorization. A second implementation of a rule is not additional security when it disagrees with the first. CP-04's two-phase writes and explicit deferrals show thoughtful engineering, but best-effort compensation and mirror reversion need persistent reconciliation states before the fleet grows. A comment that drift cannot happen is not proof across process crashes and two databases.

The process is producing valuable safety detail, but completion evidence often measures implementation slices rather than a customer operation. Branding/control-plane work is ahead of safe field work and operations recovery. Freeze broad feature expansion; tie each next task to one falsifiable pilot outcome. Keep the existing design system, human clinical approval and domain contracts; no architectural rewrite is recommended.

## Suggested next two sprints (proposal only)

The following is an embedded suggested sprint file and task board. **No additional sprint files were created**, as only this report was authorized for modification. IDs below are review-local placeholders; assign the next unused project-global TASK numbers after approval under ADR 0001. Owners are roles to assign, not assignments already made. Existing Sprint 7 remains unapproved/uncompleted by this proposal.

### Proposed sprint file: `sprint-09-authoritative-access-and-audit.md`

**Status:** Proposed — not approved.  
**Goal:** Make a single-agency PHI pilot defensible and finish CP-05's security boundary.  
**Depends on:** Classification resolution and HomeLab backfill verification; approved provider/market scope.  
**Out of scope:** Native app, AI provider integration, external reminder delivery, onboarding unrelated production agencies.

#### Decisions

- **D-S9-1:** All live PHI access requires authoritative membership/session validation; explicit single-operational-organization admission until CP-06 is proven.
- **D-S9-2:** Decide the patient identity boundary from joined-data classification; no "non-PHI by sign-off" shortcut.
- **D-S9-3:** Durable audit owns committed changes and logical reads; authorization attempts are separate events with outcomes. Independent audit preservation is a release gate.
- **D-S9-4:** Collapse duplicated clinical access rules; retain service-level enforcement using the same rule source.
- **D-S9-5:** Provider integration tests accompany concurrency/idempotency fixes. Preserve legacy Identity until verified copy/rollback and authorized forward drop; do not prioritize dropping tables over working revocation.

#### Task board

| ID (temporary) | Task / primary files | Owner | Depends on | Status / evidence required |
|---|---|---|---|---|
| S9-P01 | Approve launch scope, identity classification, RPO/RTO and retention owner | Product + Compliance | — | Proposed; signed decisions and current legal matrix |
| S9-P02 | CP-05 authoritative subject/membership/version/session checks; platform revocation; single-agency gate (`WebApi/Security`, `Application/Auth`) | Backend | P01 | Proposed; next-request denial across two instances and outage tests |
| S9-P03 | Durable PHI audit/outbox, outcome model, read ownership (`Infrastructure/Audit`, `Application/Auth/IdorGuard.cs`) | Backend + Operations | P01 | Proposed; rollback/failure/exact-count/restore evidence |
| S9-P04 | Consolidate clinical authorization and fix clinician commands (`ClientOperationsService`, object guard) | Backend | P02 | Proposed; allowed/denied clinician create/update regression tests |
| S9-P05 | Membership invitation acceptance, atomic last-admin guard, refresh-family/rotation failure handling | Backend | P02 | Proposed; relational parallel/replay/fault tests |
| S9-P06 | Payment idempotency and atomic caregiver reservation | Backend | P03 | Proposed; two-connection tests on SQL Server and PostgreSQL |
| S9-P07 | TLS/DB ingress/secret/audit-backup runbook and deployment verification | Operations | P01–P03 | Proposed; synthetic restore and edge spoof tests |
| S9-P08 | Reconcile Sprint 6/TODO/docs; keyboard/PHI browser critical flows | Web + QA | P04–P07 | Proposed; actual browser results and explicit remaining gaps |

**Exit gate:** Zero-warning build; full tests; production-provider regressions; no stale-role access; durable audit; clinician workflow works; payment retry is safe; concurrent double booking blocked; only approved organization admitted; demonstrated secure synthetic deployment/restore. Real-patient pilot still requires organizational/legal acceptance. If capacity is insufficient, keep these blockers and reduce pilot scope; do not mark tasks Done on unit tests alone.

### Proposed sprint file: `sprint-10-tenant-routing-and-market-pilot.md`

**Status:** Proposed — not approved.  
**Goal:** Demonstrate CP-06 isolation and one commercially coherent market path.  
**Depends on:** Sprint 9 exit and approved regional/currency/identity design.  
**Out of scope:** General availability, full native offline app, automatic emergency contact, broad AI/OCR and multi-country payer integration.

#### Decisions

- **D-S10-1:** CP-06 admission verifies database identity, deployment/region/schema and least-privilege secret on each relevant connection lifecycle; no fallback data plane.
- **D-S10-2:** Same-origin verified agency hosts route web/API together. Mutable region metadata never moves real data by itself.
- **D-S10-3:** Product chooses one pilot track: Maryland private-pay operations or Nigeria private-pay operations. Country contract changes are explicit and preserve historical records.
- **D-S10-4:** Nigeria track requires NGN/country credentials/patient-without-login plus an offline protocol proof; a field pilot waits for actual device implementation and outage tests. US track requires provider-specific retention, onboarding/offboarding and visit/billing acceptance. Neither track claims payer accreditation.
- **D-S10-5:** Synthetic fleet/restore evidence precedes unrelated production-agency onboarding. External messaging remains gated until consent, provider contracts and delivery reliability are proved.

#### Task board

| ID (temporary) | Task / primary files | Owner | Depends on | Status / evidence required |
|---|---|---|---|---|
| S10-P01 | Scoped data-plane locator/context factory and DB-marker admission (`Infrastructure/Persistence`, `ControlPlane`, `OrganizationDataPlane`) | Backend | S9 | Proposed; swapped locator, stale secret, missing marker/schema and parallel-request denial |
| S10-P02 | Provisioning/migration lease, compatibility gate and independent restore runbook | Operations + Backend | P01 | Proposed; provision/upgrade/fail/restore synthetic agencies without cross-impact |
| S10-P03 | Two real-DB API security matrix including files/audit namespaces; same-origin two-host browser test | QA + Web | P01–P02 | Proposed; guessed IDs, revoked roles, platform denial and zero wrong-DB queries |
| S10-P04 | Pilot jurisdiction/currency/time-zone contracts and migration plan (`Domain`, `CarePath.Contracts`, Web formatting/input) | Product + Backend + Web | S9-P01 | Proposed; immutable historical currency, Lagos/Maryland/remote-device time tests |
| S10-P05 | Customer workflow validation; country credentials/contact/proxy gaps for selected pilot | Product + Clinical owner | P04 | Proposed; interviews and accepted end-to-end scenario, not simulated market demand |
| S10-P06 | Offline capture/idempotent sync protocol and low-end Android proof, if Nigeria track | Mobile + QA | P04–P05 | Proposed; encrypted minimal synthetic packet, crash/reconnect/revocation outcomes; field release remains blocked if incomplete |
| S10-P07 | Country privacy/rights/breach/vendor checklist with named owners; messaging go/no-go | Compliance + Operations | P02,P05 | Proposed; rights case and incident tabletop; approved transfer/retention policy |
| S10-P08 | Pilot go/no-go, cost/connection budget, explicit remaining backlog | Product + Engineering + Compliance | P03–P07 | Proposed; evidence-backed decision and support coverage |

**Exit gate:** Demonstrated tenant isolation/recovery, complete selected pilot workflow, measured operational budget and signed market-specific acceptance. At most a tightly controlled pilot; not simultaneous Nigeria/US general availability. If CP-06 consumes the sprint, defer the pilot and mobile work rather than diluting isolation evidence.

## Did not verify / remaining evidence

- MiroFish plugin execution, graph/persona simulation or interviews; manual scenarios are substitutes only for reasoning, not tool completion.
- Exhaustive line-by-line review of all historical specs, generated migration designers, every Razor screen or all dependency source code.
- Standalone API/Swagger authentication and live Postman smoke run: API startup failed on macOS LocalDB; named Postman JSON files are absent from this branch's directory.
- Real SQL Server/PostgreSQL migration application, two-organization clinical database isolation, relational refresh concurrency, locking races, or real provider constraints. SQLite/InMemory tests are not equivalents.
- Live HomeLab deployment, actual external TLS edge/firewall, production Azure settings, encryption at rest, key-ring encryption, least-privilege runtime DB grants, backups, restore drills, recovery times or retention/legal-hold enforcement.
- BAAs/DPAs, exact legal entity/license status, employee/contractor classification, customer contracts, professional credential authenticity, local/state licensing, payer/EVV/NHIA acceptance or clinical service accreditation.
- Full final GAID text/current subsidiary amendments and threshold interpretation; several regulator pages/PDFs were blocked. No blanket Nigerian localization mandate or exact NHIA coverage is asserted.
- Full browser accessibility/PHI review, authenticated staff workflows, low-end Android/native MAUI, offline/cellular/GPS, SMS/voice/USSD, real payment rails, AI/OCR providers or clinical efficacy.
- Penetration test, secrets/history scan, image/OS/JavaScript vulnerability scan, load test, measured coverage percentage, vendor prices, willingness to pay, regional hosting costs or cost-per-agency figures.

## Sources and reproducibility

Official sources consulted on 2026-10-22; linked statutes/guidance should be rechecked at implementation. Regulatory text informs engineering findings and is not a certification of either business.

- [HHS Security Rule summary](https://www.hhs.gov/hipaa/for-professionals/security/laws-regulations/index.html)
- [HHS audit protocol: audit controls and documentation retention](https://www.hhs.gov/hipaa/for-professionals/compliance-enforcement/audit/protocol/index.html)
- [HHS medical-record retention FAQ](https://www.hhs.gov/hipaa/for-professionals/faq/does-hipaa-require-covered-entities-to-keep-medical-records-for-any-period/index.html)
- [Maryland Health-General §4-403](https://mgaleg.maryland.gov/mgawebsite/Laws/StatuteText?article=ghg&section=4-403)
- [Maryland COMAR 10.07.05.15](https://regs.maryland.gov/us/md/exec/comar/10.07.05.15)
- [NDPA 2023 Official Gazette, NCC-hosted publication](https://old.ncc.gov.ng/documents/1193-nigeria-data-protection-act-2023/file)
- [NDPC 2025 annual report](https://ndpc.gov.ng/wp-content/uploads/2026/02/Print_NDPC-Annual-Report-2025-1.pdf)
- [Final GAID 2025 publication; full fetch blocked](https://ndpc.gov.ng/wp-content/uploads/2025/03/NDP-ACT-GAID-2025-MARCH-20TH.pdf)
- [NHIA operational guidelines landing page](https://www.nhia.gov.ng/operational-guideline/)
- [NMCN licensing](https://www.nmcn.gov.ng/licensing.html) and [verification](https://local-verification.nmcn.gov.ng/)
- [Microsoft EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency) and [transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions)

Local command logs outside the repository: `/tmp/carepath-review-build.log`, `/tmp/carepath-review-tests.log`, `/tmp/carepath-review-vulnerabilities.log`, `/tmp/carepath-review-api.log`, `/tmp/carepath-review-web.log`. These logs are ephemeral; the results above are preserved in this report. Repository-relative findings refer to the immutable reviewed commit, not suggested future line numbers.
