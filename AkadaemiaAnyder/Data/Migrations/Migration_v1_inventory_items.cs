using System;
using Dalamud.Plugin.Services;
using Microsoft.Data.Sqlite;

namespace AkadaemiaAnyder.Data.Migrations
{
    /// <summary>
    /// Migration to create the inventory_items table for tracking material availability.
    /// This table stores the current state of all items across all storage locations for each character.
    ///
    /// Storage locations tracked:
    /// - inventory: Character's main inventory (140 slots)
    /// - saddlebag: Chocobo saddlebag (70 slots)
    /// - retainer_N: Individual retainers, where N = retainer index (up to 10 retainers × 175 slots each)
    /// - glamour: Glamour dresser (400 slots)
    /// - armory: Armory chest (all equipment slots)
    /// </summary>
    public static class Migration_v1_InventoryItems
    {
        public static void Apply(SqliteConnection connection)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                CreateInventoryItemsTable(connection);
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                throw new InvalidOperationException("Failed to apply inventory_items migration", ex);
            }
        }

        private static void CreateInventoryItemsTable(SqliteConnection connection)
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS inventory_items (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    character_content_id INTEGER NOT NULL,
                    item_id INTEGER NOT NULL,
                    item_name TEXT NOT NULL,
                    location TEXT NOT NULL CHECK(location IN (
                        'inventory', 'saddlebag', 'retainer_0', 'retainer_1', 'retainer_2',
                        'retainer_3', 'retainer_4', 'retainer_5', 'retainer_6', 'retainer_7',
                        'retainer_8', 'retainer_9', 'glamour', 'armory'
                    )),
                    slot_id INTEGER NOT NULL,
                    quantity INTEGER NOT NULL CHECK(quantity > 0),
                    is_hq INTEGER NOT NULL DEFAULT 0 CHECK(is_hq IN (0, 1)),
                    last_updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,

                    UNIQUE(character_content_id, location, slot_id),
                    FOREIGN KEY(character_content_id) REFERENCES characters(content_id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_inventory_items_character ON inventory_items(character_content_id);
                CREATE INDEX IF NOT EXISTS idx_inventory_items_item ON inventory_items(item_id);
                CREATE INDEX IF NOT EXISTS idx_inventory_items_location ON inventory_items(location);
                CREATE INDEX IF NOT EXISTS idx_inventory_items_character_item ON inventory_items(character_content_id, item_id);
                CREATE INDEX IF NOT EXISTS idx_inventory_items_last_updated ON inventory_items(last_updated_at DESC);";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Placeholder for a characters table that would track character metadata.
    /// This is referenced by the inventory_items foreign key.
    ///
    /// Future implementation would store:
    /// - content_id: Character content ID from game memory
    /// - character_name: Character name (optional, per privacy settings)
    /// - world_id: World/data center
    /// - last_scanned_at: Timestamp of last inventory scan
    /// </summary>
    public static class Migration_v1_Characters
    {
        public static void Apply(SqliteConnection connection)
        {
            // Note: This migration would be used if character metadata tracking is added
            // For now, inventory_items.character_content_id is a standalone INT
            // In the future, this could be a FK to a characters table
        }
    }
}
