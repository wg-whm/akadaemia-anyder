# Akadaemia Anyder: Feature Roadmap

**Last Updated:** 2026-01-26

---

## Vision

**Privacy-first FFXIV completionist toolkit** combining Artisan's crafting power with comprehensive collection/inventory tracking—all stored locally with zero network requests.

---

## Feature Tiers

### **Tier 1: Core Features (MVP)**

Must-have features that define the plugin's value proposition:

#### **From Artisan Fork (BSD-3-Clause)**
- ✅ Crafting queue/to-do management
- ✅ Dynamic crafting solver (no macro libraries needed)
- ✅ Job management and gear switching
- ✅ Macro execution automation

#### **Privacy Extensions (New Code)**
1. **Unified Inventory Tracker** ⭐ **PROMOTED FROM TIER 2**
   - Universal search across ALL storage locations
   - Character inventory (140 slots)
   - **Chocobo saddlebags** (70 slots) - critical inclusion
   - Retainers (up to 10 retainers × 175 slots)
   - Armory chest (all equipment slots)
   - Glamour dresser (400 slots)
   - Storage summary dashboard (capacity warnings)
   - Smart saddlebag suggestions
   - Material availability for crafting queue integration

2. **Collection Tracker**
   - Mounts, minions, Triple Triad cards
   - Orchestrion rolls, emotes, hairstyles
   - Bardings, Blue Mage spells, aetherytes
   - Progress tracking with completion percentages

3. **Fishing Logger**
   - Real-time catch detection
   - Bite time, weather, bait tracking
   - Size and HQ status logging
   - Spot-based organization (no GPS coordinates)

4. **Gathering Logger**
   - Real-time node detection
   - Item, node, zone tracking
   - HQ status logging

5. **Session Statistics**
   - Today's progress summaries
   - Items gathered/fished/crafted counts
   - Time played per job

6. **Anonymous Export**
   - JSON/CSV exports
   - PII stripped by default (character names, server, GPS)
   - Manual export only (no automatic uploads)

---

### **Tier 2: High-Value Extensions (Post-MVP)**

Strong user demand, clear differentiation from existing tools:

1. **Retainer Ventures Module**
   - Venture completion timers
   - Completion notifications
   - Venture history (items brought back)
   - Suggest next ventures based on item needs

2. **Housing Decorator Module**
   - Furniture catalog (1000+ items)
   - Track owned furniture (inventory + storage + retainers)
   - Wishlists for housing projects
   - Where to obtain furniture (vendor, crafting, dungeon)
   - Cost calculator for wishlist items

3. **Achievement Helper Module**
   - Track partial progress toward achievements
   - Show "close to completion" achievements (e.g., "5/10 dungeons done")
   - Suggest next steps for achievement chains
   - Cross-reference with collections (e.g., "200 mounts" achievement)

4. **Glamour Dresser Tracker**
   - Track which items are in glamour dresser (400 slot limit)
   - Show duplicates (items in both dresser AND inventory)
   - Suggest items to remove when dresser is full
   - Track prism usage over time

5. **Custom Wishlist System**
   - Create custom goals ("Collect all ARR mounts")
   - Track progress with progress bars
   - Set deadlines for goals
   - Goal templates (popular completionist goals pre-configured)

---

### **Tier 3: Nice-to-Have (Future Considerations)**

Useful but lower priority:

1. **Market Board Tracker** (local-only, privacy-focused)
   - Track YOUR market board searches (not crowdsourced)
   - Show YOUR price check history
   - Local price trend graphs
   - Watchlist alerts (notify when item drops below target price)
   - **Privacy contrast:** Universalis uploads ALL searches to cloud

2. **Loot History Logger**
   - Track all items you obtain (quest rewards, dungeon drops, vendor purchases)
   - Show "first time obtaining" notifications
   - Generate session loot summaries
   - Search loot history ("When did I get this minion?")

3. **Crafting Material Tracker**
   - Show materials you have vs need for recipes
   - Cross-reference with retainer inventory (via Unified Inventory Tracker)
   - Suggest gathering routes for missing materials
   - Track material costs over time

4. **Currency Tracker**
   - Track all currencies (gil, tomestones, seals, scrips, gemstones)
   - Historical graphs (how your gil changed over time)
   - Spending alerts ("You spent 500k gil this week")
   - Currency cap warnings ("Poetics at 1900/2000")

