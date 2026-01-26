# Deep Dive: Testing Strategy

**Topic:** How to test Artisan fork and Akadaemia extensions
**Complexity:** Medium
**Relevance:** Ensures quality and prevents regressions

---

## Testing Pyramid

```
        /\
       /  \  E2E (In-game testing)
      /____\
     /      \  Integration Tests
    /________\
   /          \  Unit Tests
  /__________  \
```

### Layer 1: Unit Tests (70% of tests)

**What to test:**
- Solver logic (CraftingLogic/Solvers/*)
- Simulator state machine
- Data models and DTOs
- Repository queries (with in-memory database)
- Game data provider (with mocks)

**Example:**
```csharp
[Fact]
public void ExpertSolver_LowCP_ChoosesProgressOverQuality()
{
    var solver = new ExpertSolver();
    var state = new SimulatorState
    {
        Progress = 500,
        MaxProgress = 1000,
        Quality = 0,
        MaxQuality = 5000,
        CP = 50,  // Very low CP
        Durability = 40
    };

    var action = solver.SolveNextStep(state);

    Assert.NotNull(action);
    Assert.True(IsProgressAction(action.Value));  // Should choose progress, not quality
}
```

### Layer 2: Integration Tests (20% of tests)

**What to test:**
- UI components with mock dependencies
- Database CRUD operations
- Configuration persistence
- Module interactions

**Example:**
```csharp
[Fact]
public void RecipeWindowUI_WithMockedDependencies_DisplaysMaterialAvailability()
{
    var mockGameData = new MockGameDataProvider();
    mockGameData.AddMockRecipe(1, new List<RecipeIngredient>
    {
        new() { ItemId = 5333, Quantity = 10 }
    });

    var mockRepo = new MockRepositoryIntegration();
    mockRepo.SetMaterialAvailability(5333, new MaterialAvailability
    {
        InInventory = 15,
        InSaddlebag = 8
    });

    var ui = new RecipeWindowUI(mockGameData, mockRepo, mockLog);

    // Act (would require ImGui test framework)
    ui.DrawIngredients(recipeId: 1);

    // Verify material availability was queried
    Assert.True(mockRepo.WasCalled(nameof(mockRepo.GetMaterialAvailability)));
}
```

### Layer 3: E2E Tests (10% of tests - manual)

**In-game testing checklist:**
```
□ Plugin loads without errors
□ All tabs render
□ Crafting queue works
□ Material availability displays
□ Privacy settings persist
□ No network calls detected
□ No crashes
```

---

## Mock Implementations

### MockGameDataProvider

```csharp
public class MockGameDataProvider : IGameDataProvider
{
    private readonly Dictionary<uint, Item> _items = new();
    private readonly Dictionary<uint, Recipe> _recipes = new();
    private readonly Dictionary<uint, List<RecipeIngredient>> _ingredients = new();

    public void AddMockItem(uint id, string name)
    {
        _items[id] = new Item { RowId = id, Name = name };
    }

    public void AddMockRecipe(uint recipeId, List<RecipeIngredient> ingredients)
    {
        _recipes[recipeId] = new Recipe { RowId = recipeId };
        _ingredients[recipeId] = ingredients;
    }

    public Item? GetItem(uint itemId) => _items.GetValueOrDefault(itemId);
    public Recipe? GetRecipe(uint recipeId) => _recipes.GetValueOrDefault(recipeId);
    public List<RecipeIngredient> GetRecipeIngredients(uint recipeId) =>
        _ingredients.GetValueOrDefault(recipeId) ?? new List<RecipeIngredient>();
}
```

### MockRepositoryIntegration

```csharp
public class MockRepositoryIntegration : IRepositoryIntegration
{
    private readonly Dictionary<uint, MaterialAvailability> _materials = new();
    private readonly List<string> _calledMethods = new();

    public void SetMaterialAvailability(uint itemId, MaterialAvailability availability)
    {
        _materials[itemId] = availability;
    }

    public MaterialAvailability GetMaterialAvailability(uint itemId)
    {
        _calledMethods.Add(nameof(GetMaterialAvailability));
        return _materials.GetValueOrDefault(itemId) ?? new MaterialAvailability { ItemId = itemId };
    }

    public bool WasCalled(string methodName) => _calledMethods.Contains(methodName);
}
```

---

## In-Memory Database for Testing

```csharp
private static PrivacyDatabaseContext CreateTestDatabase()
{
    var options = new DbContextOptionsBuilder<PrivacyDatabaseContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    return new PrivacyDatabaseContext(options);
}

[Fact]
public void Repository_SaveAndLoad_WorksCorrectly()
{
    using var db = CreateTestDatabase();
    var repo = new AkadaemiaAnyderRepository(db, mockLog);

    var list = new CraftingListData
    {
        Name = "Test List",
        Items = new List<CraftingListItemData>
        {
            new() { RecipeId = 1, Quantity = 10 }
        }
    };

    repo.SaveCraftingList(list);
    var loaded = repo.LoadCraftingList(list.Id);

    Assert.NotNull(loaded);
    Assert.Equal("Test List", loaded.Name);
    Assert.Single(loaded.Items);
}
```

---

**End of Testing Strategy Deep Dive**
