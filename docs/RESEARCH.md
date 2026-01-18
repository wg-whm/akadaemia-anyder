# Research Notes

## Square Enix ToS Analysis

### Official Policy
- **Section 2.5**: Prohibits data mining via "unauthorized third-party software"
- No exceptions for personal use, read-only, or collection tracking
- Violations: reverse engineering, packet modification, UI modifications displaying extra information

### Enforcement Reality
- "Don't ask, don't tell" policy in practice
- Bans triggered by: harassment with parse data, public tool promotion/streaming, competitive advantage tools, datamining unreleased content
- **No documented bans** for memory-reading collection trackers or passive monitoring tools
- ACT/Dalamud have no ban history despite millions of users

### Risk Mitigation
- Never mention tool in-game
- Avoid streaming with tool visible
- No automated actions or server interaction
- Read-only memory access

## Existing Tracker Ecosystem

### Web-Based (Lodestone Scrapers)
**FFXIV Collect** - Updates via Lodestone profile scraping, tracks achievements/mounts/minions/facewear
**Lalachievements** - Scrapes achievement rankings, daily background updates
*These are ToS-compliant since they use public data*

### Memory-Reading (Dalamud Plugins)
**Good Memory** - Shows ownership indicators in item tooltips
**Collections (seventhxiv)** - Full interface for tracking/discovering collections
**WhichMount** - Mount acquisition info via context menu
*These violate ToS §2.5 but have zero enforcement history*

### GitHub Tool History
- No DMCA takedowns or repo closures observed
- Major repos (ACT, Dalamud, XIVAPI) operate openly for years
- Enforcement targets user behavior, not tool development
