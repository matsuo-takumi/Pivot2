# 📘 Global Specification: Application Documentation Structure (`docs/`, `logs/`, `tips/` Folder Standard)

---

## 1. Purpose

This document defines the **mandatory documentation and reference folder structure** required for all software projects.

Every repository—regardless of its size, purpose, or technology stack—**must include the following three top-level directories**:

1. `docs/` — Core architectural and implementation documentation
    
2. `logs/` — Historical and decision tracking documents
    
3. `tips/` — Knowledge-sharing notes, developer insights, and practical guides
    

The purpose of this standard is to ensure:

- **Consistency** in how teams organize technical knowledge
    
- **Traceability** of design decisions, code modifications, and development progress
    
- **Maintainability** by making system structures and reasoning transparent
    
- **AI-readability**, enabling tools like Cursor and Claude to fully utilize project documentation context
    

All developers must adhere to this standard when initializing or updating a repository.

---

## 2. Functional Requirements

|Category|Requirement|
|---|---|
|**Folder Creation**|Each repository **must include root-level directories named `docs/`, `logs/`, and `tips/`**.|
|**Documentation Format**|All files must be written in **Markdown (`.md`)** for readability and AI compatibility.|
|**Core Files (Mandatory)**|Inside `docs/`, the following files must exist: 1) `README.md` (Documentation index) 2) `Architecture.md` 3) `DirectoryStructure.md` 4) `TechStack.md` 5) `DataStructure.md` 6) `ImplementationTasks.md`|
|**Logs Folder Contents**|Inside `logs/`, the following files must exist: 1) `Changelog.md` — Tracks system-level and document-level changes. 2) `DecisionRecords.md` — Stores Architecture Decision Records (ADR). 3) `ModificationLog.md` — Records detailed change descriptions per feature, module, or document.|
|**Tips Folder Contents**|Inside `tips/`, developers should add optional but recommended `.md` files for guidance, debugging, or environment setup.|
|**Extensibility**|Additional `.md` files may be created within any folder to support project-specific requirements (e.g., `API.md`, `Testing.md`, `DesignSystem.md`).|
|**Version Control**|All documentation must be version-controlled via Git. Changes are reviewed as part of Pull Requests.|
|**AI Synchronization**|When using AI tools such as Cursor, Claude, or LangChain, these folders must be continuously synchronized with the latest project state using automated summarization or reflection workflows.|

---

## 3. Documentation Structure and Content Requirements

### 3.1 `docs/README.md` — Documentation Index

**Purpose:**  
Acts as the entry point for all documentation. Provides an overview, index, and quick navigation between `docs/`, `logs/`, and `tips/`.

**Required Sections:**

- Project summary
    
- Overview of available documentation
    
- Links to related logs and tips
    
- Notice to developers and AI agents (designate this as the “context root”)
    

**Example:**

```markdown
# 📚 Documentation Index

This folder contains all reference documentation for the project.

## 📖 Core Documents
| Document | Description |
|-----------|--------------|
| [Architecture.md](./Architecture.md) | System architecture overview |
| [DirectoryStructure.md](./DirectoryStructure.md) | Project folder and module organization |
| [TechStack.md](./TechStack.md) | Technologies and frameworks used |
| [DataStructure.md](./DataStructure.md) | Data schema and model definitions |
| [ImplementationTasks.md](./ImplementationTasks.md) | Phase-based implementation checklist |

## 🧾 Related Logs
| Log | Description |
|-----|--------------|
| [Changelog](../logs/Changelog.md) | System-wide and document updates |
| [Decision Records](../logs/DecisionRecords.md) | Technical and architectural decision logs |
| [Modification Log](../logs/ModificationLog.md) | Detailed change records |

## 💡 Developer Notes
| Tip File | Description |
|-----------|--------------|
| [EnvironmentSetup.md](../tips/EnvironmentSetup.md) | Local environment setup and configs |
| [DebuggingGuide.md](../tips/DebuggingGuide.md) | Common debugging techniques |
| [PerformanceOptimization.md](../tips/PerformanceOptimization.md) | Performance best practices |

> **Note:**  
> This README serves as the **primary AI context index** for tools such as Cursor and Claude.
```

---

### 3.2 `Architecture.md`

**Purpose:**  
Defines the high-level architecture and the structure of the entire system.

**Required Sections:**

- System overview and purpose
    
- Architecture diagram (if available)
    
- Major components and their roles (frontend, backend, database, services, etc.)
    
- Communication flow between modules
    
- Chosen architecture pattern (e.g., microservices, layered, MVC)
    
- Scalability, performance, and fault tolerance design strategies
    

---

### 3.3 `DirectoryStructure.md`

**Purpose:**  
Describes the purpose and content of each major directory within the project.

