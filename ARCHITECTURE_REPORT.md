# Backend Architecture Report — Zebl API

Concise high-level summary of the backend solution (projects, DbContext, audit, controllers, cross-cutting concerns, and EZClaim alignment).

---

## 1. Projects and Responsibilities

| Project | Responsibilities |
|--------|-------------------|
| **Zebl.Api** | Web host, controllers, middleware (global exception, security headers), optional JWT auth, rate limiting, CORS, health checks (SQL + UI), Swagger, Serilog. Registers `ZeblDbContext`, `Hl7ParserService`, `Hl7ImportService`. |
| **Zebl.Application** | DTOs (Claims, Patients, Payments, Services, Adjustments, Disbursements, Physicians, Payers, Common), request/response and list/detail shapes. Static `RelatedColumnConfig` for list “related columns”. Holds **EF Core** package references (used by Infrastructure via project reference). No domain layer, no application services. |
| **Zebl.Infrastructure** | Persistence only: **ZeblDbContext** (single context), DB-first entities under `Persistence/Entities/`. References Application (for EF Core and possibly DTO usage). No repository abstraction. |

No separate domain or use-case layer; controllers and HL7 import service orchestrate logic and data access directly.

---

## 2. DbContext and DB-First Entity Usage

- **Single context:** `Zebl.Infrastructure.Persistence.Context.ZeblDbContext` (partial, `OnModelCreating` + `OnModelCreatingPartial`).
- **DbSets:** `Adjustments`, `Claims`, `Claim_Insureds`, `Disbursements`, `Insureds`, `Patients`, `Patient_Insureds`, `Payers`, `Payments`, `Physicians`, `Procedure_Codes`, `Service_Lines`. All mapped with keys, FKs, and column config (string lengths, `money`, etc.) in `OnModelCreating`.
- **DB-first usage:** Entities are partial classes in `Persistence/Entities/` with EZClaim-style names (e.g. `ClaID`, `ClaPatFID`, `SrvClaFID`). Used directly by controllers and `Hl7ImportService`; no repository or unit-of-work abstraction. Read paths typically use `AsNoTracking()`.
- **Entity not in context:** `Hl7_Import_Log` entity exists in `Persistence/Entities/` but **there is no `DbSet<Hl7_Import_Log>`** (and no mapping) in `ZeblDbContext`. Any code that tries to persist to `Hl7_Import_Log` will fail at runtime (e.g. “Invalid object name 'Hl7_Import_Log'” if the table exists but isn’t mapped, or similar). Current HL7 import flow does not use this entity in the active code path; the log file references are from an older controller design.

---

## 3. Audit Handling (Created/Modified)

- **Schema:** Entities carry EZClaim-style audit fields: `*DateTimeCreated`, `*DateTimeModified`, and often `*CreatedUserGUID`, `*CreatedUserName`, `*CreatedComputerName`; `Claim` also has `ClaLastUserGUID`, `ClaLastUserName`, `ClaLastComputerName`. Same pattern appears on Claim, Service_Line, Patient, Payment, Payer, Physician, Adjustment, Disbursement, Insured, Claim_Insured, Patient_Insured, Procedure_Code.
- **No central audit:** `ZeblDbContext` has **no `SaveChanges`/`SaveChangesAsync` override**. No interceptors or base logic to set Created/Modified or user/computer names.
- **Manual audit:**
  - **PhysiciansController (POST/PUT):** Sets `PhyDateTimeCreated`/`PhyDateTimeModified` (and on update only `PhyDateTimeModified`) explicitly.
  - **Hl7ImportService:** Sets `PatDateTimeCreated`/`PatDateTimeModified`, `ClaDateTimeCreated`/`ClaDateTimeModified`, `SrvDateTimeCreated`/`SrvDateTimeModified`, `PayDateTimeCreated`/`PayDateTimeModified`, `ClaInsDateTimeCreated`/`ClaInsDateTimeModified`, and `claim.ClaDateTimeModified` in `UpdateClaimTotals`. No setting of `*CreatedUserGUID`/`*LastUserGUID` or computer/user names.
- **Read-only controllers:** Claims, Patients, Payers, Payments, Services, Adjustments, Disbursements only read data; they don’t set audit fields. Any future create/update there would need explicit audit handling or a shared strategy.