5. **Local Activity Log**
   - Personal gameplay journal (dungeons run, bosses killed, time played per job)
   - Session summaries ("Today: 3 dungeons, 2 trials, 15 fates, 4 hours played")
   - Job playtime tracking
   - Activity heatmaps (when you play most)

---

### **Tier 4: Advanced/Niche (Power Users)**

Specific use cases or advanced features:

1. **Macro Library Manager**
   - Store unlimited crafting/combat macros
   - Tag and search macros
   - Import/export macro collections
   - Macro versioning

2. **Rotation Analyzer** (Advanced)
   - Track combat rotations (which skills in what order)
   - Compare to optimal rotations
   - Show DPS improvement suggestions

3. **Inventory Optimizer**
   - Suggest items to discard/sell (duplicates, vendor trash)
   - Highlight valuable items to keep
   - Show items taking up space that you can re-obtain easily
   - Auto-categorize items

4. **Party/FC Member Tracker** (Privacy Opt-in)
   - Remember party members you've played with
   - Track which FC members you've seen online recently
   - Notify when friends come online

---

## Comparison: What Existing Tools DON'T Do

| Feature | Teamcraft | FFXIV Collect | Artisan | **Akadaemia Anyder** |
|---------|-----------|---------------|---------|----------------------|
| **Privacy-first** | ❌ Uploads data | ❌ Cloud sync | ✅ Local | ✅ **Local + guaranteed** |
| **Crafting queue** | ⚠️ Lists only | ❌ No | ✅ Yes | ✅ **Artisan fork** |
| **Collection tracking** | ❌ No | ✅ Cloud | ❌ No | ✅ **Local-only** |
| **Fishing logs** | ✅ Uploads | ❌ No | ❌ No | ✅ **Local-only** |
| **Gathering logs** | ⚠️ Limited | ❌ No | ❌ No | ✅ **Real-time** |
| **Universal inventory search** | ❌ No | ❌ No | ❌ No | ✅ **NEW** |
| **Saddlebag tracking** | ❌ No | ❌ No | ❌ No | ✅ **NEW** |
| **Retainer inventory** | ⚠️ Manual sync | ❌ No | ❌ No | ✅ **Auto-tracked** |
| **Housing catalog** | ❌ No | ❌ No | ❌ No | ✅ **Planned** |
| **Glamour dresser** | ❌ No | ❌ No | ❌ No | ✅ **Planned** |
| **Custom goals** | ❌ No | ❌ No | ❌ No | ✅ **Planned** |
| **Loot history** | ❌ No | ❌ No | ❌ No | ✅ **Planned** |

---

## Development Timeline

**AI-Accelerated Approach:** 6-8 weeks to MVP (vs 11-13 weeks manual)

**Strategy:** Use AI agents for parallel module development, code generation, automated testing, and documentation. Human focus on game testing, UI/UX refinement, and integration verification.

### **Phase 1: Foundation (1-1.5 weeks)** ⚡ 50% faster with AI
- Fork Artisan (AI assists with codebase review)
- Privacy retrofit (AI identifies and strips network code)
- Set up local SQLite database (AI generates EF Core models)
- Attribution documentation (AI drafts)
- **Manual:** In-game testing, code review verification

### **Phase 2: Privacy Extensions (2-3 weeks)** ⚡ 40% faster with AI + parallelization
- ✅ **Unified Inventory Tracker** (AI Agent 1: saddlebags + retainers + universal search)
- ✅ **Collection Tracker** (AI Agent 2: 8+ scanner types)
- ✅ **Fishing/Gathering Logger** (AI Agent 3: event detection)
- ✅ Unified UI with tabs (AI generates ImGui code)
- ✅ Privacy settings (AI implements)
- **Manual:** UI/UX refinement, in-game testing, event timing calibration

### **Phase 3: Polish & Testing (1-1.5 weeks)** ⚡ 30% faster with AI
- Comprehensive tests (AI generates xUnit test suites)
- Performance optimization (AI suggests, human measures)
- UI/UX polish (human-driven)
- Privacy policy documentation (AI drafts)
- User guide (AI writes, human reviews)
- **Manual:** Multi-character testing, subjective polish

### **Phase 4: Modular Architecture (4-5 days)** ⚡ 65% faster with AI
- IModule interface implementation (AI designs and codes)
- ModuleManager (AI implements)
- Refactor to IModule pattern (AI bulk refactor)
- Module settings UI (AI generates)
- Module development guide (AI documents)
- **Manual:** Integration testing, enable/disable verification