**Required Sections:**

- Complete directory tree (manual or auto-generated)
    
- Explanation of each directory
    
- Folder naming conventions and module addition rules
    

**Example:**

```markdown
src/ → main source code  
docs/ → project documentation  
logs/ → historical records  
tips/ → developer notes  
```

---

### 3.4 `TechStack.md`

**Purpose:**  
Documents the technical stack and rationale for chosen tools.

**Required Sections:**

- Programming languages and versions
    
- Frameworks and libraries
    
- Build tools and package managers
    
- CI/CD configuration tools
    
- Testing frameworks and coverage goals
    
- Hosting/infrastructure setup
    
- Justification for selected technologies
    

---

### 3.5 `DataStructure.md`

**Purpose:**  
Defines data models, entities, and schemas used throughout the application.

**Required Sections:**

- Database schema with ER diagram
    
- API input/output data structures
    
- Persistent vs. transient data
    
- Validation and serialization rules
    
- Example object/JSON representations
    
- Data flow between system layers
    

---

### 3.6 `ImplementationTasks.md`

**Purpose:**  
Tracks actionable development work, organized by **phases**, using Markdown checkboxes.  
This document acts as a **living ToDo list** — items remain unchecked until tasks are completed.  
Once a task is implemented and verified, it should be checked (`[x]`).  
**Do not record completion dates**; only mark completion status.

**Guidelines:**

- Each phase must begin with a header (`## Phase N: Name`).
    
- Use checkboxes to represent progress:
    
    - `- [ ]` → Task not yet implemented
        
    - `- [x]` → Task completed and verified
        
- Do **not** include completion dates or timestamps.
    
- Each phase must include:
    
    - **Dependencies:** Previous phases or prerequisites
        
    - **Blockers:** Current issues preventing progress
        
    - **Related Tickets:** References to issues or PRs
        
- When all tasks in a phase are checked, the phase is considered complete.
    
- Avoid removing or editing completed items — retain full historical record.
    

**Example:**

```markdown
# Implementation Tasks

## Phase 1: Project Setup
**Dependencies:** None  
**Blockers:** None  
**Related Tickets:** [#1](https://github.com/org/repo/issues/1)

- [x] Initialize repository structure  
- [x] Setup CI/CD pipeline  
- [x] Configure ESLint/Prettier  
- [x] Create `.env` template  

---

## Phase 2: Backend Core
**Dependencies:** Phase 1  
**Blockers:** Database schema review pending  
**Related Tickets:** [#5](https://github.com/org/repo/issues/5)

- [ ] Implement authentication endpoints  
- [ ] Add JWT middleware  
- [x] Define ORM models  
- [ ] Integrate external APIs  

---

## Phase 3: Frontend Integration
**Dependencies:** Phase 2  
**Blockers:** API specification finalization  
**Related Tickets:** [#10](https://github.com/org/repo/issues/10)

- [ ] Setup React + Vite project  
- [ ] Integrate API service layer  
- [ ] Build authentication screens  
- [ ] Configure routing and navigation  

---

## Phase 4: Testing & QA
**Dependencies:** Phase 3  
**Blockers:** Pending backend integration tests  
**Related Tickets:** [#18](https://github.com/org/repo/issues/18)

- [ ] Implement unit tests for API layer  
- [ ] Add E2E tests for login flow  
- [ ] Setup test coverage reporting  
- [ ] Run QA verification checklist  
```

**Update Instructions:**

1. Keep all phases and tasks in chronological order.
    
2. Check (`[x]`) a task only after implementation is verified.
    
3. Do **not** add completion dates or timestamps.
    
4. If a task is replaced or deprecated, strike through the line using `~~text~~` but do not delete it.
    
5. Ensure references to tickets or PRs remain accurate.
    
6. Each PR that introduces new functionality should update this file with corresponding tasks.
    

**Automation Suggestion:**

- Integrate with CI pipelines or AI tools (Cursor, Claude) to automatically check off completed tasks when corresponding commits or merges are detected.
    
- Optional Git hook or workflow:
    
    - Detect when a PR referencing a task is merged.
        
    - Automatically update the related checklist item to `[x]`.
        

---

## 4. Logs Folder Structure (`logs/`)

This folder stores historical, architectural, and implementation-related change records.

### 4.1 `Changelog.md`

**Purpose:**  
Record major project or document updates.

**Required Sections:**

- Date
    
- Summary of change
    
- Related tickets or PRs
    
- Notes or rationale
    

**Example:**

```markdown
# 🕒 Changelog

| Date | Change | Related | Notes |
|------|---------|----------|-------|
| 2025-10-12 | Added standardized documentation folders | #101 | Initial setup |
| 2025-10-15 | Updated Architecture.md | #108 | Added event-driven architecture section |
```