---

## 4. Controllers and Service Boundaries

- **Controllers:** AdjustmentsController, ClaimsController, DisbursementsController, Hl7ImportController, PatientsController, PayersController, PaymentsController, PhysiciansController, ServicesController. All inject `ZeblDbContext` (and logger) except Hl7ImportController, which uses `Hl7ParserService` and `Hl7ImportService` only.
- **Service boundary:** Only **HL7 import** is behind a dedicated service layer (`Hl7ImportService` + `Hl7ParserService`). All other features are **controller → DbContext → DTO**: list/detail endpoints build queries and map to Application DTOs inside the controller. No application services for claim, patient, payment, etc.
- **Write operations:** Only **PhysiciansController** (POST create, PUT update) and **Hl7ImportController** (POST import) perform writes. The rest are read-only (GET list + GET by id or claim-scoped lists).
- **Shared patterns:** List endpoints use query params (pagination, date filters, search, numeric min/max). Claim-scoped child data (services, payments, adjustments, disbursements) are exposed via dedicated actions (e.g. by claim id). Some controllers use raw SQL for “next id” (e.g. `SqlQueryRaw<int>`).

---

## 5. Missing or Inconsistent Cross-Cutting Concerns

- **Audit:** No global or base audit (no `SaveChanges` override, no interceptors). Audit is ad hoc in Physicians and HL7 import; user/GUID and computer name are never set.
- **Validation:** No FluentValidation or request-model validation pipeline mentioned; validation is likely inline or minimal.
- **Authorization:** JWT and `[Authorize(Policy = "RequireAuth")]` are present; no fine-grained resource/scope checks visible at controller level.
- **Logging:** Serilog is configured; controllers and HL7 services log. No structured “audit log” for data changes.
- **Error handling:** Global exception middleware returns consistent error DTOs and maps known exceptions (e.g. EF, SQL) to status codes. Good baseline; no domain-specific exception mapping.
- **Transaction:** Only HL7 import uses explicit transactions (per-message). Single-request, multi-entity updates elsewhere (e.g. future claim/patient writes) would need a defined transaction strategy.

---

## 6. Incomplete or Inconsistent vs EZClaim DBML

- **Hl7_Import_Log:** Entity exists; **not registered in ZeblDbContext** (no DbSet, no configuration). Either add the set and mapping and ensure the table exists, or remove use of this table from any code path.
- **Claim notes:** No dedicated “Claim Note” or “ClaimNote” entity or table in the solution; EZClaim often has claim notes (e.g. notes table or fields). Front-end “Claim Note” lists would need a corresponding API and entity if they are to persist to EZClaim-shaped schema.
- **Task / Adjustment:** `Adjustment` has `AdjTaskFID` and FK to “TaskSrv” (`FK_Adjustment_TaskSrv`). There is **no Task or TaskSrv entity/DbSet** in the context, so that relationship is only partially represented and may be broken or unused.
- **DB-first alignment:** Table and column names follow EZClaim conventions (Claim, Service_Line, Patient, etc.). Gaps are the missing DbSet for `Hl7_Import_Log`, missing Task/TaskSrv if required by EZClaim, and any EZClaim tables (e.g. notes, tasks) not yet present in the model.

---

## Summary Table

| Area | Status |
|------|--------|
| Projects | API (host + controllers + HL7 services), Application (DTOs + EF refs), Infrastructure (DbContext + entities). |
| DbContext | Single context; 12 DbSets; DB-first entities; no `Hl7_Import_Log` in context. |
| Audit | Schema has Created/Modified (and user/computer) fields; no central handling; only Physicians + HL7 set timestamps manually; user/GUID/computer never set. |
| Controllers | All use DbContext directly except HL7; only Physicians and HL7 perform writes. |
| Cross-cutting | Global exception + optional JWT + rate limit + CORS + health + Serilog; no global audit, no validation pipeline, no transaction strategy beyond HL7. |
| EZClaim | Aligned on core entities; inconsistent: `Hl7_Import_Log` not in context, no Claim Note entity, Task/TaskSrv referenced by Adjustment but not present in model. |
