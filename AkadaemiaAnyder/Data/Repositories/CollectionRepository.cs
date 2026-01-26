using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Microsoft.Data.Sqlite;
using AkadaemiaAnyder.Data.Models;

namespace AkadaemiaAnyder.Data.Repositories
{
    /// <summary>
    /// Base repository implementation for collection entries.
    /// Handles SQLite locking with retry logic and transaction management.
    /// </summary>
    public class CollectionRepository : ICollectionRepository
    {
        private readonly DatabaseContext context;
        private readonly IPluginLog log;
        private const int MaxRetryAttempts = 3;
        private const int BaseRetryDelayMs = 100;
        private const int BusyTimeoutMs = 5000;

        public CollectionRepository(DatabaseContext databaseContext, IPluginLog pluginLog)
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

        public async Task<List<T>> GetAllAsync<T>() where T : CollectionEntry
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"GetAllAsync<{typeof(T).Name}>");
                var results = new List<T>();

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return results;
                }

                var sql = BuildSelectAllSql<T>();
                using var command = context.Connection.CreateCommand();
                command.CommandText = sql;

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var entry = MapFromReader<T>(reader);
                    if (entry != null)
                    {
                        results.Add(entry);
                    }
                }

                log.Debug($"GetAllAsync<{typeof(T).Name}> returned {results.Count} entries");
                return results;
            });
        }

        public async Task<T?> GetByIdAsync<T>(int id) where T : CollectionEntry
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"GetByIdAsync<{typeof(T).Name}>(id={id})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return null;
                }

                var sql = BuildSelectByIdSql<T>();
                using var command = context.Connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@id", id);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var entry = MapFromReader<T>(reader);
                    log.Debug($"GetByIdAsync<{typeof(T).Name}>(id={id}) found");
                    return entry;
                }

                log.Debug($"GetByIdAsync<{typeof(T).Name}>(id={id}) not found");
                return null;
            });
        }

        public async Task<int> InsertAsync<T>(T entry) where T : CollectionEntry
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"InsertAsync<{typeof(T).Name}>(item={entry.ItemName})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return 0;
                }

                using var transaction = context.Connection.BeginTransaction();
                try
                {
                    // Insert into collections table
                    var collectionSql = @"
                        INSERT INTO collections (character_id, character_name, world_name, type, item_id, item_name,
                                                 is_unlocked, unlocked_at, first_seen_at, last_updated_at)
                        VALUES (@character_id, @character_name, @world_name, @type, @item_id, @item_name,
                                @is_unlocked, @unlocked_at, @first_seen_at, @last_updated_at);
                        SELECT last_insert_rowid();";

                    using var collectionCmd = context.Connection.CreateCommand();
                    collectionCmd.Transaction = transaction;
                    collectionCmd.CommandText = collectionSql;
                    AddBaseParameters(collectionCmd, entry);

                    var collectionId = Convert.ToInt32(await collectionCmd.ExecuteScalarAsync());
                    entry.Id = collectionId;

                    // Insert into type-specific table
                    await InsertTypeSpecific(entry, collectionId, transaction);

                    transaction.Commit();
                    log.Debug($"InsertAsync<{typeof(T).Name}> inserted with id={collectionId}");
                    return collectionId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }

        public async Task<int> UpdateAsync<T>(T entry) where T : CollectionEntry
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"UpdateAsync<{typeof(T).Name}>(id={entry.Id})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return 0;
                }

                using var transaction = context.Connection.BeginTransaction();
                try
                {
                    // Update collections table
                    var collectionSql = @"
                        UPDATE collections
                        SET character_id = @character_id,
                            character_name = @character_name,
                            world_name = @world_name,
                            type = @type,
                            item_id = @item_id,
                            item_name = @item_name,
                            is_unlocked = @is_unlocked,
                            unlocked_at = @unlocked_at,
                            last_updated_at = @last_updated_at
                        WHERE id = @id";

                    using var collectionCmd = context.Connection.CreateCommand();
                    collectionCmd.Transaction = transaction;
                    collectionCmd.CommandText = collectionSql;
                    AddBaseParameters(collectionCmd, entry);
                    collectionCmd.Parameters.AddWithValue("@id", entry.Id);

                    var rowsAffected = await collectionCmd.ExecuteNonQueryAsync();

                    // Update type-specific table
                    if (rowsAffected > 0)
                    {
                        await UpdateTypeSpecific(entry, transaction);
                    }

                    transaction.Commit();
                    log.Debug($"UpdateAsync<{typeof(T).Name}>(id={entry.Id}) affected {rowsAffected} rows");
                    return rowsAffected;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }

        public async Task<int> DeleteAsync<T>(int id) where T : CollectionEntry
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"DeleteAsync<{typeof(T).Name}>(id={id})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return 0;
                }

                // Cascade delete is handled by foreign key constraints
                var sql = "DELETE FROM collections WHERE id = @id";
                using var command = context.Connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@id", id);

                var rowsAffected = await command.ExecuteNonQueryAsync();
                log.Debug($"DeleteAsync<{typeof(T).Name}>(id={id}) affected {rowsAffected} rows");
                return rowsAffected;
            });
        }

        public async Task<int> BulkUpsertAsync<T>(List<T> entries) where T : CollectionEntry
        {
            return await RetryOnBusy(async () =>
            {
                log.Debug($"BulkUpsertAsync<{typeof(T).Name}>(count={entries.Count})");

                if (context.Connection == null)
                {
                    log.Warning("Database connection is null");
                    return 0;
                }

                if (entries.Count == 0)
                {
                    return 0;
                }

                using var transaction = context.Connection.BeginTransaction();
                try
                {
                    int totalAffected = 0;

                    foreach (var entry in entries)
                    {
                        // INSERT OR REPLACE into collections table
                        var collectionSql = @"
                            INSERT OR REPLACE INTO collections (id, character_id, character_name, world_name, type,
                                                                item_id, item_name, is_unlocked, unlocked_at,
                                                                first_seen_at, last_updated_at)
                            VALUES (@id, @character_id, @character_name, @world_name, @type, @item_id, @item_name,
                                    @is_unlocked, @unlocked_at, @first_seen_at, @last_updated_at);
                            SELECT last_insert_rowid();";

                        using var collectionCmd = context.Connection.CreateCommand();
                        collectionCmd.Transaction = transaction;
                        collectionCmd.CommandText = collectionSql;

                        if (entry.Id > 0)
                        {
                            collectionCmd.Parameters.AddWithValue("@id", entry.Id);
                        }
                        else
                        {
                            collectionCmd.Parameters.AddWithValue("@id", DBNull.Value);
                        }

                        AddBaseParameters(collectionCmd, entry);

                        var collectionId = Convert.ToInt32(await collectionCmd.ExecuteScalarAsync());
                        entry.Id = collectionId;

                        // INSERT OR REPLACE into type-specific table
                        await UpsertTypeSpecific(entry, collectionId, transaction);
                        totalAffected++;
                    }

                    transaction.Commit();
                    log.Debug($"BulkUpsertAsync<{typeof(T).Name}> processed {totalAffected} entries");
                    return totalAffected;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    log.Error($"BulkUpsertAsync failed: {ex.Message}");
                    throw;
                }
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

        private void AddBaseParameters(SqliteCommand command, CollectionEntry entry)
        {
            command.Parameters.AddWithValue("@character_id", entry.CharacterId);
            command.Parameters.AddWithValue("@character_name", entry.CharacterName);
            command.Parameters.AddWithValue("@world_name", entry.WorldName);
            command.Parameters.AddWithValue("@type", (int)entry.Type);
            command.Parameters.AddWithValue("@item_id", entry.ItemId);
            command.Parameters.AddWithValue("@item_name", entry.ItemName);
            command.Parameters.AddWithValue("@is_unlocked", entry.IsUnlocked ? 1 : 0);
            command.Parameters.AddWithValue("@unlocked_at", entry.UnlockedAt?.ToString("o") ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@first_seen_at", entry.FirstSeenAt.ToString("o"));
            command.Parameters.AddWithValue("@last_updated_at", entry.LastUpdatedAt.ToString("o"));
        }

        private string BuildSelectAllSql<T>() where T : CollectionEntry
        {
            if (typeof(T) == typeof(RecipeEntry))
            {
                return @"
                    SELECT c.*, r.recipe_id, r.recipe_level, r.crafting_class, r.is_master_recipe,
                           r.master_book_id, r.item_level
                    FROM collections c
                    INNER JOIN recipes r ON c.id = r.collection_id
                    WHERE c.type = 1";
            }
            else if (typeof(T) == typeof(GatheringNodeEntry))
            {
                return @"
                    SELECT c.*, g.node_id, g.gathering_class, g.zone, g.folklore_book_id,
                           g.node_level, g.is_legendary, g.is_ephemeral
                    FROM collections c
                    INNER JOIN gathering_nodes g ON c.id = g.collection_id
                    WHERE c.type = 2";
            }
            else if (typeof(T) == typeof(FishingHoleEntry))
            {
                return @"
                    SELECT c.*, f.fish_id, f.fishing_hole_id, f.zone, f.recommended_bait,
                           f.is_big_fish, f.weather_requirement, f.time_requirement
                    FROM collections c
                    INNER JOIN fishing_holes f ON c.id = f.collection_id
                    WHERE c.type = 3";
            }
            else
            {
                return "SELECT * FROM collections";
            }
        }

        private string BuildSelectByIdSql<T>() where T : CollectionEntry
        {
            var baseSql = BuildSelectAllSql<T>();
            return baseSql.Contains("WHERE")
                ? $"{baseSql} AND c.id = @id"
                : $"{baseSql} WHERE id = @id";
        }

        private T? MapFromReader<T>(SqliteDataReader reader) where T : CollectionEntry
        {
            var entry = Activator.CreateInstance<T>();

            // Map base properties
            entry.Id = reader.GetInt32(reader.GetOrdinal("id"));
            entry.CharacterId = reader.GetInt32(reader.GetOrdinal("character_id"));
            entry.CharacterName = reader.GetString(reader.GetOrdinal("character_name"));
            entry.WorldName = reader.GetString(reader.GetOrdinal("world_name"));
            entry.Type = (CollectionType)reader.GetInt32(reader.GetOrdinal("type"));
            entry.ItemId = reader.GetInt32(reader.GetOrdinal("item_id"));
            entry.ItemName = reader.GetString(reader.GetOrdinal("item_name"));
            entry.IsUnlocked = reader.GetInt32(reader.GetOrdinal("is_unlocked")) == 1;

            var unlockedAtOrdinal = reader.GetOrdinal("unlocked_at");
            entry.UnlockedAt = reader.IsDBNull(unlockedAtOrdinal)
                ? null
                : DateTime.Parse(reader.GetString(unlockedAtOrdinal));

            entry.FirstSeenAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("first_seen_at")));
            entry.LastUpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_updated_at")));

            // Map type-specific properties
            if (entry is RecipeEntry recipeEntry)
            {
                recipeEntry.RecipeId = reader.GetInt32(reader.GetOrdinal("recipe_id"));
                recipeEntry.RecipeLevel = reader.GetInt32(reader.GetOrdinal("recipe_level"));
                recipeEntry.CraftingClass = (CraftingClass)reader.GetInt32(reader.GetOrdinal("crafting_class"));
                recipeEntry.IsMasterRecipe = reader.GetInt32(reader.GetOrdinal("is_master_recipe")) == 1;

                var masterBookOrdinal = reader.GetOrdinal("master_book_id");
                recipeEntry.MasterBookId = reader.IsDBNull(masterBookOrdinal)
                    ? null
                    : reader.GetInt32(masterBookOrdinal);

                recipeEntry.ItemLevel = reader.GetInt32(reader.GetOrdinal("item_level"));
            }
            else if (entry is GatheringNodeEntry gatheringEntry)
            {
                gatheringEntry.NodeId = reader.GetInt32(reader.GetOrdinal("node_id"));
                gatheringEntry.GatheringClass = (GatheringClass)reader.GetInt32(reader.GetOrdinal("gathering_class"));
                gatheringEntry.Zone = reader.GetString(reader.GetOrdinal("zone"));

                var folkloreOrdinal = reader.GetOrdinal("folklore_book_id");
                gatheringEntry.FolkloreBookId = reader.IsDBNull(folkloreOrdinal)
                    ? null
                    : reader.GetInt32(folkloreOrdinal);

                gatheringEntry.NodeLevel = reader.GetInt32(reader.GetOrdinal("node_level"));
                gatheringEntry.IsLegendary = reader.GetInt32(reader.GetOrdinal("is_legendary")) == 1;
                gatheringEntry.IsEphemeral = reader.GetInt32(reader.GetOrdinal("is_ephemeral")) == 1;
            }
            else if (entry is FishingHoleEntry fishingEntry)
            {
                fishingEntry.FishId = reader.GetInt32(reader.GetOrdinal("fish_id"));
                fishingEntry.FishingHoleId = reader.GetInt32(reader.GetOrdinal("fishing_hole_id"));
                fishingEntry.Zone = reader.GetString(reader.GetOrdinal("zone"));
                fishingEntry.RecommendedBait = reader.GetString(reader.GetOrdinal("recommended_bait"));
                fishingEntry.IsBigFish = reader.GetInt32(reader.GetOrdinal("is_big_fish")) == 1;

                var weatherOrdinal = reader.GetOrdinal("weather_requirement");
                fishingEntry.WeatherRequirement = reader.IsDBNull(weatherOrdinal)
                    ? null
                    : reader.GetString(weatherOrdinal);

                var timeOrdinal = reader.GetOrdinal("time_requirement");
                fishingEntry.TimeRequirement = reader.IsDBNull(timeOrdinal)
                    ? null
                    : reader.GetString(timeOrdinal);
            }

            return entry;
        }

        private async Task InsertTypeSpecific<T>(T entry, int collectionId, SqliteTransaction transaction) where T : CollectionEntry
        {
            if (entry is RecipeEntry recipeEntry)
            {
                var sql = @"
                    INSERT INTO recipes (collection_id, recipe_id, recipe_level, crafting_class,
                                        is_master_recipe, master_book_id, item_level)
                    VALUES (@collection_id, @recipe_id, @recipe_level, @crafting_class,
                            @is_master_recipe, @master_book_id, @item_level)";

                using var cmd = context.Connection!.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@collection_id", collectionId);
                cmd.Parameters.AddWithValue("@recipe_id", recipeEntry.RecipeId);
                cmd.Parameters.AddWithValue("@recipe_level", recipeEntry.RecipeLevel);
                cmd.Parameters.AddWithValue("@crafting_class", (int)recipeEntry.CraftingClass);
                cmd.Parameters.AddWithValue("@is_master_recipe", recipeEntry.IsMasterRecipe ? 1 : 0);
                cmd.Parameters.AddWithValue("@master_book_id", recipeEntry.MasterBookId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@item_level", recipeEntry.ItemLevel);
                await cmd.ExecuteNonQueryAsync();
            }
            else if (entry is GatheringNodeEntry gatheringEntry)
            {
                var sql = @"
                    INSERT INTO gathering_nodes (collection_id, node_id, gathering_class, zone,
                                                 folklore_book_id, node_level, is_legendary, is_ephemeral)
                    VALUES (@collection_id, @node_id, @gathering_class, @zone,
                            @folklore_book_id, @node_level, @is_legendary, @is_ephemeral)";

                using var cmd = context.Connection!.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@collection_id", collectionId);
                cmd.Parameters.AddWithValue("@node_id", gatheringEntry.NodeId);
                cmd.Parameters.AddWithValue("@gathering_class", (int)gatheringEntry.GatheringClass);
                cmd.Parameters.AddWithValue("@zone", gatheringEntry.Zone);
                cmd.Parameters.AddWithValue("@folklore_book_id", gatheringEntry.FolkloreBookId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@node_level", gatheringEntry.NodeLevel);
                cmd.Parameters.AddWithValue("@is_legendary", gatheringEntry.IsLegendary ? 1 : 0);
                cmd.Parameters.AddWithValue("@is_ephemeral", gatheringEntry.IsEphemeral ? 1 : 0);
                await cmd.ExecuteNonQueryAsync();
            }
            else if (entry is FishingHoleEntry fishingEntry)
            {
                var sql = @"
                    INSERT INTO fishing_holes (collection_id, fish_id, fishing_hole_id, zone,
                                               recommended_bait, is_big_fish, weather_requirement, time_requirement)
                    VALUES (@collection_id, @fish_id, @fishing_hole_id, @zone,
                            @recommended_bait, @is_big_fish, @weather_requirement, @time_requirement)";

                using var cmd = context.Connection!.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@collection_id", collectionId);
                cmd.Parameters.AddWithValue("@fish_id", fishingEntry.FishId);
                cmd.Parameters.AddWithValue("@fishing_hole_id", fishingEntry.FishingHoleId);
                cmd.Parameters.AddWithValue("@zone", fishingEntry.Zone);
                cmd.Parameters.AddWithValue("@recommended_bait", fishingEntry.RecommendedBait);
                cmd.Parameters.AddWithValue("@is_big_fish", fishingEntry.IsBigFish ? 1 : 0);
                cmd.Parameters.AddWithValue("@weather_requirement", fishingEntry.WeatherRequirement ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@time_requirement", fishingEntry.TimeRequirement ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task UpdateTypeSpecific<T>(T entry, SqliteTransaction transaction) where T : CollectionEntry
        {
            if (entry is RecipeEntry recipeEntry)
            {
                var sql = @"
                    UPDATE recipes
                    SET recipe_id = @recipe_id,
                        recipe_level = @recipe_level,
                        crafting_class = @crafting_class,
                        is_master_recipe = @is_master_recipe,
                        master_book_id = @master_book_id,
                        item_level = @item_level
                    WHERE collection_id = @collection_id";

                using var cmd = context.Connection!.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@collection_id", entry.Id);
                cmd.Parameters.AddWithValue("@recipe_id", recipeEntry.RecipeId);
                cmd.Parameters.AddWithValue("@recipe_level", recipeEntry.RecipeLevel);
                cmd.Parameters.AddWithValue("@crafting_class", (int)recipeEntry.CraftingClass);
                cmd.Parameters.AddWithValue("@is_master_recipe", recipeEntry.IsMasterRecipe ? 1 : 0);
                cmd.Parameters.AddWithValue("@master_book_id", recipeEntry.MasterBookId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@item_level", recipeEntry.ItemLevel);
                await cmd.ExecuteNonQueryAsync();
            }
            else if (entry is GatheringNodeEntry gatheringEntry)
            {
                var sql = @"
                    UPDATE gathering_nodes
                    SET node_id = @node_id,
                        gathering_class = @gathering_class,
                        zone = @zone,
                        folklore_book_id = @folklore_book_id,
                        node_level = @node_level,
                        is_legendary = @is_legendary,
                        is_ephemeral = @is_ephemeral
                    WHERE collection_id = @collection_id";

                using var cmd = context.Connection!.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@collection_id", entry.Id);
                cmd.Parameters.AddWithValue("@node_id", gatheringEntry.NodeId);
                cmd.Parameters.AddWithValue("@gathering_class", (int)gatheringEntry.GatheringClass);
                cmd.Parameters.AddWithValue("@zone", gatheringEntry.Zone);
                cmd.Parameters.AddWithValue("@folklore_book_id", gatheringEntry.FolkloreBookId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@node_level", gatheringEntry.NodeLevel);
                cmd.Parameters.AddWithValue("@is_legendary", gatheringEntry.IsLegendary ? 1 : 0);
                cmd.Parameters.AddWithValue("@is_ephemeral", gatheringEntry.IsEphemeral ? 1 : 0);
                await cmd.ExecuteNonQueryAsync();
            }
            else if (entry is FishingHoleEntry fishingEntry)
            {
                var sql = @"
                    UPDATE fishing_holes
                    SET fish_id = @fish_id,
                        fishing_hole_id = @fishing_hole_id,
                        zone = @zone,
                        recommended_bait = @recommended_bait,
                        is_big_fish = @is_big_fish,
                        weather_requirement = @weather_requirement,
                        time_requirement = @time_requirement
                    WHERE collection_id = @collection_id";

                using var cmd = context.Connection!.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@collection_id", entry.Id);
                cmd.Parameters.AddWithValue("@fish_id", fishingEntry.FishId);
                cmd.Parameters.AddWithValue("@fishing_hole_id", fishingEntry.FishingHoleId);
                cmd.Parameters.AddWithValue("@zone", fishingEntry.Zone);
                cmd.Parameters.AddWithValue("@recommended_bait", fishingEntry.RecommendedBait);
                cmd.Parameters.AddWithValue("@is_big_fish", fishingEntry.IsBigFish ? 1 : 0);
                cmd.Parameters.AddWithValue("@weather_requirement", fishingEntry.WeatherRequirement ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@time_requirement", fishingEntry.TimeRequirement ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task UpsertTypeSpecific<T>(T entry, int collectionId, SqliteTransaction transaction) where T : CollectionEntry
        {
            if (entry is RecipeEntry recipeEntry)
            {
                var sql = @"
                    INSERT OR REPLACE INTO recipes (collection_id, recipe_id, recipe_level, crafting_class,
                                                    is_master_recipe, master_book_id, item_level)
                    VALUES (@collection_id, @recipe_id, @recipe_level, @crafting_class,
                            @is_master_recipe, @master_book_id, @item_level)";

                using var cmd = context.Connection!.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@collection_id", collectionId);
                cmd.Parameters.AddWithValue("@recipe_id", recipeEntry.RecipeId);
                cmd.Parameters.AddWithValue("@recipe_level", recipeEntry.RecipeLevel);
                cmd.Parameters.AddWithValue("@crafting_class", (int)recipeEntry.CraftingClass);
                cmd.Parameters.AddWithValue("@is_master_recipe", recipeEntry.IsMasterRecipe ? 1 : 0);
                cmd.Parameters.AddWithValue("@master_book_id", recipeEntry.MasterBookId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@item_level", recipeEntry.ItemLevel);
                await cmd.ExecuteNonQueryAsync();
            }
            else if (entry is GatheringNodeEntry gatheringEntry)
            {
                var sql = @"
                    INSERT OR REPLACE INTO gathering_nodes (collection_id, node_id, gathering_class, zone,
                                                            folklore_book_id, node_level, is_legendary, is_ephemeral)
                    VALUES (@collection_id, @node_id, @gathering_class, @zone,
                            @folklore_book_id, @node_level, @is_legendary, @is_ephemeral)";

                using var cmd = context.Connection!.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@collection_id", collectionId);
                cmd.Parameters.AddWithValue("@node_id", gatheringEntry.NodeId);
                cmd.Parameters.AddWithValue("@gathering_class", (int)gatheringEntry.GatheringClass);
                cmd.Parameters.AddWithValue("@zone", gatheringEntry.Zone);
                cmd.Parameters.AddWithValue("@folklore_book_id", gatheringEntry.FolkloreBookId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@node_level", gatheringEntry.NodeLevel);
                cmd.Parameters.AddWithValue("@is_legendary", gatheringEntry.IsLegendary ? 1 : 0);
                cmd.Parameters.AddWithValue("@is_ephemeral", gatheringEntry.IsEphemeral ? 1 : 0);
                await cmd.ExecuteNonQueryAsync();
            }
            else if (entry is FishingHoleEntry fishingEntry)
            {
                var sql = @"
                    INSERT OR REPLACE INTO fishing_holes (collection_id, fish_id, fishing_hole_id, zone,
                                                          recommended_bait, is_big_fish, weather_requirement, time_requirement)
                    VALUES (@collection_id, @fish_id, @fishing_hole_id, @zone,
                            @recommended_bait, @is_big_fish, @weather_requirement, @time_requirement)";

                using var cmd = context.Connection!.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@collection_id", collectionId);
                cmd.Parameters.AddWithValue("@fish_id", fishingEntry.FishId);
                cmd.Parameters.AddWithValue("@fishing_hole_id", fishingEntry.FishingHoleId);
                cmd.Parameters.AddWithValue("@zone", fishingEntry.Zone);
                cmd.Parameters.AddWithValue("@recommended_bait", fishingEntry.RecommendedBait);
                cmd.Parameters.AddWithValue("@is_big_fish", fishingEntry.IsBigFish ? 1 : 0);
                cmd.Parameters.AddWithValue("@weather_requirement", fishingEntry.WeatherRequirement ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@time_requirement", fishingEntry.TimeRequirement ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