---

### 4.2 `DecisionRecords.md`

**Purpose:**  
Maintain a log of major technical decisions (ADR — Architecture Decision Records).

**Structure Example:**

```markdown
# ADR 001: Database Choice

**Status:** Accepted  
**Date:** 2025-10-11  

## Context
Need a relational database with ORM support.

## Decision
Adopt PostgreSQL with Prisma ORM.

## Consequences
- Strong typing with Prisma  
- Requires additional setup for Dockerized local testing
```

---

### 4.3 `ModificationLog.md`

**Purpose:**  
Track **how** specific features or files were modified and why.

**Required Sections:**

- Date of modification
    
- File(s) affected
    
- Description of modification
    
- Related ticket or PR
    
- Verification details
    

**Example:**

```markdown
# 🧾 Modification Log

| Date | File / Module | Description | Related | Verified |
|------|----------------|-------------|----------|----------|
| 2025-10-20 | /src/api/userController.ts | Refactored login flow for error handling | #123 | ✅ Tested locally |
| 2025-10-21 | /docs/ImplementationTasks.md | Added Phase 3 checklist | #125 | 🧠 Reviewed |
```

---

## 5. Tips Folder Structure (`tips/`)

This folder contains optional but highly encouraged documentation for internal knowledge sharing.

**Recommended Files:**

1. `EnvironmentSetup.md` — Environment setup guide
    
2. `DebuggingGuide.md` — Troubleshooting methods
    
3. `PerformanceOptimization.md` — Tips for improving system performance
    
4. Any other `.md` documents for project-specific advice
    

**Example (`EnvironmentSetup.md`):**

```markdown
# 🧩 Environment Setup

## Prerequisites
- Node.js 20+
- Docker Desktop
- pnpm package manager

## Setup Steps
1. Clone the repository  
2. Run `pnpm install`  
3. Copy `.env.example` to `.env` and configure variables  
4. Start local environment: `pnpm dev`
```

---

## 6. Integration with AI Development Tools

**For Cursor, Claude, or similar AI-assisted environments:**

- The file `docs/README.md` serves as the **AI entry point** for contextual learning.
    
- `logs/` and `tips/` directories are secondary retrieval sources for deeper context.
    
- All Markdown files must maintain a clear header structure (`#`, `##`, `###`).
    
- Projects should implement **automated sync tasks** (e.g., “Sync docs with latest backend changes”).
    
- For Cursor-specific workflows: prefer short, atomic commits with informative messages; use inline command prompts to regenerate snippets aligned with `ImplementationTasks.md` items; include repository-wide embeddings to improve AI recall.
    

---

## 7. Non-Functional Requirements

|Aspect|Requirement|
|---|---|
|**Consistency**|Every repository follows the same documentation structure.|
|**Readability**|All content written in plain, clear Markdown English.|
|**Traceability**|Every decision, change, and log must reference commits or tickets.|
|**Automation**|Use `markdownlint`, `doctoc`, `autodoc`, or CI validation scripts.|
|**Security**|No credentials or sensitive data may appear in documentation.|
|**Review Policy**|PR reviewers must confirm documentation updates before merge.|

---

## 8. Example Repository Structure

```bash
my-application/
├── src/
├── public/
├── tests/
├── docs/
│   ├── README.md
│   ├── Architecture.md
│   ├── DirectoryStructure.md
│   ├── TechStack.md
│   ├── DataStructure.md
│   ├── ImplementationTasks.md
│   └── (optional) UI_UX_Guidelines.md
├── logs/
│   ├── Changelog.md
│   ├── DecisionRecords.md
│   └── ModificationLog.md
├── tips/
│   ├── EnvironmentSetup.md
│   ├── DebuggingGuide.md
│   └── PerformanceOptimization.md
├── README.md
└── package.json
```

---

## 9. Enforcement & Governance

- Every repository must include and maintain the `docs/`, `logs/`, and `tips/` folders.
    
- CI pipelines must validate the existence of mandatory files.
    
- PR templates should include a “Docs & Logs Update Checklist.”
    
- Code reviewers must ensure:
    
    - Corresponding changelog entries exist
        
    - Modifications are logged
        
    - ADRs are updated for major design changes
        
- Regular architecture reviews must confirm `docs/` completeness and consistency.
    

---

## 10. Future Extensions

- Integration with **ADR automation tools** (`adr-tools`, `adr-log`).
    
- Automatic changelog generation via commit parsing (`auto-changelog`).
    
- Embedding-based retrieval for AI agents to query `docs/`, `logs/`, and `tips/`.
    
- LangGraph or LangSmith integration for **context tracing and reasoning evaluation**.
    
- Cross-folder linking and AI-assisted summarization of logs and decisions.