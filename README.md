# Akadaemia Anyder

A Dalamud plugin for Final Fantasy XIV that tracks crafting recipes, gathering nodes, and fishing holes across all your characters.

## Features

### ✅ Currently Implemented
- **Recipe Tracking**: Monitors all crafting recipes learned via memory reading from your character's recipe book
- **Progress Display**: Visual progress bars, completion percentages, and collection statistics for recipes
- **Multi-Character**: Automatically tracks collections per character
- **Export/Import**: JSON-based backup and restore for all collection data
- **Database Reliability**: 3-tier fallback strategy (file → in-memory → degraded mode)
- **Performance**: Optimized memory reading with safe, bounded access patterns

### ⚠️ Infrastructure Complete, Detection Logic Pending
- **Gathering Tracking**: Event listeners and database ready, game event detection not yet implemented
- **Fishing Tracking**: Event listeners and database ready, game event detection not yet implemented

> **Note**: Gathering and Fishing require FFXIVClientStructs APIs that are currently stub-only. Infrastructure is built and ready; detection logic requires additional research into Dalamud's game state APIs.

## Installation

### Using Dalamud Plugin Repository (Recommended)

1. Install [XIVLauncher](https://github.com/goatcorp/FFXIVQuickLauncher)
2. Launch FFXIV through XIVLauncher
3. Type `/xlplugins` in-game to open Plugin Installer
4. Search for "Akadaemia Anyder" and install
5. Type `/akadaemia` in-game to open the tracker

### Development Build

For development, see [DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Usage

### Basic Commands

- `/akadaemia` - Toggle main tracker window
- `/akadaemia-config` - Open settings and advanced options

### In-Game Features

1. **Main Window**
   - Tabs for Recipes, Gathering, and Fishing collections
   - Real-time progress indicators
   - Completion statistics
   - Character selector

2. **Collection Management**
   - Click "Scan Collections" to update your current progress
   - View detailed breakdown by category
   - Export collections to JSON file
   - Import previously exported collections

3. **Settings**
   - Database health status
   - Import/export options
   - Character management
   - Display preferences

## Architecture

The plugin uses a sophisticated architecture designed for reliability and performance:

- **Hybrid Tracking**: Memory reading for recipes (one-time load), event listening for gathering/fishing (real-time)
- **3-Tier Database**: Automatic fallback from file-based SQLite → in-memory fallback → degraded mode on corruption
- **Repository Pattern**: Clean data access layer with retry logic and error handling
- **SafeMemoryReader**: Bounded, exception-safe memory access with automatic fallback
- **ImGui UI**: Native FFXIV overlay interface that integrates seamlessly with game UI

For detailed architecture documentation, see [ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Development

To contribute or build locally, see [DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Requirements

- FINAL FANTASY XIV (any region, any expansion level)
- Dalamud plugin system enabled via XIVLauncher
- Windows 10 or later

## License

MIT License - see [LICENSE](LICENSE) file

## Troubleshooting

### Plugin doesn't load
- Ensure XIVLauncher is configured correctly
- Run `/xlplugins` to check Dalamud status
- Check plugin compatibility with your Dalamud version

### Database errors
- Plugin automatically recovers from database corruption
- Check game logs in `%APPDATA%\XIVLauncher\log\` for detailed error messages
- Database is stored in `%APPDATA%\XIVLauncher\pluginConfigs\AkadaemiaAnyder\akadaemia.db`

### Collection data missing
- Use export feature from Settings to backup current data
- Re-scan collections with "Scan Collections" button
- Import previously exported JSON files if needed

## Author

wgdevelopment

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.
