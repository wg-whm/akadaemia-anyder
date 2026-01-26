using Microsoft.Data.Sqlite;
using System;

namespace AkadaemiaAnyder.Data.Migrations
{
    /// <summary>
    /// Initial database schema migration.
    /// Creates all core tables for collection tracking.
    /// </summary>
    public static class Migration_v1
    {
        public const int Version = 1;

        public static void Apply(SqliteConnection connection)
        {
            using var transaction = connection.BeginTransaction();

            try
            {
                // Create schema version tracking table
                CreateSchemaVersionTable(connection);

                // Create base collections table
                CreateCollectionsTable(connection);

                // Create type-specific tables
                CreateRecipesTable(connection);
                CreateGatheringNodesTable(connection);
                CreateFishingHolesTable(connection);

                // Record migration version
                RecordMigrationVersion(connection);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static void CreateSchemaVersionTable(SqliteConnection connection)
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS schema_version (
                    version INTEGER PRIMARY KEY,
                    applied_at TEXT NOT NULL DEFAULT (datetime('now')),
                    description TEXT NOT NULL
                )";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static void CreateCollectionsTable(SqliteConnection connection)
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS collections (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    character_id INTEGER NOT NULL,
                    character_name TEXT NOT NULL,
                    world_name TEXT NOT NULL,
                    type INTEGER NOT NULL,
                    item_id INTEGER NOT NULL,
                    item_name TEXT NOT NULL,
                    is_unlocked INTEGER NOT NULL DEFAULT 0,
                    unlocked_at TEXT,
                    first_seen_at TEXT NOT NULL,
                    last_updated_at TEXT NOT NULL,
                    UNIQUE(character_id, type, item_id)
                )";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();

            // Create indices for common queries
            CreateIndex(connection, "idx_collections_character", "collections", "character_id");
            CreateIndex(connection, "idx_collections_type", "collections", "type");
            CreateIndex(connection, "idx_collections_unlocked", "collections", "is_unlocked");
        }

        private static void CreateRecipesTable(SqliteConnection connection)
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS recipes (
                    collection_id INTEGER PRIMARY KEY,
                    recipe_id INTEGER NOT NULL,
                    recipe_level INTEGER NOT NULL,
                    crafting_class INTEGER NOT NULL,
                    is_master_recipe INTEGER NOT NULL DEFAULT 0,
                    master_book_id INTEGER,
                    item_level INTEGER NOT NULL,
                    FOREIGN KEY(collection_id) REFERENCES collections(id) ON DELETE CASCADE
                )";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();

            CreateIndex(connection, "idx_recipes_class", "recipes", "crafting_class");
            CreateIndex(connection, "idx_recipes_level", "recipes", "recipe_level");
        }

        private static void CreateGatheringNodesTable(SqliteConnection connection)
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS gathering_nodes (
                    collection_id INTEGER PRIMARY KEY,
                    node_id INTEGER NOT NULL,
                    gathering_class INTEGER NOT NULL,
                    zone TEXT NOT NULL,
                    folklore_book_id INTEGER,
                    node_level INTEGER NOT NULL,
                    is_legendary INTEGER NOT NULL DEFAULT 0,
                    is_ephemeral INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(collection_id) REFERENCES collections(id) ON DELETE CASCADE
                )";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();

            CreateIndex(connection, "idx_gathering_class", "gathering_nodes", "gathering_class");
            CreateIndex(connection, "idx_gathering_zone", "gathering_nodes", "zone");
        }

        private static void CreateFishingHolesTable(SqliteConnection connection)
        {
            var sql = @"
                CREATE TABLE IF NOT EXISTS fishing_holes (
                    collection_id INTEGER PRIMARY KEY,
                    fish_id INTEGER NOT NULL,
                    fishing_hole_id INTEGER NOT NULL,
                    zone TEXT NOT NULL,
                    recommended_bait TEXT NOT NULL,
                    is_big_fish INTEGER NOT NULL DEFAULT 0,
                    weather_requirement TEXT,
                    time_requirement TEXT,
                    FOREIGN KEY(collection_id) REFERENCES collections(id) ON DELETE CASCADE
                )";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();

            CreateIndex(connection, "idx_fishing_zone", "fishing_holes", "zone");
        }

        private static void CreateIndex(SqliteConnection connection, string indexName, string tableName, string columnName)
        {
            var sql = $"CREATE INDEX IF NOT EXISTS {indexName} ON {tableName}({columnName})";
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static void RecordMigrationVersion(SqliteConnection connection)
        {
            var sql = @"
                INSERT INTO schema_version (version, description)
                VALUES (@version, @description)";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@version", Version);
            command.Parameters.AddWithValue("@description", "Initial schema - collections, recipes, gathering nodes, fishing holes");
            command.ExecuteNonQuery();
        }
    }
}
