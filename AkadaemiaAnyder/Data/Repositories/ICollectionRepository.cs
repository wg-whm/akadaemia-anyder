using System.Collections.Generic;
using System.Threading.Tasks;
using AkadaemiaAnyder.Data.Models;

namespace AkadaemiaAnyder.Data.Repositories
{
    /// <summary>
    /// Generic repository interface for collection entries.
    /// Provides CRUD operations with SQLite optimizations.
    /// </summary>
    public interface ICollectionRepository
    {
        /// <summary>
        /// Gets all collection entries of the specified type.
        /// </summary>
        Task<List<T>> GetAllAsync<T>() where T : CollectionEntry;

        /// <summary>
        /// Gets a single collection entry by ID.
        /// </summary>
        Task<T?> GetByIdAsync<T>(int id) where T : CollectionEntry;

        /// <summary>
        /// Inserts a new collection entry.
        /// Returns the inserted entry's ID.
        /// </summary>
        Task<int> InsertAsync<T>(T entry) where T : CollectionEntry;

        /// <summary>
        /// Updates an existing collection entry.
        /// Returns the number of rows affected.
        /// </summary>
        Task<int> UpdateAsync<T>(T entry) where T : CollectionEntry;

        /// <summary>
        /// Deletes a collection entry by ID.
        /// Returns the number of rows affected.
        /// </summary>
        Task<int> DeleteAsync<T>(int id) where T : CollectionEntry;

        /// <summary>
        /// Bulk upsert (INSERT OR REPLACE) multiple entries in a single transaction.
        /// Returns the number of rows affected.
        /// </summary>
        Task<int> BulkUpsertAsync<T>(List<T> entries) where T : CollectionEntry;
    }
}
