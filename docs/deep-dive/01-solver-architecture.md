# Deep Dive: Artisan Solver Architecture

**Topic:** How Artisan's crafting solvers work and how to extend them
**Complexity:** High
**Relevance:** Understanding this enables custom solver development and debugging

---

## Overview

Artisan's solver system is the brain of automated crafting. It decides which crafting actions to execute based on current craft state (progress, quality, CP, durability, buffs).

**Key Components:**
- `ISolver` interface - Contract all solvers implement
- `Simulator` - Crafting state machine that validates and executes actions
- `CraftingProcessor` - Orchestrator that routes craft events to active solver
- 6 solver implementations with fallback chain

---

## ISolver Interface

**Location:** `CraftingLogic/ISolver.cs`

```csharp
public interface ISolver
{
    /// <summary>
    /// Determines the next action to take given current craft state
    /// </summary>
    /// <param name="state">Current simulator state</param>
    /// <returns>Action to execute, or null if craft should end</returns>
    CraftAction? SolveNextStep(SimulatorState state);

    /// <summary>
    /// Called when a new craft begins
    /// </summary>
    void OnCraftStart(Recipe recipe, CharacterStats stats);

    /// <summary>
    /// Called when craft completes (success or failure)
    /// </summary>
    void OnCraftEnd(CraftResult result);
}
```

**Design Pattern:** Strategy pattern
- Solvers are swappable strategies for action selection
- Processor doesn't know which solver is active (polymorphism)
- Easy to add new solvers without modifying existing code

---

## The 6 Solver Implementations

### 1. Script Solver (User-Defined Rotations)

**File:** `CraftingLogic/Solvers/ScriptSolver.cs`
**Priority:** 1 (highest - user has full control)

**How It Works:**
- User writes rotation script in custom DSL (domain-specific language)
- ScriptSolverCompiler parses script into action list
- Solver returns actions in sequence
- Supports conditions: `IF CP > 200 THEN Manipulation`

**Example Script:**
```
MuscleMemory
Manipulation
Veneration
Groundwork x3
Innovation
PreparatoryTouch x4
GreatStrides
ByregotsBlessing
BasicSynthesis
```

**Pros:**
- Complete user control
- Predictable (same sequence every time)
- Fast (no computation overhead)

**Cons:**
- Requires manual tuning per recipe/stats
- No adaptation to RNG conditions
- Breaks if stat requirements not met

**When to Use:** Endgame recipes with tight stat requirements, user wants exact control

---

### 2. Raphael Solver (External AI)

**File:** `CraftingLogic/Solvers/RaphaelSolver.cs`
**Priority:** 2

**How It Works:**
- Calls external `raphael-cli.exe` tool (6.4 MB binary)
- Raphael is a Rust-based craft optimizer (developed separately)
- Sends recipe + stats JSON → receives optimal rotation JSON
- Caches results locally (no repeated calls for same recipe/stats)

**Integration:**
```csharp
public CraftAction? SolveNextStep(SimulatorState state)
{
    // Check cache first
    if (_cachedRotation != null && _currentStep < _cachedRotation.Count)
    {
        return _cachedRotation[_currentStep++];
    }

    // Call Raphael CLI
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "raphael-cli.exe",
            Arguments = $"--recipe {recipeId} --stats {stats.ToJson()}",
            RedirectStandardOutput = true
        }
    };

    process.Start();
    var output = process.StandardOutput.ReadToEnd();
    _cachedRotation = ParseRaphaelOutput(output);

    return _cachedRotation[0];
}
```

**Pros:**
- Near-optimal solutions (uses search algorithms)
- Adapts to stat variations
- Well-tested by community

**Cons:**
- External dependency (raphael-cli.exe must exist)
- Slower first call (20-60 seconds solve time)
- No mid-craft adaptation (pre-computed rotation)

**When to Use:** New recipes where user hasn't written script yet, stats are borderline

---

### 3. Expert Solver (Heuristic-Based)

