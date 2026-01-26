using System.Collections.Generic;

namespace SamplePlugin.EventListeners;

/// <summary>
/// Interface for event-based collection tracking.
/// Subscribes to game events and accumulates unlocked items.
/// </summary>
/// <typeparam name="T">The type of collection item being tracked</typeparam>
public interface ICollectionListener<T>
{
    /// <summary>
    /// Start listening to game events.
    /// </summary>
    void Start();

    /// <summary>
    /// Stop listening to game events.
    /// </summary>
    void Stop();

    /// <summary>
    /// Get all items collected via events since Start() or last Clear().
    /// </summary>
    /// <returns>List of collected items</returns>
    List<T> GetCollectedItems();

    /// <summary>
    /// Clear the collected items list.
    /// </summary>
    void ClearCollectedItems();

    /// <summary>
    /// Check if the listener is currently active.
    /// </summary>
    /// <returns>True if listening to events</returns>
    bool IsActive { get; }
}
