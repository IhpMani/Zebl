# Backend Refactor Plan: Audit & Data Integrity

Structured plan from full backend scan. Constraints: no DB schema changes (no defaults, triggers, migrations), no per-controller audit logic, no full rewrite. Safe global audit changes have been implemented; everything else is documented as TODOs with file paths and risk levels.

---

## Section 1: What Was Detected (facts only)

### Global audit
- All 12 main entities (Claim, Service_Line, Patient, Payment, Payer, Physician, Adjustment, Disbursement, Insured, Claim_Insured, Patient_Insured, Procedure_Code) have EZClaim-style audit columns: `*DateTimeCreated`, `*DateTimeModified`, `*CreatedUserGUID`, `*LastUserGUID`, `*CreatedUserName`, `*LastUserName`, `*CreatedComputerName`, `*LastComputerName`.
- No `SaveChanges` / `SaveChangesAsync` override existed; no interceptors; no shared logic to set these fields.
- Manual audit only in: **PhysiciansController** (POST set PhyDateTimeCreated/Modified; PUT set PhyDateTimeModified) and **Hl7ImportService** (set Pat/Cla/Srv/Pay/ClaIns DateTimeCreated/Modified and claim.ClaDateTimeModified in UpdateClaimTotals). User/computer names and GUIDs were never set anywhere.

### DbContext and entities
- Single context: `Zebl.Infrastructure.Persistence.Context.ZeblDbContext`. Twelve `DbSet`s: Adjustments, Claims, Claim_Insureds, Disbursements, Insureds, Patients, Patient_Insureds, Payers, Payments, Physicians, Procedure_Codes, Service_Lines.
- Entity **Hl7_Import_Log** exists in `Zebl.Infrastructure.Persistence.Entities.Hl7_Import_Log` but has **no DbSet and no mapping** in ZeblDbContext. Any code persisting to this table would fail at runtime (invalid object name or missing configuration).
- Adjustment has `AdjTaskFID` and FK `FK_Adjustment_TaskSrv`; in the model this is mapped to **Service_Line** (AdjTaskF navigation to Service_Line). So there is no separate Task/TaskSrv entity; the “phantom” is only the constraint name, not a missing table.

### Write boundaries and data risks
- **Writes:** Only **PhysiciansController** (POST/PUT) and **Hl7ImportController** → **Hl7ImportService** perform persistence. All other controllers are read-only.
- **Duplicate data:** PhysiciansController already checks duplicate NPI on create/update and returns Conflict. No backend deduplication for existing rows (e.g. merge or flagging duplicates).
- **Idempotency:** HL7 import creates Patient/Claim/Service_Lines per message with per-message transactions; duplicate detection is by Patient + DOS + Visit. No shared idempotency key or “import run” abstraction beyond the existing logic.
- **Write logic split:** No aggregate boundaries; controllers and Hl7ImportService both use ZeblDbContext directly. No application services for claim/patient/payment, etc.

### Cross-cutting
- Optional JWT, global exception middleware, rate limiting, Serilog, health checks are present. No validation pipeline (e.g. FluentValidation), no global transaction scope beyond HL7’s own transactions.

---

## Section 2: What Was Auto-Fixed (with file list)

Only safe, global audit changes were implemented. No schema, no per-controller audit, no behavior change to HL7 import flow other than audit now being applied centrally.

