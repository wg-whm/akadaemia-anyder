# Akadaemia Anyder

> Your Eorzean Collection Archive

A local memory-reading collection tracker for Final Fantasy XIV that automatically monitors what you've obtained without relying on Lodestone scraping.

## Overview

Akadaemia Anyder is a collection tracking tool that reads game memory to provide real-time tracking of your FFXIV collections. Named after the ancient Amaurotine academy, this tool aims to catalog your accomplishments with the precision of the Ancients' creation magic.

## Features (Planned)

- **Crafting & Gathering Logs**: Track recipe completion and gathering node progress
- **Blue Mage Spells**: Monitor learned spells from your spellbook
- **Quest Progress**: Track MSQ, side quests, and beast tribe progression
- **Duty Completion**: Monitor dungeon, trial, and raid clears
- **Sightseeing Log**: Automated vista completion tracking
- **Relic Weapons**: Track progress on all relic quest chains

## Project Status

🚧 **Early Development** - Project structure being established

## Architecture

This tool operates entirely locally by reading FFXIV game memory, similar to how ACT and Dalamud plugins function. All data remains on your machine.

### Technology Stack

- Memory reading via process inspection
- Local data storage
- No network calls or data transmission

## Legal & ToS Considerations

⚠️ **Important**: Like ACT and other third-party tools, this application violates FFXIV's Terms of Service §2.5 (data mining via unauthorized software). 

**Risk Profile**: Memory-reading collection trackers have no documented enforcement history. However:
- Never mention this tool in-game
- Use at your own risk
- Account suspension is possible per ToS

## Development

### Project Structure

```
akadaemia-anyder/
├── docs/           # Documentation and research
├── src/            # Source code
├── tests/          # Test suite
└── README.md
```

## Etymology

**Akadaemia Anyder** (Greek: "Waterless Academy") - A dungeon in FFXIV's Shadowbringers expansion located in the phantom city of Amaurot. The academy served as a repository of knowledge for the ancient civilization.

## License

MIT License - See LICENSE file for details

## Contributing

Contributions welcome! Please see CONTRIBUTING.md for guidelines.

## Disclaimer

This project is not affiliated with or endorsed by Square Enix. FINAL FANTASY is a registered trademark of Square Enix Holdings Co., Ltd.
