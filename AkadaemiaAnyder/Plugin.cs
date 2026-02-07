using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SamplePlugin.Windows;
using AkadaemiaAnyder.Data;
using AkadaemiaAnyder.Data.Repositories;
using SamplePlugin.Services;
using SamplePlugin.MemoryReaders;
using SamplePlugin.EventListeners;
using AkadaemiaAnyder.Core.Models;
using AkadaemiaAnyder.Data.Services;
using System.Collections.Generic;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/akadaemia";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("AkadaemiaAnyder");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    // Service instances
    private readonly DatabaseContext databaseContext;
    private readonly CollectionRepository collectionRepository;
    private readonly RecipeRepository recipeRepository;
    private readonly GatheringRepository gatheringRepository;
    private readonly FishingRepository fishingRepository;
    private readonly LoggingService loggingService;
    private readonly RecipeReader recipeReader;
    private readonly SafeMemoryReader<List<CraftingRecipe>> safeRecipeReader;
    private readonly GatheringEventListener gatheringListener;
    private readonly FishingEventListener fishingListener;
    private readonly CollectionService collectionService;
    private readonly ProgressCalculator progressCalculator;
    private readonly ChangeDetector changeDetector;
    private readonly JsonExporter jsonExporter;
    private readonly JsonImporter jsonImporter;
    private readonly TelemetryService telemetryService;
    private readonly MaterialAvailabilityRepository materialAvailabilityRepository;
    private readonly MaterialAvailabilityCacheService materialCacheService;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // 1. Initialize logging FIRST
        loggingService = new LoggingService(Log);
        Log.Information("[AkadaemiaAnyder] Plugin initialization started");

        // 2. Initialize database with 3-tier fallback (automatically handled by DatabaseContext constructor)
        var configDirectory = PluginInterface.ConfigDirectory.FullName;
        databaseContext = new DatabaseContext(Log, configDirectory);

        var tier = databaseContext.GetHealthStatus();
        Log.Information($"[AkadaemiaAnyder] Database initialized: {tier}");

        // 3. Initialize repositories
        collectionRepository = new CollectionRepository(databaseContext, Log);
        recipeRepository = new RecipeRepository(databaseContext, Log);
        gatheringRepository = new GatheringRepository(databaseContext, Log);
        fishingRepository = new FishingRepository(databaseContext, Log);
        materialAvailabilityRepository = new MaterialAvailabilityRepository(databaseContext, Log);

        // 3.5 Initialize cache services
        materialCacheService = new MaterialAvailabilityCacheService(
            materialAvailabilityRepository,
            Log,
            ClientState
        );
        Log.Information("[AkadaemiaAnyder] Material cache service initialized");

        // 4. Initialize memory readers and event listeners
        recipeReader = new RecipeReader(Log, DataManager);
        safeRecipeReader = new SafeMemoryReader<List<CraftingRecipe>>(
            recipeReader,
            (msg) => Log.Error(msg),
            (msg) => Log.Warning(msg)
        );

        gatheringListener = new GatheringEventListener(Framework, ClientState, Log);
        fishingListener = new FishingEventListener(Framework, ClientState, Log);

        // Start event listeners
        gatheringListener.Start();
        fishingListener.Start();
        Log.Information("[AkadaemiaAnyder] Event listeners started");

        // 5. Initialize services
        collectionService = new CollectionService(
            collectionRepository,
            recipeRepository,
            gatheringRepository,
            fishingRepository,
            recipeReader,
            gatheringListener,
            fishingListener,
            ClientState,
            Log
        );

        progressCalculator = new ProgressCalculator(
            collectionRepository,
            recipeRepository,
            gatheringRepository,
            fishingRepository,
            Log
        );

        changeDetector = new ChangeDetector(
            collectionRepository,
            recipeRepository,
            gatheringRepository,
            fishingRepository,
            Log
        );

        jsonExporter = new JsonExporter(
            collectionRepository,
            recipeRepository,
            gatheringRepository,
            fishingRepository,
            databaseContext,
            Log
        );

        jsonImporter = new JsonImporter(
            collectionRepository,
            recipeRepository,
            gatheringRepository,
            fishingRepository,
            Log
        );

        telemetryService = new TelemetryService(Log);

        // 6. Initialize UI
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(
            this,
            collectionService,
            progressCalculator,
            jsonExporter,
            jsonImporter,
            databaseContext,
            loggingService,
            Configuration,
            materialCacheService
        );

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        // 7. Register commands
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Akadaemia Anyder collection tracker"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information("[AkadaemiaAnyder] Plugin initialization complete");
    }

    public void Dispose()
    {
        Log.Information("[AkadaemiaAnyder] Plugin disposal started");

        // 1. Stop event listeners FIRST (prevent new data)
        if (gatheringListener != null && gatheringListener.IsActive)
        {
            gatheringListener.Stop();
        }
        if (fishingListener != null && fishingListener.IsActive)
        {
            fishingListener.Stop();
        }
        Log.Information("[AkadaemiaAnyder] Event listeners stopped");

        // 2. Unregister UI callbacks
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        // 3. Dispose windows
        WindowSystem.RemoveAllWindows();
        ConfigWindow?.Dispose();
        MainWindow?.Dispose();

        // 4. Unregister commands
        CommandManager.RemoveHandler(CommandName);

        // 5. Dispose database connection
        databaseContext?.Dispose();

        Log.Information("[AkadaemiaAnyder] Plugin disposal complete");
    }

    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our main ui
        MainWindow.Toggle();
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