### Files created
| File | Purpose |
|------|--------|
| `Zebl.Application/Abstractions/IAuditableEntity.cs` | Interface for entities that support SetCreated/SetModified (used by DbContext in SaveChanges). |
| `Zebl.Application/Abstractions/ICurrentUserContext.cs` | Interface for current user/computer (UserId, UserName, ComputerName). |
| `Zebl.Api/Services/SystemCurrentUserContext.cs` | Temporary implementation: returns fixed SYSTEM Guid, "SYSTEM" for UserName and ComputerName. |
| `Zebl.Infrastructure/Persistence/Entities/Claim.Audit.cs` | Partial Claim : IAuditableEntity with SetCreated/SetModified mapping to Cla* audit fields. |
| `Zebl.Infrastructure/Persistence/Entities/Service_Line.Audit.cs` | Same for Service_Line (Srv*). |
| `Zebl.Infrastructure/Persistence/Entities/Patient.Audit.cs` | Same for Patient (Pat*). |
| `Zebl.Infrastructure/Persistence/Entities/Payment.Audit.cs` | Same for Payment (Pmt*). |
| `Zebl.Infrastructure/Persistence/Entities/Payer.Audit.cs` | Same for Payer (Pay*). |
| `Zebl.Infrastructure/Persistence/Entities/Physician.Audit.cs` | Same for Physician (Phy*). |
| `Zebl.Infrastructure/Persistence/Entities/Adjustment.Audit.cs` | Same for Adjustment (Adj*). |
| `Zebl.Infrastructure/Persistence/Entities/Disbursement.Audit.cs` | Same for Disbursement (Disb*). |
| `Zebl.Infrastructure/Persistence/Entities/Insured.Audit.cs` | Same for Insured (Ins*). |
| `Zebl.Infrastructure/Persistence/Entities/Claim_Insured.Audit.cs` | Same for Claim_Insured (ClaIns*). |
| `Zebl.Infrastructure/Persistence/Entities/Patient_Insured.Audit.cs` | Same for Patient_Insured (PatIns*). |
| `Zebl.Infrastructure/Persistence/Entities/Procedure_Code.Audit.cs` | Same for Procedure_Code (Proc*). |

### Files modified
| File | Change |
|------|--------|
| `Zebl.Infrastructure/Persistence/Context/ZeblDbContext.cs` | Injected `ICurrentUserContext` (optional second ctor); overrode `SaveChanges()` and `SaveChangesAsync()` to call `ApplyAudit()` before base; `ApplyAudit()` sets Created/Modified on all Added/Modified entries that implement `IAuditableEntity` using current user context. |
| `Zebl.Api/Program.cs` | Registered `ICurrentUserContext` → `SystemCurrentUserContext` (Scoped) in Services region. |
| `Zebl.Api/Controllers/PhysiciansController.cs` | Removed manual `PhyDateTimeCreated`/`PhyDateTimeModified` on POST and `PhyDateTimeModified` on PUT; audit is now applied by DbContext. |
| `Zebl.Api/Services/Hl7ImportService.cs` | Removed all manual assignment of Pat/Cla/Srv/Pay/ClaIns DateTimeCreated/DateTimeModified and removed `claim.ClaDateTimeModified = DateTime.UtcNow` in UpdateClaimTotals; added comment that global audit sets it on SaveChanges. |

### Behavior preserved
- DB-first: no entity or table renames; only partial classes and context overrides added.
- HL7 import: same flow (Patient → Claim → Service_Lines → Claim_Insured → UpdateClaimTotals); each `SaveChangesAsync` now runs `ApplyAudit()` so new/updated entities get SYSTEM audit. No change to deduplication or transaction boundaries.
- When DbContext is constructed without `ICurrentUserContext` (e.g. design-time), `_userContext` is null and `ApplyAudit()` is a no-op.

---

## Section 3: What MUST Be Done Next (ordered, with risk level)

### 1. Register Hl7_Import_Log in DbContext (if the table is used)
- **Risk: Low.**  
- **Action:** If the app or future code should persist HL7 import log rows, add `DbSet<Hl7_Import_Log> Hl7_Import_Logs` to ZeblDbContext and configure the entity in `OnModelCreating` (table name, key, properties). If the table does not exist in the DB, create it via a migration only when you are allowed to change schema; otherwise remove or avoid any code that writes to it.  
- **TODO (exact path):** `Backend/Zebl.Api/Zebl.Infrastructure/Persistence/Context/ZeblDbContext.cs` — add DbSet and mapping for `Hl7_Import_Log` **or** remove references to writing Hl7_Import_Log from code and document that logging is out of scope until the table exists and is mapped.  
- **Reasoning:** Entity exists in code; context does not. Either make it consistent or stop using it.

