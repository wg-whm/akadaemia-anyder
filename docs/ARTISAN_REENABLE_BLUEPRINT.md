# Blueprint: Re-enable Artisan Modules Compilation

**Created:** 2026-01-29
**Complexity Score:** 14/20 (FINE decomposition)
**Estimated Duration:** 4-6 hours
**Risk Level:** Medium-High

---

## Executive Summary

Re-enable compilation of 357 Artisan module C# files currently excluded from build. Previous attempt resulted in 50+ errors requiring full exclusion. This blueprint uses incremental dependency resolution to avoid catastrophic failure.

**Current State:**
- All Artisan modules excluded via `<Compile Remove="Modules\**\*.cs" />`
- 8 NuGet dependencies commented out in .csproj
- Plugin is a shell with no crafting functionality

**Target State:**
- All Artisan modules compile successfully
- All dependencies enabled and resolved
- Zero build errors, warnings acceptable
- Tests pass (after fixing existing test issues in #2)

**Strategy:** Incremental enablement - one dependency tier at a time, verify builds between steps.

---

## Complexity Assessment

| Factor | Score | Rationale |
|--------|-------|-----------|
| File count | 4/5 | 357 files affected |
| Logic complexity | 4/5 | Dependency resolution, namespace refactoring needed |
| Risk surface | 3/5 | Build system, no data/auth risk |
| Test coverage | 3/5 | Tests exist but currently broken (issue #2) |
| **TOTAL** | **14/20** | **FINE decomposition required** |

---

## Dependencies Analysis

### Tier 1: Foundation Libraries (Build First)
- **OtterGui** - UI framework used by 77 files
  - Dependencies: Microsoft.Extensions.DependencyInjection 9.0.2, JetBrains.Annotations 2024.3.0
  - Located: `AkadaemiaAnyder/Modules/Artisan/OtterGui/`

- **PunishLib** - Utility library
  - Dependencies: Dalamud framework only
  - Located: `AkadaemiaAnyder/Modules/Artisan/PunishLib/`

### Tier 2: Integration Libraries
- **ECommons** 3.1.0.10 - Used by 57 files
- **FuzzySharp** 2.0.2 - String matching
- **NAudio** 2.2.1 - Audio processing
- **SharpDX.Mathematics** 4.2.0 - Math utilities

### Tier 3: Code Analysis
- **Microsoft.CodeAnalysis** 4.13.1
- **Microsoft.CodeAnalysis.CSharp** 4.13.1

### Tier 4: Artisan Application (Build Last)
- All 357 .cs files in `Modules/Artisan/`
- Depends on all above tiers

**No namespace conflicts detected** - System.Numerics.Vector2/3/4 used consistently.

---

## Task Breakdown (FINE Decomposition)

### Phase 1: Foundation Setup
**Duration:** 30-45 min

#### T1: Enable OtterGui and PunishLib Dependencies
**Objective:** Restore foundation library dependencies and verify they build.

**Actions:**
1. Uncomment in `AkadaemiaAnyder.csproj`:
   - `Microsoft.Extensions.DependencyInjection` 9.0.2
   - `JetBrains.Annotations` 2024.3.0
2. Run `dotnet restore AkadaemiaAnyder/AkadaemiaAnyder.csproj`
3. Run `dotnet build AkadaemiaAnyder/Modules/Artisan/OtterGui/OtterGui.csproj`
4. Run `dotnet build AkadaemiaAnyder/Modules/Artisan/PunishLib/PunishLib.csproj`

**Success Criteria:**
- Both libraries build with 0 errors
- Warnings acceptable and documented

**Dependencies:** None
**Rollback:** Re-comment dependencies

---

#### T2: Enable Integration Libraries
**Objective:** Add ECommons, FuzzySharp, NAudio, SharpDX dependencies.

**Actions:**
1. Uncomment in `AkadaemiaAnyder.csproj`:
   - `<PackageReference Include="ECommons" Version="3.1.0.10" />`
   - `<PackageReference Include="FuzzySharp" Version="2.0.2" />`
   - `<PackageReference Include="NAudio" Version="2.2.1" />`
   - `<PackageReference Include="SharpDX.Mathematics" Version="4.2.0" />`
2. Run `dotnet restore AkadaemiaAnyder/AkadaemiaAnyder.csproj`
3. Run `dotnet build AkadaemiaAnyder/AkadaemiaAnyder.csproj --no-restore`

**Success Criteria:**
- NuGet restore succeeds
- Build completes (errors expected but should be from Modules still being excluded)

**Dependencies:** T1 complete
**Rollback:** Re-comment packages

---

#### T3: Enable Code Analysis Dependencies
**Objective:** Add Microsoft.CodeAnalysis packages.

**Actions:**
1. Uncomment in `AkadaemiaAnyder.csproj`:
   - `<PackageReference Include="Microsoft.CodeAnalysis" Version="4.13.1" />`
   - `<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.13.1" />`
2. Run `dotnet restore AkadaemiaAnyder/AkadaemiaAnyder.csproj`

**Success Criteria:**
- NuGet restore succeeds
- No conflicts with existing packages

**Dependencies:** T2 complete
**Rollback:** Re-comment packages

---

### Phase 2: Incremental Module Enablement
**Duration:** 1.5-2 hours

#### T4: Create Namespace Refactoring Script
**Objective:** Automate namespace changes from `Artisan.*` to `AkadaemiaAnyder.Modules.Artisan.*`

**Actions:**
1. Create PowerShell script: `scripts/Refactor-ArtisanNamespaces.ps1`
2. Script logic:
   ```powershell
   # For each .cs file in Modules/Artisan/
   # Replace: namespace Artisan
   # With: namespace AkadaemiaAnyder.Modules.Artisan
   # Replace: using Artisan.
   # With: using AkadaemiaAnyder.Modules.Artisan.
   ```
3. Add dry-run mode with diff output
4. Test on 5 sample files first

**Success Criteria:**
- Script runs without errors
- Dry-run shows expected changes
- Sample files refactor correctly

**Dependencies:** None (parallel to T1-T3)
**Rollback:** Git reset

---

#### T5: Enable Small Module Subset (10 files)
**Objective:** Test build with minimal module subset to catch early issues.

**Actions:**
1. Modify `AkadaemiaAnyder.csproj` to change:
   ```xml
   <Compile Remove="Modules\**\*.cs" />
   ```
   to:
   ```xml
   <Compile Remove="Modules\Artisan\Artisan\**\*.cs" />
   <Compile Include="Modules\Artisan\OtterGui\**\*.cs" />
   <Compile Include="Modules\Artisan\PunishLib\**\*.cs" />
   ```
2. Run namespace refactoring script on OtterGui and PunishLib only
3. Build: `dotnet build AkadaemiaAnyder/AkadaemiaAnyder.csproj`
4. Document all errors in `ARTISAN_ERRORS_T5.md`

**Success Criteria:**
- Build completes (errors expected)
- Error patterns identified
- No crashes during build

**Dependencies:** T1, T2, T3, T4 complete
**Rollback:** Revert .csproj changes, git reset namespace changes

---

#### T6: Fix OtterGui/PunishLib Build Errors
**Objective:** Resolve all errors from T5 build.

**Actions:**
1. Categorize errors from `ARTISAN_ERRORS_T5.md`:
   - Namespace mismatches
   - Missing using statements
   - Type conflicts
   - Other
2. Fix errors incrementally:
   - Start with namespace issues (likely most common)
   - Then missing usings
   - Then type conflicts
3. Rebuild after each category fix
4. Document solutions in `ARTISAN_FIXES.md`

**Success Criteria:**
- OtterGui and PunishLib build with 0 errors
- All fixes documented
- Warnings < 10

**Dependencies:** T5 complete
**Rollback:** Git reset to T5 state

---

### Phase 3: Full Module Enablement
**Duration:** 2-3 hours

#### T7: Enable All Artisan Modules
**Objective:** Include remaining 250+ Artisan application files.

**Actions:**
1. Modify `AkadaemiaAnyder.csproj` to remove all exclusions:
   ```xml
   <!-- Delete these lines:
   <Compile Remove="Modules\**\*.cs" />
   <EmbeddedResource Remove="Modules\**\*" />
   <None Remove="Modules\**\*" />
   -->
   ```
2. Run namespace refactoring script on all `Modules/Artisan/Artisan/**/*.cs` files
3. Build: `dotnet build AkadaemiaAnyder/AkadaemiaAnyder.csproj`
4. Document errors in `ARTISAN_ERRORS_T7.md`
5. Estimate error count and categorize

**Success Criteria:**
- Build completes
- Error count < 100 (if > 100, re-evaluate strategy)
- Error categories identified

**Dependencies:** T6 complete
**Rollback:** Re-add exclusions, git reset namespace changes

---

#### T8: Fix Artisan Module Build Errors (Batch 1)
**Objective:** Resolve first 50% of errors from T7.

**Actions:**
1. Sort errors by category from `ARTISAN_ERRORS_T7.md`
2. Fix highest-frequency category first (likely namespace issues)
3. Apply fixes in batches of 10-20 files
4. Rebuild after each batch
5. Document progress in `ARTISAN_FIXES.md`

**Success Criteria:**
- 50% error reduction
- Pattern-based fixes documented
- No new error categories introduced

**Dependencies:** T7 complete
**Rollback:** Git reset to T7 state

---

#### T9: Fix Artisan Module Build Errors (Batch 2)
**Objective:** Resolve remaining errors from T7.

**Actions:**
1. Continue fixing errors from remaining categories
2. Handle one-off errors individually
3. Use Git blame to understand original intent if needed
4. Consult Artisan upstream repo if necessary: https://github.com/PunishXIV/Artisan

**Success Criteria:**
- 0 build errors
- Warnings < 50 (document if higher)
- All modules compile successfully

**Dependencies:** T8 complete
**Rollback:** Git reset to T8 state

---

### Phase 4: Verification
**Duration:** 30-45 min

#### T10: Run Full Build and Validate
**Objective:** Confirm entire project builds successfully.

**Actions:**
1. Clean build: `dotnet clean && dotnet build AkadaemiaAnyder/AkadaemiaAnyder.csproj -c Debug`
2. Release build: `dotnet build AkadaemiaAnyder/AkadaemiaAnyder.csproj -c Release`
3. Count warnings: `dotnet build | grep "warning" | wc -l`
4. Verify DLL size (should be ~5-10MB with Artisan vs ~500KB without)
5. Update CLAUDE.md with completion status

**Success Criteria:**
- Debug build: 0 errors
- Release build: 0 errors
- Warnings documented
- DLL size increased significantly

**Dependencies:** T9 complete
**Rollback:** N/A (verification only)

---

## Risk Mitigation

### Risk 1: Error Count Exceeds 100 in T7
**Likelihood:** Medium
**Impact:** High (indicates approach failure)

**Mitigation:**
- Set hard stop at 100 errors in T7
- If triggered, PAUSE and reassess strategy
- Options: (a) More granular enablement, (b) Fix namespace script issues, (c) Consult upstream Artisan repo

**Contingency:** Revert to Phase 2 (T6) and enable modules in smaller batches (50 files at a time)

---

### Risk 2: Namespace Refactoring Script Breaks Code
**Likelihood:** Low
**Impact:** Medium

**Mitigation:**
- Mandatory dry-run with diff review before execution
- Test on 5 sample files first (T4)
- Git commit before each script run

**Contingency:** Git reset to pre-script state, manual namespace fixes

---

### Risk 3: Dependency Version Conflicts
**Likelihood:** Low
**Impact:** Medium

**Mitigation:**
- Enable dependencies incrementally (T1-T3)
- Check for conflicts after each restore
- Document exact versions in blueprint

**Contingency:** Try compatible versions from Artisan upstream .csproj, fallback to version ranges

---

### Risk 4: Tests Fail After Re-enabling Modules
**Likelihood:** High (tests already broken per issue #2)
**Impact:** Low (issue #2 is pre-existing, not caused by this work)

**Mitigation:**
- Fix issue #2 separately (tracked in GitHub)
- Document if new test failures introduced

**Contingency:** N/A - existing test issues are out of scope

---

## Rollback Strategy

Each task has defined rollback steps. General rollback procedure:

1. **Immediate rollback** (within same task):
   ```bash
   git reset --hard HEAD
   git clean -fd
   ```

2. **Rollback to previous task**:
   ```bash
   git log --oneline  # Find task commit
   git reset --hard <commit-sha>
   ```

3. **Nuclear option** (restore full exclusion):
   ```bash
   # Re-add to .csproj:
   <ItemGroup>
     <Compile Remove="Modules\**\*.cs" />
     <EmbeddedResource Remove="Modules\**\*" />
     <None Remove="Modules\**\*" />
   </ItemGroup>
   # Re-comment all Artisan dependencies
   ```

**Commit strategy:** Commit after each successful task (T1-T10) for granular rollback points.

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Build errors | 0 | `dotnet build` output |
| Build warnings | < 50 | `dotnet build \| grep warning \| wc -l` |
| DLL size | > 5MB | File size of SamplePlugin.dll |
| Files compiled | 357 | All Modules/**/*.cs included |
| Dependencies enabled | 8/8 | All package references uncommented |
| Task completion | 10/10 | All tasks marked complete |

---

## Approval Required

**Approach Decision:**
- **Option A (Recommended):** Follow this FINE-grained blueprint (T1-T10) with incremental enablement
- **Option B:** Attempt all-at-once enablement (higher risk, faster if successful)
- **Option C:** Enable modules in 3 larger batches (coarser than FINE, finer than all-at-once)

**User Input Needed:**
1. Approve approach (A/B/C)?
2. Error threshold for T7 - pause if errors > 100?
3. Warning threshold acceptable - < 50 warnings OK?

---

## Estimated Timeline

| Phase | Tasks | Duration | Cumulative |
|-------|-------|----------|------------|
| Phase 1: Foundation | T1-T3 | 30-45 min | 0:45 |
| Phase 2: Incremental | T4-T6 | 1.5-2 hrs | 2:45 |
| Phase 3: Full Enable | T7-T9 | 2-3 hrs | 5:45 |
| Phase 4: Verification | T10 | 30-45 min | 6:30 |
| **TOTAL** | **10 tasks** | **4.5-6.5 hrs** | |

Add 1-2 hours buffer for unexpected issues = **6-8 hours total**.

---

## Dependencies Graph

```
T1 (OtterGui/PunishLib deps) ──┐
                               ├──> T2 (Integration libs) ──> T3 (Code analysis) ──┐
T4 (Namespace script) ─────────┤                                                    ├──> T5 (Enable subset) ──> T6 (Fix subset) ──> T7 (Enable all) ──> T8 (Fix batch 1) ──> T9 (Fix batch 2) ──> T10 (Verify)
                               └────────────────────────────────────────────────────┘
```

**Critical Path:** T1 → T2 → T3 → T5 → T6 → T7 → T8 → T9 → T10 (9 tasks, ~6 hours)
**Parallel Path:** T4 can run anytime before T5

---

## Next Steps After Approval

1. Execute T1-T3 (Foundation setup)
2. Create namespace refactoring script (T4)
3. Test with small subset (T5-T6)
4. Full enablement and fixes (T7-T9)
5. Final verification (T10)
6. Update roadmap documentation
7. Close tracking issue (create if needed)
8. Merge to main after PR review

---

**Blueprint Status:** READY FOR APPROVAL
**Requires User Decision:** Approach selection (A/B/C) and threshold confirmation
