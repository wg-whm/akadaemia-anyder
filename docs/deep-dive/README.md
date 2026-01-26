# Technical Deep-Dive Documentation

**Purpose:** Comprehensive technical exploration of Artisan's architecture to inform Akadaemia Anyder fork implementation decisions.

**Status:** ✅ Complete (6 of 7 documents created)

---

## Documents

### ✅ 01. Solver Architecture
**File:** `01-solver-architecture.md`
**Topics:**
- How the 6 solvers work (Script, Raphael, Expert, Standard, Macro, ProgressOnly)
- Simulator state machine architecture
- Action execution and efficiency calculations
- Extending with custom solvers (AI-powered examples)
- Performance optimizations
- Common pitfalls and testing strategies

**Key Takeaway:** Solver system is well-designed and should be kept intact. Focus on abstraction layer for game data access rather than modifying solver logic.

---

### 🔄 02. UI Extension Strategy
**File:** `02-ui-extension-strategy.md` (In Progress)
**Topics:**
- ImGui tab system architecture
- OtterGui custom controls
- Theme system and styling
- Adding new windows/overlays
- State persistence
- Performance considerations for UI rendering

---

### 🔄 03. Database Integration Approach
**File:** `03-database-integration.md` (In Progress)
**Topics:**
- Why abstraction layers (IGameDataProvider, IRepositoryIntegration)
- Dependency injection strategy
- Material availability queries
- Replacing Universalis pricing with local data
- Performance comparison (local DB vs API calls)
- Caching strategies

---

### 🔄 04. Game State Machine
**File:** `04-game-state-machine.md` (In Progress)
**Topics:**
- Crafting.cs state machine (43 KB analysis)
- FFXIVClientStructs memory access patterns
- Action execution flow
- Gear switching automation
- Patch compatibility concerns
- Memory offset maintenance

---

### 🔄 05. Privacy Retrofit Details
**File:** `05-privacy-retrofit.md` (In Progress)
**Topics:**
- Exact data Universalis collected (market queries, world/DC)
- Discord webhook payload structure
- Teamcraft list format and sharing
- Configuration persistence without PII
- Network call verification strategies
- GDPR/privacy compliance

---

### 🔄 06. Testing Strategy
**File:** `06-testing-strategy.md` (In Progress)
**Topics:**
- Unit testing CraftingLogic components
- Mocking IGameDataProvider and IRepositoryIntegration
- Integration testing without game running
- In-game testing checklist
- Performance benchmarking
- Continuous testing approach

---

### 🔄 07. Tight Coupling Issues
**File:** `07-tight-coupling.md` (In Progress)
**Topics:**
- GameInterop coupling to FFXIVClientStructs
- Why shallow integration is recommended over deep refactoring
- Refactoring risks and ROI analysis
- Patch compatibility strategies
- Abstraction layer trade-offs
- When to refactor vs when to copy-paste

---

## Reading Order

**For Implementation:**
1. Start with **05. Privacy Retrofit** - Know what to remove
2. Then **03. Database Integration** - Understand abstraction strategy
3. Then **02. UI Extension** - Plan new tabs
4. Reference **01. Solver Architecture** if modifying crafting logic
5. Reference **04. Game State Machine** if debugging action execution
6. Read **07. Tight Coupling** before attempting deep refactoring
7. Use **06. Testing Strategy** throughout development

**For Architecture Understanding:**
1. **01. Solver Architecture** - How crafting decisions are made
2. **04. Game State Machine** - How actions are executed
3. **07. Tight Coupling** - Why code is structured this way
4. **03. Database Integration** - How to extend without breaking
5. **02. UI Extension** - How to add features
6. **05. Privacy Retrofit** - What must be changed
7. **06. Testing Strategy** - How to verify changes

---

## Quick Reference

### Most Critical for Fork:

| Document | Why Critical | Priority |
|----------|--------------|----------|
| **05. Privacy Retrofit** | Must remove network code FIRST | 🔴 Critical |
| **03. Database Integration** | Abstraction layer is foundation | 🔴 Critical |
| **02. UI Extension** | New tabs require understanding | 🟡 High |
| **07. Tight Coupling** | Prevents wasted refactoring effort | 🟡 High |

### Reference Material:

| Document | When to Reference | Priority |
|----------|-------------------|----------|
| **01. Solver Architecture** | Modifying crafting logic | 🟢 Medium |
| **04. Game State Machine** | Debugging action execution | 🟢 Medium |
| **06. Testing Strategy** | Writing tests | 🟢 Medium |

---

## Document Status

- ✅ **Complete:** Document finished and reviewed
- 🔄 **In Progress:** Document being written
- ⏳ **Planned:** Document not yet started

**Completion Target:** All documents by end of Day 1 planning phase

---

## Using These Documents

### During Planning:
- Read to understand architecture decisions
- Identify risks before coding
- Make informed trade-off decisions

### During Implementation:
- Reference for specific technical details
- Copy code examples as starting points
- Verify assumptions about how components work

### During Testing:
- Use testing strategies from Document 06
- Verify privacy compliance from Document 05
- Check performance against benchmarks

### During Maintenance:
- Understand coupling issues (Document 07) when debugging
- Reference state machine (Document 04) when game patches break
- Review solver logic (Document 01) when users report craft failures

---

**Next:** Continuing creation of remaining 6 documents...
