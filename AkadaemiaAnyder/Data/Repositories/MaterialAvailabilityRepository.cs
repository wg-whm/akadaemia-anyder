using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Microsoft.Data.Sqlite;
using AkadaemiaAnyder.Data.Models;

namespace AkadaemiaAnyder.Data.Repositories
{
    /// <summary>
    /// Repository for querying material availability from local inventory data.
    /// Replaces Universalis API with local-only inventory tracking across all storage locations.
    /// </summary>
    public class MaterialAvailabilityRepository
    {
        private readonly DatabaseContext context;
        private readonly IPluginLog log;
        private const int MaxRetryAttempts = 3;
        private const int BaseRetryDelayMs = 100;
        private const int BusyTimeoutMs = 5000;

        public MaterialAvailabilityRepository(DatabaseContext databaseContext, IPluginLog pluginLog)
        {
            context = databaseContext;
            log = pluginLog;

            // Set SQLite busy timeout
            if (context.Connection != null)
            {
                using var cmd = context.Connection.CreateCommand();
                cmd.CommandText = $"PRAGMA busy_timeout = {BusyTimeoutMs}";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Gets material availability for a single item, aggregated across all storage locations.
        /// </summary>
        public async Task<MaterialAvailability> GetMaterialAvailabilityAsync(uint itemId, ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"GetMaterialAvailabilityAsync(itemId={itemId}, characterId={characterContentId})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return MaterialAvailability.NotFound(itemId);
                }

                const string sql = @"
                    SELECT
                        item_id,
                        item_name,
                        location,
                        COALESCE(SUM(quantity), 0) as total_quantity
                    FROM inventory_items
                    WHERE character_content_id = @character_content_id
                        AND item_id = @item_id
                    GROUP BY item_id, item_name, location";

                using var command = context.Connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@character_content_id", (long)characterContentId);
                command.Parameters.AddWithValue("@item_id", itemId);

                var availability = new MaterialAvailability
                {
                    ItemId = itemId,
                    ByLocation = new Dictionary<string, int>()
                };

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (availability.ItemName == string.Empty)
                    {
                        availability.ItemName = reader.GetString(reader.GetOrdinal("item_name"));
                    }

                    var location = reader.GetString(reader.GetOrdinal("location"));
                    var quantity = reader.GetInt32(reader.GetOrdinal("total_quantity"));

                    availability.ByLocation[location] = quantity;

                    // Populate specific properties based on location
                    switch (location)
                    {
                        case "inventory":
                            availability.InInventory = quantity;
                            break;
                        case "saddlebag":
                            availability.InSaddlebag = quantity;
                            break;
                        case "armory":
                            availability.InArmoryChest = quantity;
                            break;
                        case "glamour":
                            availability.InGlamourDresser = quantity;
                            break;
                        default:
                            // Retainer: retainer_N format
                            if (location.StartsWith("retainer"))
                            {
                                availability.InRetainers += quantity;
                            }
                            break;
                    }
                }

                log.Debug($"GetMaterialAvailabilityAsync returned {availability.Total} total ({availability.ByLocation.Count} locations)");
                return availability;
            });
        }

        /// <summary>
        /// Gets all material locations for a specific item (inventory slot details).
        /// Useful for finding specific HQ vs NQ or slot information.
        /// </summary>
        public async Task<List<MaterialLocation>> FindMaterialLocationsAsync(uint itemId, ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"FindMaterialLocationsAsync(itemId={itemId}, characterId={characterContentId})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return new List<MaterialLocation>();
                }

                const string sql = @"
                    SELECT
                        location,
                        slot_id,
                        quantity,
                        is_hq
                    FROM inventory_items
                    WHERE character_content_id = @character_content_id
                        AND item_id = @item_id
                    ORDER BY location, slot_id";

                using var command = context.Connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@character_content_id", (long)characterContentId);
                command.Parameters.AddWithValue("@item_id", itemId);

                var locations = new List<MaterialLocation>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    locations.Add(new MaterialLocation
                    {
                        Location = reader.GetString(reader.GetOrdinal("location")),
                        SlotId = reader.GetInt32(reader.GetOrdinal("slot_id")),
                        Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                        IsHQ = reader.GetInt32(reader.GetOrdinal("is_hq")) == 1
                    });
                }

                log.Debug($"FindMaterialLocationsAsync found {locations.Count} locations");
                return locations;
            });
        }

        /// <summary>
        /// Gets material availability for multiple items in a single query.
        /// More efficient than calling GetMaterialAvailabilityAsync multiple times.
        /// </summary>
        public async Task<Dictionary<uint, MaterialAvailability>> GetBulkMaterialAvailabilityAsync(
            IEnumerable<uint> itemIds,
            ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                var itemIdList = itemIds.ToList();
                log.Debug($"GetBulkMaterialAvailabilityAsync(count={itemIdList.Count}, characterId={characterContentId})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return new Dictionary<uint, MaterialAvailability>();
                }

                if (itemIdList.Count == 0)
                {
                    return new Dictionary<uint, MaterialAvailability>();
                }

                // Build dynamic IN clause
                var placeholders = string.Join(",", Enumerable.Range(0, itemIdList.Count).Select(i => $"@itemId{i}"));

                var sql = $@"
                    SELECT
                        item_id,
                        item_name,
                        location,
                        COALESCE(SUM(quantity), 0) as total_quantity
                    FROM inventory_items
                    WHERE character_content_id = @character_content_id
                        AND item_id IN ({placeholders})
                    GROUP BY item_id, item_name, location
                    ORDER BY item_id, location";

                using var command = context.Connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@character_content_id", (long)characterContentId);

                for (int i = 0; i < itemIdList.Count; i++)
                {
                    command.Parameters.AddWithValue($"@itemId{i}", itemIdList[i]);
                }

                var results = new Dictionary<uint, MaterialAvailability>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var itemId = (uint)reader.GetInt64(reader.GetOrdinal("item_id"));
                    var location = reader.GetString(reader.GetOrdinal("location"));
                    var quantity = reader.GetInt32(reader.GetOrdinal("total_quantity"));

                    if (!results.ContainsKey(itemId))
                    {
                        results[itemId] = new MaterialAvailability
                        {
                            ItemId = itemId,
                            ItemName = reader.GetString(reader.GetOrdinal("item_name")),
                            ByLocation = new Dictionary<string, int>()
                        };
                    }

                    var availability = results[itemId];
                    availability.ByLocation[location] = quantity;

                    // Populate specific properties
                    switch (location)
                    {
                        case "inventory":
                            availability.InInventory = quantity;
                            break;
                        case "saddlebag":
                            availability.InSaddlebag = quantity;
                            break;
                        case "armory":
                            availability.InArmoryChest = quantity;
                            break;
                        case "glamour":
                            availability.InGlamourDresser = quantity;
                            break;
                        default:
                            if (location.StartsWith("retainer"))
                            {
                                availability.InRetainers += quantity;
                            }
                            break;
                    }
                }

                // Add missing items with zero availability
                foreach (var itemId in itemIdList.Where(id => !results.ContainsKey(id)))
                {
                    results[itemId] = MaterialAvailability.NotFound(itemId);
                }

                log.Debug($"GetBulkMaterialAvailabilityAsync returned {results.Count} items");
                return results;
            });
        }

        /// <summary>
        /// Updates or inserts inventory item data.
        /// Called when game memory is scanned for inventory contents.
        /// </summary>
        public async Task<int> UpsertInventoryItemAsync(InventoryItem item, ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"UpsertInventoryItemAsync(itemId={item.ItemId}, location={item.Location}, qty={item.Quantity})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return 0;
                }

                const string sql = @"
                    INSERT OR REPLACE INTO inventory_items (
                        character_content_id, item_id, item_name, location, slot_id,
                        quantity, is_hq, last_updated_at
                    ) VALUES (
                        @character_content_id, @item_id, @item_name, @location, @slot_id,
                        @quantity, @is_hq, @last_updated_at
                    )";

                using var command = context.Connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@character_content_id", (long)characterContentId);
                command.Parameters.AddWithValue("@item_id", item.ItemId);
                command.Parameters.AddWithValue("@item_name", item.ItemName ?? string.Empty);
                command.Parameters.AddWithValue("@location", item.Location);
                command.Parameters.AddWithValue("@slot_id", item.SlotId);
                command.Parameters.AddWithValue("@quantity", item.Quantity);
                command.Parameters.AddWithValue("@is_hq", item.IsHQ ? 1 : 0);
                command.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow.ToString("o"));

                var rowsAffected = await command.ExecuteNonQueryAsync();
                log.Debug($"UpsertInventoryItemAsync affected {rowsAffected} rows");
                return rowsAffected;
            });
        }

        /// <summary>
        /// Bulk upsert inventory items in a single transaction.
        /// More efficient than calling UpsertInventoryItemAsync multiple times.
        /// </summary>
        public async Task<int> BulkUpsertInventoryItemsAsync(
            List<InventoryItem> items,
            ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"BulkUpsertInventoryItemsAsync(count={items.Count}, characterId={characterContentId})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return 0;
                }

                if (items.Count == 0)
                {
                    return 0;
                }

                using var transaction = context.Connection.BeginTransaction();
                try
                {
                    int totalAffected = 0;

                    const string sql = @"
                        INSERT OR REPLACE INTO inventory_items (
                            character_content_id, item_id, item_name, location, slot_id,
                            quantity, is_hq, last_updated_at
                        ) VALUES (
                            @character_content_id, @item_id, @item_name, @location, @slot_id,
                            @quantity, @is_hq, @last_updated_at
                        )";

                    foreach (var item in items)
                    {
                        using var command = context.Connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = sql;
                        command.Parameters.AddWithValue("@character_content_id", (long)characterContentId);
                        command.Parameters.AddWithValue("@item_id", item.ItemId);
                        command.Parameters.AddWithValue("@item_name", item.ItemName ?? string.Empty);
                        command.Parameters.AddWithValue("@location", item.Location);
                        command.Parameters.AddWithValue("@slot_id", item.SlotId);
                        command.Parameters.AddWithValue("@quantity", item.Quantity);
                        command.Parameters.AddWithValue("@is_hq", item.IsHQ ? 1 : 0);
                        command.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow.ToString("o"));

                        totalAffected += await command.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                    log.Debug($"BulkUpsertInventoryItemsAsync processed {totalAffected} rows");
                    return totalAffected;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    log.Error($"BulkUpsertInventoryItemsAsync failed: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Deletes all inventory items for a character.
        /// Used when clearing/resetting inventory data.
        /// </summary>
        public async Task<int> DeleteCharacterInventoryAsync(ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"DeleteCharacterInventoryAsync(characterId={characterContentId})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return 0;
                }

                const string sql = "DELETE FROM inventory_items WHERE character_content_id = @character_content_id";

                using var command = context.Connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@character_content_id", (long)characterContentId);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                log.Debug($"DeleteCharacterInventoryAsync deleted {rowsAffected} items");
                return rowsAffected;
            });
        }

        private async Task<TResult> RetryOnBusy<TResult>(Func<Task<TResult>> operation)
        {
            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 5 && attempt < MaxRetryAttempts)
                {
                    // SQLITE_BUSY - retry with exponential backoff
                    var delay = BaseRetryDelayMs * attempt;
                    log.Warning($"SQLite busy (attempt {attempt}/{MaxRetryAttempts}), retrying in {delay}ms...");
                    await Task.Delay(delay);
                }
            }

            // Final attempt without catch
            return await operation();
        }
    }

    /// <summary>
    /// DTO for querying material availability across all storage locations.
    /// Aggregates inventory counts by storage type.
    /// </summary>
    public class MaterialAvailability
    {
        /// <summary>
        /// The item ID being tracked.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// The item name (for display purposes).
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Quantity in character's main inventory (140 slots).
        /// </summary>
        public int InInventory { get; set; }

        /// <summary>
        /// Quantity in chocobo saddlebag (70 slots).
        /// </summary>
        public int InSaddlebag { get; set; }

        /// <summary>
        /// Quantity across all retainers (up to 10 retainers × 175 slots).
        /// </summary>
        public int InRetainers { get; set; }

        /// <summary>
        /// Quantity in glamour dresser (400 slots).
        /// </summary>
        public int InGlamourDresser { get; set; }

        /// <summary>
        /// Quantity in armory chest (all equipment slots).
        /// </summary>
        public int InArmoryChest { get; set; }

        /// <summary>
        /// Total quantity across all storage locations.
        /// </summary>
        public int Total => InInventory + InSaddlebag + InRetainers + InGlamourDresser + InArmoryChest;

        /// <summary>
        /// Breakdown by location for detailed queries.
        /// Keys: "inventory", "saddlebag", "retainer_N", "glamour", "armory"
        /// </summary>
        public Dictionary<string, int> ByLocation { get; set; } = new();

        /// <summary>
        /// Creates a "not found" response for items with zero availability.
        /// </summary>
        public static MaterialAvailability NotFound(uint itemId) => new()
        {
            ItemId = itemId,
            InInventory = 0,
            InSaddlebag = 0,
            InRetainers = 0,
            InGlamourDresser = 0,
            InArmoryChest = 0,
            ByLocation = new Dictionary<string, int>()
        };
    }

    /// <summary>
    /// DTO for a single material location (inventory slot detail).
    /// Includes HQ status and slot information for granular queries.
    /// </summary>
    public class MaterialLocation
    {
        /// <summary>
        /// Storage location identifier: "inventory", "saddlebag", "retainer_1", "glamour", "armory"
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Inventory slot ID within the location (0-indexed).
        /// </summary>
        public int SlotId { get; set; }

        /// <summary>
        /// Quantity in this specific slot.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// Whether this stack is HQ (high quality).
        /// </summary>
        public bool IsHQ { get; set; }
    }

    /// <summary>
    /// DTO for inventory item data to be stored in the database.
    /// Represents a single stack in a single slot.
    /// </summary>
    public class InventoryItem
    {
        /// <summary>
        /// Item ID from FFXIV game data.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Item name for display (cached from game data).
        /// </summary>
        public string? ItemName { get; set; }

        /// <summary>
        /// Storage location: "inventory", "saddlebag", "retainer_N", "glamour", "armory"
        /// </summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Slot ID within the location container.
        /// </summary>
        public int SlotId { get; set; }

        /// <summary>
        /// Stack quantity in this slot.
        /// </summary>
        public int Quantity { get; set; }

        /// <summary>
        /// High quality flag.
        /// </summary>
        public bool IsHQ { get; set; }
    }
}
