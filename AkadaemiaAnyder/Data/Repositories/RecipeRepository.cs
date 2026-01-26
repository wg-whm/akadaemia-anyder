using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Data.Models;

namespace AkadaemiaAnyder.Data.Repositories
{
    /// <summary>
    /// Specialized repository for recipe collection entries.
    /// Provides filtering by crafting class and master recipe status.
    /// </summary>
    public class RecipeRepository : CollectionRepository
    {
        private readonly DatabaseContext context;
        private readonly IPluginLog log;

        public RecipeRepository(DatabaseContext databaseContext, IPluginLog pluginLog)
            : base(databaseContext, pluginLog)
        {
            context = databaseContext;
            log = pluginLog;
        }

        /// <summary>
        /// Gets all recipes for a specific crafting class.
        /// </summary>
        public async Task<List<RecipeEntry>> GetByCraftingClassAsync(CraftingClass craftingClass)
        {
            log.Debug($"GetByCraftingClassAsync(class={craftingClass})");
            var results = new List<RecipeEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, r.recipe_id, r.recipe_level, r.crafting_class, r.is_master_recipe,
                       r.master_book_id, r.item_level
                FROM collections c
                INNER JOIN recipes r ON c.id = r.collection_id
                WHERE c.type = 1 AND r.crafting_class = @crafting_class
                ORDER BY r.recipe_level ASC, c.item_name ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@crafting_class", (int)craftingClass);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapRecipeFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetByCraftingClassAsync(class={craftingClass}) returned {results.Count} recipes");
            return results;
        }

        /// <summary>
        /// Gets all master recipes (requires master recipe books).
        /// </summary>
        public async Task<List<RecipeEntry>> GetMasterRecipesAsync()
        {
            log.Debug("GetMasterRecipesAsync()");
            var results = new List<RecipeEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, r.recipe_id, r.recipe_level, r.crafting_class, r.is_master_recipe,
                       r.master_book_id, r.item_level
                FROM collections c
                INNER JOIN recipes r ON c.id = r.collection_id
                WHERE c.type = 1 AND r.is_master_recipe = 1
                ORDER BY r.crafting_class ASC, r.recipe_level ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapRecipeFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetMasterRecipesAsync() returned {results.Count} recipes");
            return results;
        }

        /// <summary>
        /// Gets unlocked recipes for a specific crafting class.
        /// </summary>
        public async Task<List<RecipeEntry>> GetUnlockedByClassAsync(CraftingClass craftingClass)
        {
            log.Debug($"GetUnlockedByClassAsync(class={craftingClass})");
            var results = new List<RecipeEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, r.recipe_id, r.recipe_level, r.crafting_class, r.is_master_recipe,
                       r.master_book_id, r.item_level
                FROM collections c
                INNER JOIN recipes r ON c.id = r.collection_id
                WHERE c.type = 1 AND r.crafting_class = @crafting_class AND c.is_unlocked = 1
                ORDER BY r.recipe_level ASC, c.item_name ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@crafting_class", (int)craftingClass);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapRecipeFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetUnlockedByClassAsync(class={craftingClass}) returned {results.Count} recipes");
            return results;
        }

        private RecipeEntry? MapRecipeFromReader(Microsoft.Data.Sqlite.SqliteDataReader reader)
        {
            var entry = new RecipeEntry
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                CharacterId = reader.GetInt32(reader.GetOrdinal("character_id")),
                CharacterName = reader.GetString(reader.GetOrdinal("character_name")),
                WorldName = reader.GetString(reader.GetOrdinal("world_name")),
                Type = (CollectionType)reader.GetInt32(reader.GetOrdinal("type")),
                ItemId = reader.GetInt32(reader.GetOrdinal("item_id")),
                ItemName = reader.GetString(reader.GetOrdinal("item_name")),
                IsUnlocked = reader.GetInt32(reader.GetOrdinal("is_unlocked")) == 1,
                FirstSeenAt = System.DateTime.Parse(reader.GetString(reader.GetOrdinal("first_seen_at"))),
                LastUpdatedAt = System.DateTime.Parse(reader.GetString(reader.GetOrdinal("last_updated_at"))),
                RecipeId = reader.GetInt32(reader.GetOrdinal("recipe_id")),
                RecipeLevel = reader.GetInt32(reader.GetOrdinal("recipe_level")),
                CraftingClass = (CraftingClass)reader.GetInt32(reader.GetOrdinal("crafting_class")),
                IsMasterRecipe = reader.GetInt32(reader.GetOrdinal("is_master_recipe")) == 1,
                ItemLevel = reader.GetInt32(reader.GetOrdinal("item_level"))
            };

            var unlockedAtOrdinal = reader.GetOrdinal("unlocked_at");
            entry.UnlockedAt = reader.IsDBNull(unlockedAtOrdinal)
                ? null
                : System.DateTime.Parse(reader.GetString(unlockedAtOrdinal));

            var masterBookOrdinal = reader.GetOrdinal("master_book_id");
            entry.MasterBookId = reader.IsDBNull(masterBookOrdinal)
                ? null
                : reader.GetInt32(masterBookOrdinal);

            return entry;
        }
    }
}
