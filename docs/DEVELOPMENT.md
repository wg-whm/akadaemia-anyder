# Development Guide - Akadaemia Anyder

Comprehensive guide for setting up your development environment and building the plugin from source.

> **⚠️ Current Status**: Recipe tracking is fully implemented. Gathering and fishing event detection infrastructure is built but detection logic is not yet implemented. See [STATUS.md](../STATUS.md) for details.

## Prerequisites

- **Windows 10/11** (required for XIVLauncher and Dalamud)
- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** (build system)
- **[XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)** (FFXIV launcher with Dalamud support)
- **Final Fantasy XIV** (active subscription required)
- **[Visual Studio 2022](https://visualstudio.microsoft.com) or [JetBrains Rider](https://www.jetbrains.com/rider/)** (optional but recommended)
- **Git** (for version control)

## Environment Setup

### 1. Install .NET 10 SDK

Visit [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) and download the .NET 10 SDK for Windows.

Verify installation:
```bash
dotnet --version
```

### 2. Install and Configure XIVLauncher

1. Download [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)
2. Install and run it at least once
3. Login with your FFXIV credentials
4. Launch FINAL FANTASY XIV through XIVLauncher
5. Type `/xlplugins` in-game to verify Dalamud loaded
6. Close the game

### 3. Clone Repository

```bash
git clone https://github.com/wgdevelopment/akadaemia-anyder.git
cd akadaemia-anyder
```

### 4. Environment Variables (Optional)

If using a custom Dalamud directory, set:
```bash
# Windows Command Prompt
set DALAMUD_HOME=C:\path\to\your\dalamud\directory

# PowerShell
$env:DALAMUD_HOME="C:\path\to\your\dalamud\directory"
```

## Building

### Quick Build

```bash
cd akadaemia-anyder
dotnet build SamplePlugin/SamplePlugin.csproj -c Debug
```

**Output location**: `%APPDATA%\XIVLauncher\devPlugins\AkadaemiaAnyder\SamplePlugin.dll`

### Release Build

```bash
dotnet build SamplePlugin/SamplePlugin.csproj -c Release
```

### Clean Build

```bash
dotnet clean SamplePlugin/SamplePlugin.csproj
dotnet build SamplePlugin/SamplePlugin.csproj -c Debug
```

## Activating in Game

### First Time Setup

1. Launch FINAL FANTASY XIV through XIVLauncher
2. Type `/xlsettings` to open Dalamud settings
3. Go to **Experimental** tab
4. Add to "Dev Plugin Locations": `%APPDATA%\XIVLauncher\devPlugins`
5. Type `/xlplugins` to open Plugin Installer
6. Go to **Dev Tools → Installed Dev Plugins**
7. Enable "SamplePlugin" (Akadaemia Anyder)
8. Type `/akadaemia` to open the tracker window

### Subsequent Builds

The plugin persists in the dev plugin list, so you only need to rebuild and reload:

```bash
dotnet build SamplePlugin/SamplePlugin.csproj -c Debug
```

Then reload in-game using `/xlplugins` → disable and re-enable, or `/reload` command if available.

## Running Tests

### All Tests

```bash
dotnet test AkadaemiaAnyder.Tests/AkadaemiaAnyder.Tests.csproj -v normal
```

### Specific Test Class

```bash
dotnet test AkadaemiaAnyder.Tests/AkadaemiaAnyder.Tests.csproj --filter ClassName
```

### With Coverage Report

```bash
dotnet test AkadaemiaAnyder.Tests/AkadaemiaAnyder.Tests.csproj /p:CollectCoverage=true
```

### Watch Mode (Auto-run on changes)

```bash
dotnet watch test --project AkadaemiaAnyder.Tests/AkadaemiaAnyder.Tests.csproj
```

## Debugging In-Game

### Using Visual Studio 2022

1. Build in Debug configuration:
   ```bash
   dotnet build SamplePlugin/SamplePlugin.csproj -c Debug
   ```

2. Launch FINAL FANTASY XIV through XIVLauncher

3. In Visual Studio:
   - Go to **Debug → Attach to Process**
   - Search for `ffxiv_dx11.exe`
   - Click Attach

4. Set breakpoints in the code

5. In-game, type `/akadaemia` to trigger your code

6. Execution will pause at breakpoints in Visual Studio

### Using JetBrains Rider

1. Build in Debug configuration

2. Launch FINAL FANTASY XIV through XIVLauncher

3. In Rider:
   - Go to **Run → Attach to Process**
   - Select `ffxiv_dx11.exe`
   - Click Attach

4. Set breakpoints

5. Type `/akadaemia` in-game to trigger code

### Console Output

View plugin logs:
- **In-game**: Type `/xllog` to open Dalamud log viewer
- **File logs**: `%APPDATA%\XIVLauncher\logs\`

## Project Structure

```
akadaemia-anyder/
├── SamplePlugin/                     # Main plugin project
│   ├── CoreModels/                   # Domain models (Recipe, Gathering, Fishing)
│   │   └── *.cs
│   ├── Data/                         # Database and persistence layer
│   │   ├── DatabaseContext.cs        # SQLite context and fallback logic
│   │   ├── Repositories/             # Data access objects
│   │   │   ├── RecipeRepository.cs
│   │   │   ├── GatheringRepository.cs
│   │   │   └── FishingRepository.cs
│   │   ├── Models/                   # EF Core models
│   │   │   └── *.cs
│   │   └── Migrations/               # Database schema migrations
│   │       └── *.cs
│   ├── MemoryReaders/                # Memory reading for recipes
│   │   ├── RecipeReader.cs           # Primary recipe reader
│   │   ├── SafeMemoryReader.cs       # Safe wrapper with bounds checking
│   │   └── *.cs
│   ├── EventListeners/               # Game event handlers
│   │   ├── GatheringEventListener.cs # Gathering node events
│   │   ├── FishingEventListener.cs   # Fishing hole events
│   │   └── *.cs
│   ├── Services/                     # Business logic
│   │   ├── CollectionService.cs      # Main collection orchestration
│   │   ├── ProgressCalculator.cs     # Statistics and progress
│   │   ├── ChangeDetector.cs         # Detect new items
│   │   ├── ExportImport/             # JSON export/import
│   │   │   ├── JsonExporter.cs
│   │   │   └── JsonImporter.cs
│   │   └── *.cs
│   ├── Windows/                      # ImGui UI components
│   │   ├── MainWindow.cs             # Main tracker window
│   │   ├── ConfigWindow.cs           # Settings window
│   │   └── *.cs
│   ├── Plugin.cs                     # Plugin entry point
│   ├── Configuration.cs              # Plugin configuration
│   └── SamplePlugin.csproj           # Project file
│
├── AkadaemiaAnyder.Tests/            # xUnit test project
│   ├── UnitTests/
│   │   ├── RepositoryTests.cs
│   │   ├── DatabaseContextTests.cs
│   │   ├── MemoryReaderTests.cs
│   │   └── *.cs
│   ├── IntegrationTests/
│   │   ├── CollectionServiceTests.cs
│   │   └── *.cs
│   └── AkadaemiaAnyder.Tests.csproj
│
├── docs/                             # Documentation
│   ├── ARCHITECTURE.md               # Architecture and design
│   ├── DEVELOPMENT.md                # This file
│   ├── MEMORY-STRUCTURES.md          # Memory reading details
│   └── *.md
│
└── SamplePlugin.sln                  # Solution file
```

## Adding New Collection Types

To extend the plugin with a new collection type:

### 1. Create Domain Model

In `SamplePlugin/CoreModels/`:

```csharp
public class MyCollection
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime DiscoveredAt { get; set; }
}
```

### 2. Create EF Core Model

In `SamplePlugin/Data/Models/`:

```csharp
public class MyCollectionModel
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime DiscoveredAt { get; set; }
}
```

### 3. Add DbSet to DatabaseContext

In `SamplePlugin/Data/DatabaseContext.cs`:

```csharp
public DbSet<MyCollectionModel> MyCollections { get; set; }
```

### 4. Create Migration

```bash
dotnet ef migrations add AddMyCollection --project SamplePlugin
```

### 5. Create Repository

In `SamplePlugin/Data/Repositories/`:

```csharp
public class MyCollectionRepository
{
    private readonly DatabaseContext _context;

