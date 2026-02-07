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
    /// Repository for crafting list management and recipe tracking.
    /// Handles CRUD operations for crafting lists, items, history, and sessions.
    /// Implements SQLite-specific optimizations with retry logic and transactions.
    /// </summary>
    public class CraftingListRepository
    {
        private readonly DatabaseContext context;
        private readonly IPluginLog log;
        private const int MaxRetryAttempts = 3;
        private const int BaseRetryDelayMs = 100;
        private const int BusyTimeoutMs = 5000;

        public CraftingListRepository(DatabaseContext databaseContext, IPluginLog pluginLog)
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
        /// Saves a crafting list with all its items in a transaction.
        /// Uses INSERT OR REPLACE to handle both new and updated lists.
        /// </summary>
        public async Task<bool> SaveCraftingListAsync(CraftingListData list)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"SaveCraftingListAsync: id={list.Id}, name={list.Name}, items={list.Items.Count}");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return false;
                }

                using var transaction = context.Connection.BeginTransaction();
                try
                {
                    // INSERT OR REPLACE crafting_lists entry
                    var listSql = @"
                        INSERT OR REPLACE INTO crafting_lists (id, name, character_content_id, created_at, last_modified_at)
                        VALUES (@id, @name, @character_content_id, @created_at, @last_modified_at)";

                    using var listCmd = context.Connection.CreateCommand();
                    listCmd.Transaction = transaction;
                    listCmd.CommandText = listSql;
                    listCmd.Parameters.AddWithValue("@id", list.Id);
                    listCmd.Parameters.AddWithValue("@name", list.Name);
                    listCmd.Parameters.AddWithValue("@character_content_id", list.CharacterContentId);
                    listCmd.Parameters.AddWithValue("@created_at", list.Created.ToString("o"));
                    listCmd.Parameters.AddWithValue("@last_modified_at", list.LastModified.ToString("o"));

                    await listCmd.ExecuteNonQueryAsync();

                    // Delete old items for this list (cascade by FK, but explicit is safer)
                    var deleteItemsSql = "DELETE FROM crafting_list_items WHERE list_id = @list_id";
                    using var deleteCmd = context.Connection.CreateCommand();
                    deleteCmd.Transaction = transaction;
                    deleteCmd.CommandText = deleteItemsSql;
                    deleteCmd.Parameters.AddWithValue("@list_id", list.Id);
                    await deleteCmd.ExecuteNonQueryAsync();

                    // INSERT new items
                    var itemSql = @"
                        INSERT INTO crafting_list_items (list_id, recipe_id, recipe_name, quantity, quantity_crafted, craft_type)
                        VALUES (@list_id, @recipe_id, @recipe_name, @quantity, @quantity_crafted, @craft_type)";

                    foreach (var item in list.Items)
                    {
                        using var itemCmd = context.Connection.CreateCommand();
                        itemCmd.Transaction = transaction;
                        itemCmd.CommandText = itemSql;
                        itemCmd.Parameters.AddWithValue("@list_id", list.Id);
                        itemCmd.Parameters.AddWithValue("@recipe_id", item.RecipeId);
                        itemCmd.Parameters.AddWithValue("@recipe_name", item.RecipeName ?? string.Empty);
                        itemCmd.Parameters.AddWithValue("@quantity", item.Quantity);
                        itemCmd.Parameters.AddWithValue("@quantity_crafted", item.QuantityCrafted);
                        itemCmd.Parameters.AddWithValue("@craft_type", item.CraftType);

                        await itemCmd.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                    log.Debug($"SaveCraftingListAsync: success, saved {list.Items.Count} items");
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    log.Error($"SaveCraftingListAsync failed: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Loads a single crafting list with all its items by ID and character.
        /// </summary>
        public async Task<CraftingListData?> LoadCraftingListAsync(string listId, ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"LoadCraftingListAsync: id={listId}");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return null;
                }

                // Load list metadata
                var listSql = @"
                    SELECT id, name, character_content_id, created_at, last_modified_at
                    FROM crafting_lists
                    WHERE id = @id AND character_content_id = @character_content_id";

                using var listCmd = context.Connection.CreateCommand();
                listCmd.CommandText = listSql;
                listCmd.Parameters.AddWithValue("@id", listId);
                listCmd.Parameters.AddWithValue("@character_content_id", characterContentId);

                using var listReader = await listCmd.ExecuteReaderAsync();
                if (!await listReader.ReadAsync())
                {
                    log.Debug($"LoadCraftingListAsync: id={listId} not found");
                    return null;
                }

                var list = new CraftingListData
                {
                    Id = listReader.GetString(0),
                    Name = listReader.GetString(1),
                    CharacterContentId = (ulong)listReader.GetInt64(2),
                    Created = DateTime.Parse(listReader.GetString(3)),
                    LastModified = DateTime.Parse(listReader.GetString(4)),
                    Items = new List<CraftingListItemData>()
                };

                listReader.Close();

                // Load items for this list
                var itemsSql = @"
                    SELECT recipe_id, recipe_name, quantity, quantity_crafted, craft_type
                    FROM crafting_list_items
                    WHERE list_id = @list_id
                    ORDER BY rowid ASC";

                using var itemsCmd = context.Connection.CreateCommand();
                itemsCmd.CommandText = itemsSql;
                itemsCmd.Parameters.AddWithValue("@list_id", listId);

                using var itemsReader = await itemsCmd.ExecuteReaderAsync();
                while (await itemsReader.ReadAsync())
                {
                    list.Items.Add(new CraftingListItemData
                    {
                        RecipeId = (uint)itemsReader.GetInt64(0),
                        RecipeName = itemsReader.GetString(1),
                        Quantity = itemsReader.GetInt32(2),
                        QuantityCrafted = itemsReader.GetInt32(3),
                        CraftType = itemsReader.GetByte(4)
                    });
                }

                log.Debug($"LoadCraftingListAsync: id={listId} loaded with {list.Items.Count} items");
                return list;
            });
        }

        /// <summary>
        /// Loads all crafting lists for a specific character.
        /// </summary>
        public async Task<List<CraftingListData>> LoadAllCraftingListsAsync(ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"LoadAllCraftingListsAsync: character_content_id={characterContentId}");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return new List<CraftingListData>();
                }

                var results = new List<CraftingListData>();

                // Get all list IDs for this character
                var listSql = @"
                    SELECT id FROM crafting_lists
                    WHERE character_content_id = @character_content_id
                    ORDER BY last_modified_at DESC";

                using var listCmd = context.Connection.CreateCommand();
                listCmd.CommandText = listSql;
                listCmd.Parameters.AddWithValue("@character_content_id", characterContentId);

                using var listReader = await listCmd.ExecuteReaderAsync();
                var listIds = new List<string>();
                while (await listReader.ReadAsync())
                {
                    listIds.Add(listReader.GetString(0));
                }

                listReader.Close();

                // Load full data for each list
                foreach (var listId in listIds)
                {
                    var list = await LoadCraftingListAsync(listId, characterContentId);
                    if (list != null)
                    {
                        results.Add(list);
                    }
                }

                log.Debug($"LoadAllCraftingListsAsync: loaded {results.Count} lists");
                return results;
            });
        }

        /// <summary>
        /// Deletes a crafting list and all its items (cascading delete).
        /// </summary>
        public async Task<bool> DeleteCraftingListAsync(string listId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"DeleteCraftingListAsync: id={listId}");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return false;
                }

                // Cascading delete handled by FK constraint, but explicit delete of items is safer
                using var transaction = context.Connection.BeginTransaction();
                try
                {
                    var deleteItemsSql = "DELETE FROM crafting_list_items WHERE list_id = @list_id";
                    using var deleteItemsCmd = context.Connection.CreateCommand();
                    deleteItemsCmd.Transaction = transaction;
                    deleteItemsCmd.CommandText = deleteItemsSql;
                    deleteItemsCmd.Parameters.AddWithValue("@list_id", listId);
                    await deleteItemsCmd.ExecuteNonQueryAsync();

                    var deleteListSql = "DELETE FROM crafting_lists WHERE id = @id";
                    using var deleteListCmd = context.Connection.CreateCommand();
                    deleteListCmd.Transaction = transaction;
                    deleteListCmd.CommandText = deleteListSql;
                    deleteListCmd.Parameters.AddWithValue("@id", listId);
                    var rowsAffected = await deleteListCmd.ExecuteNonQueryAsync();

                    transaction.Commit();
                    log.Debug($"DeleteCraftingListAsync: id={listId} deleted");
                    return rowsAffected > 0;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    log.Error($"DeleteCraftingListAsync failed: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Gets list of recipe IDs that have been crafted by this character.
        /// </summary>
        public async Task<List<uint>> GetCraftedRecipesAsync(ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"GetCraftedRecipesAsync: character_content_id={characterContentId}");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return new List<uint>();
                }

                var results = new List<uint>();

                var sql = @"
                    SELECT DISTINCT recipe_id FROM crafting_history
                    WHERE character_content_id = @character_content_id
                    ORDER BY recipe_id ASC";

                using var cmd = context.Connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@character_content_id", characterContentId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add((uint)reader.GetInt64(0));
                }

                log.Debug($"GetCraftedRecipesAsync: found {results.Count} crafted recipes");
                return results;
            });
        }

        /// <summary>
        /// Records a single crafted item in the crafting history.
        /// Inserts or updates the history entry.
        /// </summary>
        public async Task<bool> RecordCraftedRecipeAsync(uint recipeId, string recipeName, bool wasHQ, ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"RecordCraftedRecipeAsync: recipe_id={recipeId}, hq={wasHQ}");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return false;
                }

                var now = DateTime.UtcNow;

                // Try to update existing entry
                var updateSql = @"
                    UPDATE crafting_history
                    SET total_crafted = total_crafted + 1,
                        hq_count = hq_count + @hq_increment,
                        last_crafted_at = @now
                    WHERE recipe_id = @recipe_id AND character_content_id = @character_content_id";

                using var updateCmd = context.Connection.CreateCommand();
                updateCmd.CommandText = updateSql;
                updateCmd.Parameters.AddWithValue("@recipe_id", recipeId);
                updateCmd.Parameters.AddWithValue("@character_content_id", characterContentId);
                updateCmd.Parameters.AddWithValue("@hq_increment", wasHQ ? 1 : 0);
                updateCmd.Parameters.AddWithValue("@now", now.ToString("o"));

                var rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                // If no rows updated, insert new entry
                if (rowsAffected == 0)
                {
                    var insertSql = @"
                        INSERT INTO crafting_history (recipe_id, recipe_name, character_content_id, total_crafted, hq_count, first_crafted_at, last_crafted_at)
                        VALUES (@recipe_id, @recipe_name, @character_content_id, 1, @hq_count, @now, @now)";

                    using var insertCmd = context.Connection.CreateCommand();
                    insertCmd.CommandText = insertSql;
                    insertCmd.Parameters.AddWithValue("@recipe_id", recipeId);
                    insertCmd.Parameters.AddWithValue("@recipe_name", recipeName ?? string.Empty);
                    insertCmd.Parameters.AddWithValue("@character_content_id", characterContentId);
                    insertCmd.Parameters.AddWithValue("@hq_count", wasHQ ? 1 : 0);
                    insertCmd.Parameters.AddWithValue("@now", now.ToString("o"));

                    await insertCmd.ExecuteNonQueryAsync();
                }

                log.Debug($"RecordCraftedRecipeAsync: recipe_id={recipeId} recorded");
                return true;
            });
        }

        /// <summary>
        /// Gets crafting history for a specific recipe and character.
        /// </summary>
        public async Task<CraftingHistory?> GetCraftingHistoryAsync(uint recipeId, ulong characterContentId)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"GetCraftingHistoryAsync: recipe_id={recipeId}");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return null;
                }

                var sql = @"
                    SELECT recipe_id, recipe_name, total_crafted, hq_count, first_crafted_at, last_crafted_at
                    FROM crafting_history
                    WHERE recipe_id = @recipe_id AND character_content_id = @character_content_id";

                using var cmd = context.Connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@recipe_id", recipeId);
                cmd.Parameters.AddWithValue("@character_content_id", characterContentId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    log.Debug($"GetCraftingHistoryAsync: recipe_id={recipeId} not found");
                    return null;
                }

                var history = new CraftingHistory
                {
                    RecipeId = (uint)reader.GetInt64(0),
                    RecipeName = reader.GetString(1),
                    TotalCrafted = reader.GetInt32(2),
                    HQCount = reader.GetInt32(3),
                    FirstCrafted = DateTime.Parse(reader.GetString(4)),
                    LastCrafted = DateTime.Parse(reader.GetString(5))
                };

                log.Debug($"GetCraftingHistoryAsync: recipe_id={recipeId} retrieved");
                return history;
            });
        }

        /// <summary>
        /// Records a crafting session with start/end times and item counts.
        /// </summary>
        public async Task<bool> RecordCraftingSessionAsync(CraftingSessionData session)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"RecordCraftingSessionAsync: id={session.Id}, items={session.ItemsCrafted}");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return false;
                }

                var insertSql = @"
                    INSERT INTO crafting_sessions (id, character_content_id, start_time, end_time, items_crafted, hq_count, recipe_ids_json)
                    VALUES (@id, @character_content_id, @start_time, @end_time, @items_crafted, @hq_count, @recipe_ids_json)";

                using var cmd = context.Connection.CreateCommand();
                cmd.CommandText = insertSql;
                cmd.Parameters.AddWithValue("@id", session.Id);
                cmd.Parameters.AddWithValue("@character_content_id", session.CharacterContentId);
                cmd.Parameters.AddWithValue("@start_time", session.Start.ToString("o"));
                cmd.Parameters.AddWithValue("@end_time", session.End.ToString("o"));
                cmd.Parameters.AddWithValue("@items_crafted", session.ItemsCrafted);
                cmd.Parameters.AddWithValue("@hq_count", session.HQCount);

                // Serialize recipe IDs to JSON
                var recipeIdsJson = System.Text.Json.JsonSerializer.Serialize(session.RecipeIds);
                cmd.Parameters.AddWithValue("@recipe_ids_json", recipeIdsJson);

                await cmd.ExecuteNonQueryAsync();
                log.Debug($"RecordCraftingSessionAsync: session {session.Id} recorded");
                return true;
            });
        }

        /// <summary>
        /// Gets recent crafting sessions for a character within the specified number of days.
        /// </summary>
        public async Task<List<CraftingSessionData>> GetRecentSessionsAsync(ulong characterContentId, int days)
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"GetRecentSessionsAsync: character_content_id={characterContentId}, days={days}");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return new List<CraftingSessionData>();
                }

                var results = new List<CraftingSessionData>();
                var cutoffDate = DateTime.UtcNow.AddDays(-days);

                var sql = @"
                    SELECT id, character_content_id, start_time, end_time, items_crafted, hq_count, recipe_ids_json
                    FROM crafting_sessions
                    WHERE character_content_id = @character_content_id AND start_time >= @cutoff_date
                    ORDER BY start_time DESC";

                using var cmd = context.Connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@character_content_id", characterContentId);
                cmd.Parameters.AddWithValue("@cutoff_date", cutoffDate.ToString("o"));

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var session = new CraftingSessionData
                    {
                        Id = reader.GetString(0),
                        CharacterContentId = (ulong)reader.GetInt64(1),
                        Start = DateTime.Parse(reader.GetString(2)),
                        End = DateTime.Parse(reader.GetString(3)),
                        ItemsCrafted = reader.GetInt32(4),
                        HQCount = reader.GetInt32(5),
                        RecipeIds = DeserializeRecipeIds(reader.GetString(6))
                    };
                    results.Add(session);
                }

                log.Debug($"GetRecentSessionsAsync: retrieved {results.Count} sessions");
                return results;
            });
        }

        /// <summary>
        /// Helper to retry operations on SQLite busy errors.
        /// </summary>
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

        /// <summary>
        /// Helper to deserialize recipe IDs from JSON.
        /// </summary>
        private List<uint> DeserializeRecipeIds(string json)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<uint>>(json) ?? new List<uint>();
            }
            catch (Exception ex)
            {
                log.Warning($"Failed to deserialize recipe IDs: {ex.Message}");
                return new List<uint>();
            }
        }

        #region Nested DTO Classes

        /// <summary>
        /// DTO for crafting list data transfer between adapter and repository.
        /// </summary>
        public class CraftingListData
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public ulong CharacterContentId { get; set; }
            public DateTime Created { get; set; }
            public DateTime LastModified { get; set; }
            public List<CraftingListItemData> Items { get; set; } = new();
        }

        /// <summary>
        /// DTO for crafting list item data.
        /// </summary>
        public class CraftingListItemData
        {
            public uint RecipeId { get; set; }
            public string RecipeName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public int QuantityCrafted { get; set; }
            public byte CraftType { get; set; }
        }

        /// <summary>
        /// DTO for crafting history data.
        /// </summary>
        public class CraftingHistory
        {
            public uint RecipeId { get; set; }
            public string RecipeName { get; set; } = string.Empty;
            public int TotalCrafted { get; set; }
            public int HQCount { get; set; }
            public DateTime FirstCrafted { get; set; }
            public DateTime LastCrafted { get; set; }
        }

        /// <summary>
        /// DTO for crafting session data.
        /// </summary>
        public class CraftingSessionData
        {
            public string Id { get; set; } = string.Empty;
            public ulong CharacterContentId { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public int ItemsCrafted { get; set; }
            public int HQCount { get; set; }
            public List<uint> RecipeIds { get; set; } = new();

            /// <summary>
            /// Computed property for session duration.
            /// </summary>
            public TimeSpan Duration => End - Start;
        }

        #endregion
    }
}