### **Phase 5: Initial Release (3-4 days)** ⚡ 50% faster with AI
- Final testing (manual in-game testing)
- Create installer (manual)
- Release notes (AI writes)
- Publish to GitHub (manual)
- Marketing materials (AI drafts README, screenshots)
- **Optional:** Submit to Dalamud repository

### **Phase 6: Post-MVP Modules (1-2 weeks per module)** ⚡ AI-accelerated
- **AI develops module:** 2-3 days per module
- **Human tests in-game:** 2-3 days per module
- **Bug fixes & refinement:** 1-2 days per module

**Module priority queue:**
1. Retainer Ventures Module
2. Housing Decorator Module
3. Achievement Helper Module
4. Loot History Logger Module

---

## AI Development Breakdown

### **What AI Automates (80-90%)**
- Code scaffolding and boilerplate
- Database models and migrations
- Test suite generation
- Documentation writing
- ImGui UI code generation
- Pattern replication (e.g., 8 collection scanners)
- Bulk refactoring (namespaces, IModule pattern)

### **What Requires Human Work**
- In-game testing with real FFXIV client
- Memory access debugging (game-specific quirks)
- UI/UX design decisions and polish
- Performance profiling with real datasets
- Event timing calibration (fishing/gathering detection)
- Integration verification (Artisan + new code)
- Final release approval and publishing

### **Timeline Summary**

| Phase | Manual | AI-Accelerated | Savings |
|-------|--------|----------------|---------|
| Foundation | 2-3 weeks | 1-1.5 weeks | ~50% |
| Privacy Extensions | 4-5 weeks | 2-3 weeks | ~40% |
| Polish & Testing | 2 weeks | 1-1.5 weeks | ~30% |
| Modular Architecture | 2 weeks | 4-5 days | ~65% |
| Initial Release | 1 week | 3-4 days | ~50% |
| **TOTAL TO MVP** | **11-13 weeks** | **6-8 weeks** | **~45%** |

**Target:** 7 weeks (middle of range, includes buffer)

---

## Success Metrics

### **Privacy Compliance (Hard Requirements)**
- ✅ Zero network requests in production build
- ✅ No user IDs stored (database schema enforces this)
- ✅ No character names stored (unless explicitly enabled with consent)
- ✅ All data in local SQLite database
- ✅ Attribution properly displayed (BSD-3-Clause compliance)

### **Functionality (MVP Goals)**
- ✅ Crafting queue works (Artisan fork functional)
- ✅ Universal inventory search works (saddlebags, retainers, all storage)
- ✅ Collection scanning works (8+ collection types)
- ✅ Fishing logging works (real-time detection)
- ✅ Gathering logging works (real-time detection)
- ✅ UI responsive and intuitive

### **Modularity (Architecture Goals)**
- ✅ Can enable/disable modules independently
- ✅ Can add new modules without modifying core
- ✅ Module API documented for future development

---

## User Value Proposition

**Tagline:** "Your data, your computer. Period."

**Elevator Pitch:**
> Akadaemia Anyder is the privacy-first completionist toolkit for FFXIV. We combine Artisan's proven crafting power with comprehensive collection, inventory, and fishing/gathering tracking—all without ever uploading your data anywhere. No character names stored, no user IDs, no analytics, no cloud sync. Just pure local tracking with optional manual exports. Open source so you can verify there's no phone-home code.

**For whom:**
- Completionists who value privacy
- Players in regions with data privacy concerns
- Multi-character players who don't want persistent tracking
- Anyone who wants universal inventory search (saddlebags, retainers, etc.)
- Crafters who need material availability checking

**Key Differentiators:**
1. **Universal Inventory Search** - NO existing tool does this (includes saddlebags!)
2. **Privacy-First Architecture** - Database schema enforces no PII
3. **Artisan Fork** - Proven crafting queue system with privacy enhancements
4. **Local-Only** - Zero network stack = nothing to audit
5. **Open Source** - MIT + BSD-3-Clause licensed, fully auditable

---

## Next Steps

**Immediate actions:**
1. Fork Artisan repository
2. Review codebase structure
3. Plan integration points
4. Set up development environment

**Week 1-2:**
- Integrate Artisan into project
- Strip cloud features
- Create privacy database layer

**Week 3-4:**
- Implement Unified Inventory Tracker
- Build universal search functionality
- Add saddlebag scanning

**Week 5-6:**
- Implement Collection Tracker
- Implement Fishing/Gathering Loggers
- Build unified UI

Ready to begin implementation!