    public async Task AddAsync(MyCollection collection)
    {
        var model = new MyCollectionModel { ... };
        _context.MyCollections.Add(model);
        await _context.SaveChangesAsync();
    }
}
```

### 6. Add Event Listener

In `SamplePlugin/EventListeners/`:

```csharp
public class MyCollectionEventListener
{
    private readonly MyCollectionRepository _repo;

    private void OnGameEvent(Event e)
    {
        // Detect and save new collections
    }
}
```

### 7. Update CollectionService

In `SamplePlugin/Services/CollectionService.cs`, add orchestration logic.

### 8. Create UI Tab

In `SamplePlugin/Windows/MainWindow.cs`, add ImGui tab for displaying collections.

### 9. Add Tests

Create tests in `AkadaemiaAnyder.Tests/` for your new components.

For more details on the architecture, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Common Development Tasks

### Viewing Database Contents

The SQLite database is located at:
```
%APPDATA%\XIVLauncher\pluginConfigs\AkadaemiaAnyder\akadaemia.db
```

Use any SQLite viewer:
- **SQLiteStudio**: https://sqlitestudio.pl/
- **DB Browser for SQLite**: https://sqlitebrowser.org/
- **Visual Studio**: Use Server Explorer

### Resetting Database

To start fresh:
```bash
# Close FFXIV and plugin
del "%APPDATA%\XIVLauncher\pluginConfigs\AkadaemiaAnyder\akadaemia.db"