### 2. Replace SYSTEM with real user context when auth is wired
- **Risk: Low.**  
- **Action:** Implement `ICurrentUserContext` (e.g. in API or a shared auth assembly) that reads UserId/UserName/ComputerName from HttpContext (claims, or server name). Register it in DI instead of `SystemCurrentUserContext` (or keep SYSTEM as fallback when no user).  
- **TODO (exact path):** `Backend/Zebl.Api/Zebl.Api/Services/SystemCurrentUserContext.cs` — keep as default; add e.g. `JwtCurrentUserContext` and swap registration in `Backend/Zebl.Api/Zebl.Api/Program.cs` when JWT is the source of truth.  
- **Reasoning:** Audit columns will then reflect real users; no schema or DB change.

### 3. Physician duplicate NPI in existing data
- **Risk: Medium.**  
- **Action:** Decide policy: allow multiple rows with same NPI (current DB allows it) or enforce single NPI per physician. If enforcing: add a unique index on NPI (schema change) **or** application-level checks and a one-time cleanup/merge script. Do not auto-fix data without human confirmation.  
- **TODO (exact path):** `Backend/Zebl.Api/Zebl.Api/Controllers/PhysiciansController.cs` — already prevents new duplicates on create/update. For existing duplicates: run a report (e.g. new GET or script) listing PhyID + PhyNPI with count > 1; then perform merge/archive in a separate, reviewed process.  
- **Reasoning:** Prevents accidental data loss; duplicate handling is business policy.

### 4. Optional: Claim Note entity and API
- **Risk: Low (additive).**  
- **Action:** If EZClaim DBML has a Claim Note (or notes) table and the front-end “Claim Note” list should persist to DB, add the entity (DB-first from existing table or skip if table does not exist), add DbSet and mapping, and add a minimal API (list by claim, create/update if required).  
- **TODO (exact path):** None in current backend; add under `Zebl.Infrastructure.Persistence.Entities` and a controller under `Zebl.Api.Controllers` only if the table exists and product requires it.  
- **Reasoning:** Currently no Claim Note entity; front-end may be mock or future work.

### 5. Optional: Write boundaries and aggregate consistency
- **Risk: Medium (architectural).**  
- **Action:** Do not rewrite globally. If adding new write flows (e.g. claim create/update, patient update), consider application services that take DTOs and call DbContext, so that audit and transactions stay in one place. Leave existing Physicians and HL7 import as-is.  
- **TODO (exact path):** When adding a new write (e.g. `Backend/Zebl.Api/Zebl.Api/Controllers/ClaimsController.cs` or PatientsController), add a service in `Backend/Zebl.Api/Zebl.Api/Services/` (or a future Application layer) that uses ZeblDbContext and returns DTOs; controller only calls the service.  
- **Reasoning:** Keeps audit and transaction behavior consistent without a big-bang refactor.

### 6. Optional: Validation pipeline
- **Risk: Low.**  
- **Action:** Add FluentValidation (or similar) for request DTOs and register in pipeline so invalid requests never hit controller logic.  
- **TODO (exact path):** `Backend/Zebl.Api/Zebl.Api/Program.cs` — add validation middleware or filter; validators in `Zebl.Application` or API.  
- **Reasoning:** Cross-cutting; no DB or audit change.

---

## Summary

- **Done:** Centralized audit via `SaveChanges`/`SaveChangesAsync`, `IAuditableEntity` on all 12 entities (partials), `ICurrentUserContext` with SYSTEM, DI wiring, and removal of per-controller/per-service manual audit. DB-first and HL7 import behavior preserved.  
- **Next (required):** Resolve Hl7_Import_Log (add to context or remove usage).  
- **Next (recommended):** Replace SYSTEM with real user context when auth is ready; address existing physician NPI duplicates with a report and human-driven cleanup.  
- **Later (optional):** Claim Note entity/API if table exists; write boundaries for new features; validation pipeline.