**File:** `CraftingLogic/Solvers/ExpertSolver.cs` (60 KB - most complex)
**Priority:** 3 (Artisan's default)

**How It Works:**
- Rule-based heuristic engine
- Divides craft into phases: Opener → Mid-craft → Finisher
- Evaluates multiple factors each step:
  - Remaining progress (can we finish?)
  - Available CP (can we afford quality actions?)
  - Durability (do we need repair?)
  - Buff timers (Innovation running out?)
  - Condition (Excellent = use quality actions)

**Phase Breakdown:**

#### **Opener Phase** (First 3-5 Actions)
```csharp
private CraftAction? SolveOpener(SimulatorState state)
{
    // 1. Use Muscle Memory if available (free progress)
    if (state.CanUseAction(CraftAction.MuscleMemory))
        return CraftAction.MuscleMemory;

    // 2. Apply Manipulation (CP-efficient durability regen)
    if (!state.HasBuff(Buff.Manipulation) && state.CP >= 96)
        return CraftAction.Manipulation;

    // 3. Apply Veneration for progress phase (20% efficiency boost)
    if (!state.HasBuff(Buff.Veneration) && state.CP >= 18)
        return CraftAction.Veneration;

    // Exit opener phase
    _phase = CraftPhase.MidCraft;
    return SolveMidCraft(state);
}
```

#### **Mid-Craft Phase** (Bulk of Craft)
```csharp
private CraftAction? SolveMidCraft(SimulatorState state)
{
    // 1. Check if we can finish progress
    if (CanFinishProgressNow(state))
    {
        _phase = CraftPhase.Finisher;
        return SolveFinisher(state);
    }

    // 2. Durability management
    if (state.Durability <= 10 && !state.HasBuff(Buff.Manipulation))
    {
        if (state.CP >= 88)
            return CraftAction.MastersMend;  // Emergency repair
    }

    // 3. Progress vs Quality decision
    if (state.Progress < state.Recipe.MaxProgress * 0.5)
    {
        // Still need significant progress
        return SelectProgressAction(state);
    }
    else if (state.Quality < state.Recipe.MaxQuality && state.CP > 200)
    {
        // Can afford quality actions
        return SelectQualityAction(state);
    }
    else
    {
        // Low CP, just push progress
        return SelectProgressAction(state);
    }
}

private CraftAction SelectQualityAction(SimulatorState state)
{
    // Condition exploitation
    if (state.Condition == Condition.Excellent || state.Condition == Condition.Good)
    {
        // Use powerful quality actions on good conditions
        if (state.HasBuff(Buff.Innovation) && state.CP >= 32)
            return CraftAction.PreciseTouch;
        if (state.CP >= 18)
            return CraftAction.BasicTouch;
    }

    // Innovation buff management
    if (!state.HasBuff(Buff.Innovation) && state.CP >= 18)
        return CraftAction.Innovation;  // +20% quality efficiency

    // Default quality action
    if (state.CP >= 32)
        return CraftAction.PreparatoryTouch;  // Builds Inner Quiet stacks
    else
        return CraftAction.BasicTouch;
}
```

#### **Finisher Phase** (Last 1-3 Actions)
```csharp
private CraftAction? SolveFinisher(SimulatorState state)
{
    // 1. Byregot's Blessing if we have Inner Quiet stacks
    if (state.GetBuffStacks(Buff.InnerQuiet) >= 5 && state.CP >= 24)
    {
        // Apply Great Strides for 100% efficiency boost
        if (!state.HasBuff(Buff.GreatStrides) && state.CP >= 32)
            return CraftAction.GreatStrides;

        return CraftAction.ByregotsBlessing;  // Massive quality action
    }

    // 2. Complete progress
    if (state.Progress < state.Recipe.MaxProgress)
    {
        // Calculate exact action needed
        var remaining = state.Recipe.MaxProgress - state.Progress;

        if (CanProgressActionFinish(CraftAction.BasicSynthesis, remaining))
            return CraftAction.BasicSynthesis;
        else if (CanProgressActionFinish(CraftAction.CarefulSynthesis, remaining))
            return CraftAction.CarefulSynthesis;
        else
            return CraftAction.Groundwork;  // Most efficient
    }

    // Craft complete
    return null;
}
```

**Heuristic Factors:**

1. **CP Efficiency:**
   ```csharp
   // Prioritize CP-efficient actions
   // Manipulation: 96 CP for 8 durability = 12 CP per 10 dur
   // Master's Mend: 88 CP for 30 durability = 29 CP per 10 dur
   // Always prefer Manipulation if available
   ```

2. **Condition Exploitation:**
   ```csharp
   // Condition multipliers:
   // Excellent: 4.0x quality
   // Good: 1.5x quality
   // Normal: 1.0x
   // Poor: 0.5x

   // Save high-efficiency actions (Precise Touch, Tricks of Trade) for Excellent/Good
   ```

3. **Buff Stacking:**
   ```csharp
   // Innovation (18 CP) + Great Strides (32 CP) = 2.4x quality multiplier
   // Cost: 50 CP
   // Use for Byregot's Blessing (consumes Inner Quiet stacks)
   // Result: Massive quality gain on final action
   ```

**Pros:**
- Good balance of progress and quality
- Adapts to RNG conditions
- No external dependencies
- Reasonable for most recipes

**Cons:**
- Not optimal (heuristics are hand-tuned)
- Can get stuck in edge cases
- Complex code (hard to debug)

**When to Use:** Default for most crafting, no manual tuning needed

---

### 4. Standard Solver (Simpler Heuristic)

**File:** `CraftingLogic/Solvers/StandardSolver.cs`
**Priority:** 4 (fallback if Expert fails)

**How It Works:**
- Simplified version of Expert Solver
- Less aggressive quality optimization
- More conservative CP spending
- Focuses on completing craft reliably

**Differences from Expert:**
- No condition exploitation
- Simpler buff logic (fewer stacks)
- Lower quality targets
- More durable repair

**When to Use:** Low-level recipes, unreliable stats, "just finish it" mode

---

### 5. Macro Solver (Fixed Rotation)

**File:** `CraftingLogic/Solvers/MacroSolver.cs`
**Priority:** 5

**How It Works:**
- Executes pre-recorded macro from configuration
- No logic, just plays back actions
- Stops if action fails (insufficient CP/durability)

**Macro Format:**
```csharp
public class MacroDefinition
{
    public List<CraftAction> Actions { get; set; }
    public int MinCraftsmanship { get; set; }
    public int MinControl { get; set; }
    public int MinCP { get; set; }
}
```

**Pros:**
- Extremely fast (no computation)
- Consistent results
- Easy to share (just text file)

**Cons:**
- Fragile (stats must exactly match)
- No adaptation
- Requires manual creation per recipe

**When to Use:** Community-shared macros, very specific stat builds

---

### 6. ProgressOnly Solver (Minimal Logic)

**File:** `CraftingLogic/Solvers/ProgressOnlySolver.cs`
**Priority:** 6 (absolute fallback)

**How It Works:**
```csharp
public CraftAction? SolveNextStep(SimulatorState state)
{
    // Just spam progress actions until finished
    if (state.Durability <= 0)
        return CraftAction.MastersMend;

    if (state.Progress < state.Recipe.MaxProgress)
        return CraftAction.BasicSynthesis;

    return null;  // Done
}
```

**When to Use:** Emergency fallback, collectable turn-ins (quality doesn't matter)

---

## Simulator Architecture

**File:** `CraftingLogic/Simulator.cs` (26 KB)

### State Representation

```csharp
public class SimulatorState
{
    // Craft targets
    public int MaxProgress { get; set; }
    public int MaxQuality { get; set; }
    public int MaxDurability { get; set; }

    // Current state
    public int Progress { get; set; }
    public int Quality { get; set; }
    public int Durability { get; set; }
    public int CP { get; set; }
    public int Step { get; set; }

    // Buffs (with duration/stacks)
    public Dictionary<Buff, BuffState> ActiveBuffs { get; set; }

    // RNG state
    public Condition CurrentCondition { get; set; }

    // Character stats (cached)
    public int Craftsmanship { get; set; }
    public int Control { get; set; }
    public int MaxCP { get; set; }
    public int Level { get; set; }
}

public class BuffState
{
    public int Duration { get; set; }  // Turns remaining
    public int Stacks { get; set; }    // For stackable buffs (Inner Quiet)
}
```

### Action Execution

```csharp
public SimulationResult Execute(CraftAction action)
{
    // 1. Validate action is legal
    if (!CanUseAction(action))
        return SimulationResult.ActionInvalid;

    // 2. Deduct CP cost
    CP -= GetActionCPCost(action);

    // 3. Apply action effects
    switch (action)
    {
        case CraftAction.BasicSynthesis:
            var progressGain = CalculateProgressGain(120);  // 120% efficiency
            Progress += progressGain;
            Durability -= 10;
            break;

        case CraftAction.BasicTouch:
            var qualityGain = CalculateQualityGain(100);  // 100% efficiency
            Quality += qualityGain;
            Durability -= 10;
            IncrementInnerQuiet();
            break;

        case CraftAction.Innovation:
            AddBuff(Buff.Innovation, duration: 4);
            break;

        // ... all 40+ actions
    }

    // 4. Decrement buff durations
    foreach (var buff in ActiveBuffs.Keys.ToList())
    {
        ActiveBuffs[buff].Duration--;
        if (ActiveBuffs[buff].Duration <= 0)
            RemoveBuff(buff);
    }

    // 5. Apply Manipulation healing
    if (HasBuff(Buff.Manipulation))
        Durability += 5;

    // 6. Increment step counter
    Step++;

    // 7. Roll next condition (RNG)
    CurrentCondition = RollNextCondition();

    // 8. Check win/loss
    if (Progress >= MaxProgress)
        return SimulationResult.Success;
    if (Durability <= 0 || CP < 0)
        return SimulationResult.Failure;

    return SimulationResult.Continue;
}
```

### Efficiency Calculations

**Progress Calculation:**
```csharp
private int CalculateProgressGain(int baseEfficiency)
{
    // Base formula from game
    var baseSynthesis = (Craftsmanship * 10.0 / ProgressDivider + 2.0);

    // Apply recipe level modifier
    var levelModifier = GetRecipeLevelModifier(RecipeLevel);

    // Apply efficiency percentage
    var efficiency = baseEfficiency / 100.0;

    // Apply active buffs
    if (HasBuff(Buff.Veneration))
        efficiency *= 1.5;  // +50% progress
    if (HasBuff(Buff.MuscleMemory))
        efficiency *= 2.0;  // +100% progress (first step only)

    // Apply condition modifier
    if (CurrentCondition == Condition.Malleable)
        efficiency *= 1.5;  // +50% on Malleable

    var progressGain = (int)(baseSynthesis * levelModifier * efficiency);

    return progressGain;
}
```

**Quality Calculation:**
```csharp
private int CalculateQualityGain(int baseEfficiency)
{
    var baseTouch = (Control * 10.0 / QualityDivider + 35.0);
    var levelModifier = GetRecipeLevelModifier(RecipeLevel);
    var efficiency = baseEfficiency / 100.0;

    // Apply active buffs
    if (HasBuff(Buff.Innovation))
        efficiency *= 1.2;  // +20% quality
    if (HasBuff(Buff.GreatStrides))
        efficiency *= 2.0;  // +100% quality (consumed)

    // Inner Quiet stacks
    var innerQuietStacks = GetBuffStacks(Buff.InnerQuiet);
    var innerQuietBonus = 1.0 + (innerQuietStacks * 0.1);  // +10% per stack
    efficiency *= innerQuietBonus;

    // Apply condition modifier
    switch (CurrentCondition)
    {
        case Condition.Excellent:
            efficiency *= 4.0;  // 4x quality!
            break;
        case Condition.Good:
            efficiency *= 1.5;
            break;
        case Condition.Poor:
            efficiency *= 0.5;
            break;
    }

    var qualityGain = (int)(baseTouch * levelModifier * efficiency);

    return qualityGain;
}
```

---

## Solver Selection & Fallback Chain

**File:** `CraftingLogic/CraftingProcessor.cs`

```csharp
public class CraftingProcessor
{
    private ISolver? _activeSolver;

    private ISolver SelectSolver(Recipe recipe, Configuration config)
    {
        // 1. Check if user has script for this recipe
        if (config.RecipeScripts.ContainsKey(recipe.RowId))
            return new ScriptSolver(config.RecipeScripts[recipe.RowId]);

        // 2. Try Raphael if enabled
        if (config.UseRaphaelSolver && File.Exists("raphael-cli.exe"))
            return new RaphaelSolver();

        // 3. Default to Expert
        if (config.PreferExpertSolver)
            return new ExpertSolver();

        // 4. Standard fallback
        return new StandardSolver();
    }

    public void OnCraftStep(SimulatorState state)
    {
        try
        {
            var action = _activeSolver.SolveNextStep(state);

            if (action == null)
            {
                // Solver wants to end craft
                EndCraft();
                return;
            }

            // Execute action in game
            Crafting.ExecuteAction(action.Value);
        }
        catch (Exception ex)
        {
            // Solver crashed - switch to fallback
            Log.Error($"Solver failed: {ex.Message}");
            _activeSolver = new ProgressOnlySolver();  // Emergency fallback
        }
    }
}
```

---

## Extending with Custom Solvers

### Example: AI-Powered Solver

```csharp
public class AISolver : ISolver
{
    private readonly HttpClient _client;
    private readonly string _modelEndpoint;

    public AISolver(string endpoint)
    {
        _client = new HttpClient();
        _modelEndpoint = endpoint;
    }

    public CraftAction? SolveNextStep(SimulatorState state)
    {
        // Send state to ML model
        var request = new
        {
            progress = state.Progress,
            quality = state.Quality,
            durability = state.Durability,
            cp = state.CP,
            buffs = state.ActiveBuffs.Keys.ToList(),
            condition = state.CurrentCondition.ToString()
        };

        var response = await _client.PostAsJsonAsync(_modelEndpoint, request);
        var prediction = await response.Content.ReadFromJsonAsync<AIPrediction>();

        return prediction.RecommendedAction;
    }

    public void OnCraftStart(Recipe recipe, CharacterStats stats)
    {
        // Initialize episode for reinforcement learning
    }

    public void OnCraftEnd(CraftResult result)
    {
        // Send reward signal to model
        var reward = result.Success ? 1.0 : -1.0;
        if (result.HQ) reward += 0.5;

        await _client.PostAsJsonAsync($"{_modelEndpoint}/reward", new { reward });
    }
}
```

### Example: Hybrid Solver

```csharp
public class HybridSolver : ISolver
{
    private readonly ExpertSolver _expert;
    private readonly RaphaelSolver _raphael;
    private List<CraftAction>? _raphaelRotation;

    public CraftAction? SolveNextStep(SimulatorState state)
    {
        // Use Raphael for first 80% of craft (reliable)
        if (state.Progress < state.MaxProgress * 0.8 && _raphaelRotation != null)
        {
            return _raphaelRotation[state.Step];
        }

        // Switch to Expert for finisher (adapts to actual quality)
        return _expert.SolveNextStep(state);
    }
}
```

---

## Performance Considerations

### Simulation Speed

**Bottleneck:** Simulator.Execute() called ~20-40 times per craft

**Optimization Strategies:**

1. **Cache Calculations:**
   ```csharp
   // BAD: Recalculate every action
   var progressGain = CalculateProgressGain(120);

   // GOOD: Cache base values
   private int _cachedProgressBase;

   private void OnStatsChange()
   {
       _cachedProgressBase = (Craftsmanship * 10.0 / ProgressDivider + 2.0);
   }

   private int CalculateProgressGain(int efficiency)
   {
       return (int)(_cachedProgressBase * efficiency / 100.0 * GetBuffMultiplier());
   }
   ```

2. **Avoid Dictionary Lookups:**
   ```csharp
   // BAD: Dictionary lookup every action
   if (ActiveBuffs.ContainsKey(Buff.Innovation)) { ... }

   // GOOD: Bitfield flags
   [Flags]
   private enum ActiveBuffFlags
   {
       None = 0,
       Veneration = 1 << 0,
       Innovation = 1 << 1,
       Manipulation = 1 << 2,
       GreatStrides = 1 << 3
   }

   private ActiveBuffFlags _buffFlags;

   private bool HasBuff(Buff buff)
   {
       return (_buffFlags & GetBuffFlag(buff)) != 0;
   }
   ```

3. **Pre-compute Action Sequences:**
   ```csharp
   // Raphael approach: solve once, cache rotation
   // Avoid re-solving same recipe/stats combination
   ```

### Memory Usage

**State Size:** ~200 bytes per SimulatorState
**Solver Memory:**
- ExpertSolver: ~10 KB (rule tables)
- RaphaelSolver: ~500 KB (cache storage)
- ScriptSolver: ~1 KB (action list)

---

## Common Pitfalls

### 1. Forgetting Buff Duration Decrement

```csharp
// BUG: Buff lasts forever
public void Execute(CraftAction action)
{
    ApplyActionEffects(action);
    // Forgot to decrement buff durations!
}

// FIX:
public void Execute(CraftAction action)
{
    ApplyActionEffects(action);
    DecrementBuffDurations();  // Add this
}
```

### 2. Incorrect Condition Handling

```csharp
// BUG: Using poor condition for quality actions
if (state.CP >= 32)
    return CraftAction.PreparatoryTouch;  // Wasted if condition is Poor!

// FIX: Check condition first
if (state.Condition == Condition.Poor)
    return CraftAction.BasicSynthesis;  // Use progress instead

if (state.CP >= 32)
    return CraftAction.PreparatoryTouch;
```

### 3. Not Validating Actions

```csharp
// BUG: Trying to use unavailable action
var action = solver.SolveNextStep(state);
Crafting.ExecuteAction(action);  // Crashes if action not unlocked!

// FIX: Validate first
var action = solver.SolveNextStep(state);
if (action != null && state.CanUseAction(action.Value))
    Crafting.ExecuteAction(action.Value);
else
    // Fallback to safe action
```

---

## Testing Solvers

### Unit Test Example

```csharp
[Fact]
public void ExpertSolver_LowCPScenario_FinishesWithoutHQ()
{
    // Arrange
    var solver = new ExpertSolver();
    var state = new SimulatorState
    {
        MaxProgress = 1000,
        MaxQuality = 5000,
        MaxDurability = 80,
        CP = 250,  // Low CP
        Craftsmanship = 2500,
        Control = 2400
    };

    var simulator = new Simulator(state);
    solver.OnCraftStart(recipe, stats);

    // Act
    while (simulator.Status == CraftStatus.InProgress)
    {
        var action = solver.SolveNextStep(simulator.State);
        simulator.Execute(action);
    }

    // Assert
    Assert.Equal(CraftStatus.Success, simulator.Status);
    Assert.True(simulator.State.Progress >= state.MaxProgress);
    // May not reach HQ due to low CP (expected)
}
```

### Integration Test

```csharp
[Fact]
public void RaphaelSolver_CallsExternalCLI_ReturnsValidRotation()
{
    // Requires raphael-cli.exe in test directory
    var solver = new RaphaelSolver();

    var state = CreateTestState();
    solver.OnCraftStart(testRecipe, testStats);

    var action = solver.SolveNextStep(state);

    Assert.NotNull(action);
    Assert.True(Enum.IsDefined(typeof(CraftAction), action.Value));
}
```

---

## Recommendations

### For Akadaemia Anyder Fork:

1. **Keep all 6 solvers intact** - Don't remove any, they're all used
2. **Don't modify solver logic** - It's well-tuned, fragile to changes
3. **Focus on abstraction layer** - Inject IGameDataProvider for game data
4. **Add custom solver for testing** - Create MinimalSolver that just finishes crafts
5. **Cache Raphael results** - Store in local database, not configuration

### If Adding Custom Solver:

1. Implement ISolver interface fully
2. Handle OnCraftStart (initialize state)
3. Handle OnCraftEnd (cleanup)
4. Validate all actions with CanUseAction()
5. Test with low/high stats, low/high durability, low/high CP
6. Add fallback to ProgressOnlySolver on error

---

**End of Solver Architecture Deep Dive**

This document provides complete understanding of Artisan's solver system. Next: UI Extension Strategy.