# Relaunch and plugin will recreate clean database
```

### Checking Memory Safety

Run memory safety tests:
```bash
dotnet test MemorySafetyTests/
```

### Profiling Performance

Using Visual Studio Profiler:
1. Build Release: `dotnet build -c Release`
2. **Debug → Performance Profiler** in VS
3. Select `ffxiv_dx11.exe` as target
4. Use plugin in-game while profiling
5. Analyze results

## Troubleshooting

### Build Fails: "FFXIVClientStructs not found"

Ensure XIVLauncher dev directory is set up correctly:
```bash
# Check if files exist
ls "%APPDATA%\XIVLauncher\addon\Hooks\dev\"
```

If missing, reinstall Dalamud in XIVLauncher.

### Plugin Doesn't Load In-Game

1. Verify DLL location:
   ```bash
   dir "%APPDATA%\XIVLauncher\devPlugins\AkadaemiaAnyder\"
   ```

2. Check logs:
   ```bash
   type "%APPDATA%\XIVLauncher\logs\dalamud.log"
   ```

3. Rebuild and reload:
   ```bash
   dotnet build SamplePlugin/SamplePlugin.csproj -c Debug
   # In-game: /xlplugins → disable → enable
   ```

### Database Initialization Fails

The plugin handles this automatically, but you can check logs:
```bash
# In-game: /xllog
# Or file: %APPDATA%\XIVLauncher\logs\
```

The 3-tier fallback will activate automatically on corruption.

### Memory Reading Returns Null

This is expected on first run. The plugin learns your recipe list during the first collection scan. Type `/akadaemia` and click "Scan Collections".

## Resources

- **Dalamud Docs**: https://dalamud.dev/
- **XIVLauncher GitHub**: https://github.com/goatcorp/FFXIVQuickLauncher
- **FFXIVClientStructs**: https://github.com/aers/FFXIVClientStructs
- **xUnit Documentation**: https://xunit.net/docs/getting-started/netcore
- **Entity Framework Core**: https://docs.microsoft.com/en-us/ef/core/

## Contributing

Please follow these guidelines:

1. Create a feature branch: `git checkout -b feature/your-feature`
2. Make changes and test thoroughly
3. Run all tests: `dotnet test`
4. Commit with clear messages: `git commit -m "Add feature description"`
5. Push and create a pull request

See [CONTRIBUTING.md](../CONTRIBUTING.md) for more details.
