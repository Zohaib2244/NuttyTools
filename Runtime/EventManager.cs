using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Advanced Event-driven architecture manager with priority handling, conditional events, and performance optimizations
/// Allows decoupled communication between different systems with enhanced features
/// </summary>
public static class EventManager
{
    // Dictionary to store event handlers with priority support
    private static readonly Dictionary<Type, SortedDictionary<int, List<Delegate>>> _eventHandlers = 
        new Dictionary<Type, SortedDictionary<int, List<Delegate>>>();
    
    // Queue for delayed events
    private static readonly Queue<DelayedEvent> _delayedEvents = new Queue<DelayedEvent>();
    
    // if(logsEnabled)Event history for 
    // debugging
    private static readonly List<EventHistoryEntry> _eventHistory = new List<EventHistoryEntry>();
    private const int MAX_HISTORY_SIZE = 100;
    
    // Performance tracking
    private static readonly Dictionary<Type, EventStats> _eventStats = new Dictionary<Type, EventStats>();
    
    // Event filtering
    private static readonly HashSet<Type> _pausedEvents = new HashSet<Type>();
    private static bool _globalEventsPaused = false;
    public static bool logsEnabled = false;

    #region Core Subscription Methods

    /// <summary>
    /// Subscribe to an event with priority (lower numbers = higher priority)
    /// </summary>
    /// <example>
    /// // High priority handler (executes first)
    /// EventManager.Subscribe&lt;PlayerDeathEvent&gt;(OnPlayerDeath, priority: 0);
    /// 
    /// // Normal priority handler
    /// EventManager.Subscribe&lt;PlayerDeathEvent&gt;(OnPlayerDeathUI, priority: 10);
    /// 
    /// // Low priority handler (executes last)
    /// EventManager.Subscribe&lt;PlayerDeathEvent&gt;(OnPlayerDeathCleanup, priority: 100);
    /// </example>
    public static void Subscribe<T>(Action<T> handler, int priority = 10) where T : IGameEvent
    {
        Type eventType = typeof(T);
        AddHandler(eventType, handler, priority);
        LogSubscription(eventType, priority);
    }

    /// <summary>
    /// Subscribe to an event without parameters with priority
    /// </summary>
    /// <example>
    /// // Subscribe to game start event
    /// EventManager.Subscribe&lt;GameStartedEvent&gt;(() => 
    /// {
    ///    if(logsEnabled)    
    /// Debug.Log("Game Started!");
    ///     AudioManager.PlayMusic("background");
    /// }, priority: 5);
    /// </example>
    public static void Subscribe<T>(Action handler, int priority = 10) where T : IGameEvent
    {
        Type eventType = typeof(T);
        AddHandler(eventType, handler, priority);
        LogSubscription(eventType, priority);
    }

    /// <summary>
    /// Subscribe with a condition - handler only executes if condition is true
    /// </summary>
    /// <example>
    /// // Only handle damage if player is alive
    /// EventManager.SubscribeConditional&lt;PlayerDamageEvent&gt;(
    ///     OnPlayerDamage, 
    ///     () => PlayerController.Instance.IsAlive
    /// );
    /// 
    /// // Only handle UI updates if UI is active
    /// EventManager.SubscribeConditional&lt;ScoreUpdateEvent&gt;(
    ///     UpdateScoreUI, 
    ///     () => UIManager.Instance.IsUIActive
    /// );
    /// </example>
    public static void SubscribeConditional<T>(Action<T> handler, Func<bool> condition, int priority = 10) where T : IGameEvent
    {
        Subscribe<T>(eventData => 
        {
            if (condition())
                handler(eventData);
        }, priority);
    }

    /// <summary>
    /// Subscribe once - automatically unsubscribes after first execution
    /// </summary>
    /// <example>
    /// // Show tutorial only on first level completion
    /// EventManager.SubscribeOnce&lt;LevelCompletedEvent&gt;(ShowTutorialComplete);
    /// 
    /// // Initialize game systems only once
    /// EventManager.SubscribeOnce&lt;GameInitializedEvent&gt;(() => 
    /// {
    ///     LoadPlayerData();
    ///     InitializeAudio();
    /// });
    /// </example>
    public static void SubscribeOnce<T>(Action<T> handler, int priority = 10) where T : IGameEvent
    {
        Action<T> onceHandler = null;
        onceHandler = eventData =>
        {
            handler(eventData);
            Unsubscribe(onceHandler);
        };
        Subscribe(onceHandler, priority);
    }

    #endregion

    #region Unsubscription Methods

    /// <summary>
    /// Unsubscribe from an event
    /// </summary>
    /// <example>
    /// // Store reference to be able to unsubscribe later
    /// Action&lt;PlayerDeathEvent&gt; deathHandler = OnPlayerDeath;
    /// EventManager.Subscribe(deathHandler);
    /// 
    /// // Later unsubscribe
    /// EventManager.Unsubscribe(deathHandler);
    /// </example>
    public static void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
    {
        Type eventType = typeof(T);
        RemoveHandler(eventType, handler);
    }

    /// <summary>
    /// Unsubscribe from parameterless event
    /// </summary>
    /// <example>
    /// Action gameStartHandler = OnGameStart;
    /// EventManager.Subscribe&lt;GameStartedEvent&gt;(gameStartHandler);
    /// EventManager.Unsubscribe&lt;GameStartedEvent&gt;(gameStartHandler);
    /// </example>
    public static void Unsubscribe<T>(Action handler) where T : IGameEvent
    {
        Type eventType = typeof(T);
        RemoveHandler(eventType, handler);
    }

    /// <summary>
    /// Unsubscribe all handlers for a specific event type
    /// </summary>
    /// <example>
    /// // Clear all damage event handlers (useful for scene transitions)
    /// EventManager.UnsubscribeAll&lt;PlayerDamageEvent&gt;();
    /// </example>
    public static void UnsubscribeAll<T>() where T : IGameEvent
    {
        Type eventType = typeof(T);
        if (_eventHandlers.ContainsKey(eventType))
        {
            _eventHandlers.Remove(eventType);
            if(logsEnabled)
            Debug.Log($"EventManager: Unsubscribed all handlers for {eventType.Name}");
        }
    }

    #endregion

    #region Event Raising Methods

    /// <summary>
    /// Raise an event immediately
    /// </summary>
    /// <example>
    /// // Raise player death event
    /// EventManager.RaiseEvent(new PlayerDeathEvent 
    /// { 
    ///     Cause = DeathCause.Enemy, 
    ///     Position = player.transform.position 
    /// });
    /// 
    /// // Raise score update
    /// EventManager.RaiseEvent(new ScoreUpdateEvent { NewScore = 1500, Bonus = 200 });
    /// </example>
    public static void RaiseEvent<T>(T eventData) where T : IGameEvent
    {
        if (_globalEventsPaused || _pausedEvents.Contains(typeof(T)))
        {
            if(logsEnabled)
            Debug.Log($"EventManager: Event {typeof(T).Name} is paused, skipping execution");
            return;
        }

        Type eventType = typeof(T);
        var startTime = Time.realtimeSinceStartup;

        if (_eventHandlers.ContainsKey(eventType))
        {
            var priorityGroups = _eventHandlers[eventType];
            int totalHandlers = priorityGroups.Values.Sum(list => list.Count);
            
            if(logsEnabled)
            Debug.Log($"EventManager: Raising {eventType.Name} to {totalHandlers} handlers");

            foreach (var priorityGroup in priorityGroups)
            {
                var handlers = new List<Delegate>(priorityGroup.Value);
                
                foreach (Delegate handler in handlers)
                {
                    try
                    {
                        if (handler is Action<T> actionWithParam)
                            actionWithParam.Invoke(eventData);
                        else if (handler is Action actionWithoutParam)
                            actionWithoutParam.Invoke();
                    }
                    catch (Exception e)
                    {
                        if(logsEnabled)
                        Debug.LogError($"EventManager: Error in {eventType.Name} handler: {e.Message}");
                    }
                }
            }
        }

        // Track performance and history
        TrackEventPerformance(eventType, startTime);
        AddToHistory(eventType, eventData);
    }

    /// <summary>
    /// Raise an event without data
    /// </summary>
    /// <example>
    /// // Simple events without data
    /// EventManager.RaiseEvent&lt;GameStartedEvent&gt;();
    /// EventManager.RaiseEvent&lt;GamePausedEvent&gt;();
    /// EventManager.RaiseEvent&lt;LevelLoadedEvent&gt;();
    /// </example>
    public static void RaiseEvent<T>() where T : IGameEvent, new()
    {
        RaiseEvent(new T());
    }

    /// <summary>
    /// Raise an event after a delay
    /// </summary>
    /// <example>
    /// // Show victory screen after 2 seconds
    /// EventManager.RaiseEventDelayed&lt;ShowVictoryScreenEvent&gt;(2f);
    /// 
    /// // Respawn player after 3 seconds
    /// EventManager.RaiseEventDelayed(new PlayerRespawnEvent { Position = spawnPoint }, 3f);
    /// </example>
    public static void RaiseEventDelayed<T>(T eventData, float delay) where T : IGameEvent
    {
        _delayedEvents.Enqueue(new DelayedEvent
        {
            EventData = eventData,
            ExecuteTime = Time.time + delay
        });
    }

    public static void RaiseEventDelayed<T>(float delay) where T : IGameEvent, new()
    {
        RaiseEventDelayed(new T(), delay);
    }

    /// <summary>
    /// Process delayed events (call this in Update or similar)
    /// </summary>
    /// <example>
    /// // In a MonoBehaviour Update method:
    /// void Update()
    /// {
    ///     EventManager.ProcessDelayedEvents();
    /// }
    /// </example>
    public static void ProcessDelayedEvents()
    {
        while (_delayedEvents.Count > 0 && _delayedEvents.Peek().ExecuteTime <= Time.time)
        {
            var delayedEvent = _delayedEvents.Dequeue();
            var eventType = delayedEvent.EventData.GetType();
            var method = typeof(EventManager).GetMethod(nameof(RaiseEvent), new[] { eventType });
            method?.Invoke(null, new[] { delayedEvent.EventData });
        }
    }

    #endregion

    #region Event Control Methods

    /// <summary>
    /// Pause/Resume specific event types
    /// </summary>
    /// <example>
    /// // Pause damage events during cutscenes
    /// EventManager.PauseEvent&lt;PlayerDamageEvent&gt;();
    /// 
    /// // Resume after cutscene
    /// EventManager.ResumeEvent&lt;PlayerDamageEvent&gt;();
    /// </example>
    public static void PauseEvent<T>() where T : IGameEvent
    {
        _pausedEvents.Add(typeof(T));
        if(logsEnabled)
        Debug.Log($"EventManager: Paused {typeof(T).Name} events");
    }

    public static void ResumeEvent<T>() where T : IGameEvent
    {
        _pausedEvents.Remove(typeof(T));
        if(logsEnabled)
        Debug.Log($"EventManager: Resumed {typeof(T).Name} events");
    }

    /// <summary>
    /// Pause/Resume all events globally
    /// </summary>
    /// <example>
    /// // Pause all events during loading screen
    /// EventManager.PauseAllEvents();
    /// 
    /// // Resume when loading complete
    /// EventManager.ResumeAllEvents();
    /// </example>
    public static void PauseAllEvents()
    {
        _globalEventsPaused = true;
        if(logsEnabled)
        Debug.Log("EventManager: All events paused globally");
    }

    public static void ResumeAllEvents()
    {
        _globalEventsPaused = false;
        if(logsEnabled)
        Debug.Log("EventManager: All events resumed globally");
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Get detailed statistics about event usage
    /// </summary>
    /// <example>
    ///if(logsEnabled) // Log performance stats for 
    // debugging
    /// var stats = EventManager.GetEventStats&lt;PlayerDamageEvent&gt;();
    ///    if(logsEnabled)
    /// Debug.Log($"Damage events called {stats.CallCount} times, avg time: {stats.AverageExecutionTime}ms");
    /// </example>
    public static EventStats GetEventStats<T>() where T : IGameEvent
    {
        Type eventType = typeof(T);
        return _eventStats.ContainsKey(eventType) ? _eventStats[eventType] : new EventStats();
    }

    /// <summary>
    ///    if(logsEnabled)Get event execution history for 
    /// debugging
    /// </summary>
    /// <example>
    ///if(logsEnabled) // 
    // Debug recent events
    /// var history = EventManager.GetEventHistory();
    /// foreach(var entry in history.TakeLast(10))
    /// {
    ///    if(logsEnabled)    
    /// Debug.Log($"{entry.Timestamp}: {entry.EventType.Name}");
    /// }
    /// </example>
    public static List<EventHistoryEntry> GetEventHistory()
    {
        return new List<EventHistoryEntry>(_eventHistory);
    }

    /// <summary>
    /// Check if an event type has any subscribers
    /// </summary>
    /// <example>
    /// // Only create expensive event data if someone is listening
    /// if (EventManager.HasSubscribers&lt;DetailedAnalyticsEvent&gt;())
    /// {
    ///     var analyticsData = GenerateExpensiveAnalyticsData();
    ///     EventManager.RaiseEvent(new DetailedAnalyticsEvent { Data = analyticsData });
    /// }
    /// </example>
    public static bool HasSubscribers<T>() where T : IGameEvent
    {
        Type eventType = typeof(T);
        return _eventHandlers.ContainsKey(eventType) && 
               _eventHandlers[eventType].Values.Any(list => list.Count > 0);
    }

    /// <summary>
    /// Get total number of handlers for an event type
    /// </summary>
    /// <example>
    /// int damageHandlers = EventManager.GetHandlerCount&lt;PlayerDamageEvent&gt;();
    ///    if(logsEnabled)
    /// Debug.Log($"Number of damage handlers: {damageHandlers}");
    /// </example>
    public static int GetHandlerCount<T>() where T : IGameEvent
    {
        Type eventType = typeof(T);
        return _eventHandlers.ContainsKey(eventType) 
            ? _eventHandlers[eventType].Values.Sum(list => list.Count) 
            : 0;
    }

    /// <summary>
    /// Clear all event handlers and reset system
    /// </summary>
    /// <example>
    /// // Clean up when changing scenes
    /// void OnSceneUnload()
    /// {
    ///     EventManager.ClearAllEvents();
    /// }
    /// </example>
    public static void ClearAllEvents()
    {
        _eventHandlers.Clear();
        _delayedEvents.Clear();
        _pausedEvents.Clear();
        _eventHistory.Clear();
        _eventStats.Clear();
        _globalEventsPaused = false;
        if(logsEnabled)
        Debug.Log("EventManager: Complete system reset");
    }

    #endregion

    #region Private Helper Methods

    private static void AddHandler(Type eventType, Delegate handler, int priority)
    {
        if (!_eventHandlers.ContainsKey(eventType))
        {
            _eventHandlers[eventType] = new SortedDictionary<int, List<Delegate>>();
        }

        if (!_eventHandlers[eventType].ContainsKey(priority))
        {
            _eventHandlers[eventType][priority] = new List<Delegate>();
        }

        _eventHandlers[eventType][priority].Add(handler);
    }

    private static void RemoveHandler(Type eventType, Delegate handler)
    {
        if (!_eventHandlers.ContainsKey(eventType)) return;

        var priorityGroups = _eventHandlers[eventType];
        foreach (var priorityGroup in priorityGroups.Values)
        {
            priorityGroup.Remove(handler);
        }

        // Clean up empty priority groups
        var emptyGroups = priorityGroups.Where(kvp => kvp.Value.Count == 0).ToList();
        foreach (var emptyGroup in emptyGroups)
        {
            priorityGroups.Remove(emptyGroup.Key);
        }

        // Clean up empty event types
        if (priorityGroups.Count == 0)
        {
            _eventHandlers.Remove(eventType);
        }
    }

    private static void LogSubscription(Type eventType, int priority)
    {
        int totalHandlers = GetHandlerCount(eventType);
        if(logsEnabled)
        Debug.Log($"EventManager: Subscribed to {eventType.Name} (Priority: {priority}). Total handlers: {totalHandlers}");
    }

    private static int GetHandlerCount(Type eventType)
    {
        return _eventHandlers.ContainsKey(eventType) 
            ? _eventHandlers[eventType].Values.Sum(list => list.Count) 
            : 0;
    }

    private static void TrackEventPerformance(Type eventType, float startTime)
    {
        float executionTime = (Time.realtimeSinceStartup - startTime) * 1000f; // Convert to milliseconds

        if (!_eventStats.ContainsKey(eventType))
        {
            _eventStats[eventType] = new EventStats();
        }

        var stats = _eventStats[eventType];
        stats.CallCount++;
        stats.TotalExecutionTime += executionTime;
        stats.AverageExecutionTime = stats.TotalExecutionTime / stats.CallCount;
        stats.LastExecutionTime = executionTime;

        if (executionTime > 5f) // Warn about slow events
        {
            if(logsEnabled)
            Debug.LogWarning($"EventManager: Slow event {eventType.Name} took {executionTime:F2}ms");
        }
    }

    private static void AddToHistory(Type eventType, IGameEvent eventData)
    {
        _eventHistory.Add(new EventHistoryEntry
        {
            EventType = eventType,
            Timestamp = Time.time,
            EventData = eventData
        });

        // Keep history size manageable
        while (_eventHistory.Count > MAX_HISTORY_SIZE)
        {
            _eventHistory.RemoveAt(0);
        }
    }

    #endregion
}

#region Supporting Classes and Interfaces

/// <summary>
/// Base interface for all game events
/// </summary>
public interface IGameEvent { }

/// <summary>
/// Statistics tracking for events
/// </summary>
public class EventStats
{
    public int CallCount { get; set; }
    public float TotalExecutionTime { get; set; }
    public float AverageExecutionTime { get; set; }
    public float LastExecutionTime { get; set; }
}

/// <summary>
///    if(logsEnabled)Event history entry for 
/// debugging
/// </summary>
public class EventHistoryEntry
{
    public Type EventType { get; set; }
    public float Timestamp { get; set; }
    public IGameEvent EventData { get; set; }
}

/// <summary>
/// Delayed event container
/// </summary>
public class DelayedEvent
{
    public IGameEvent EventData { get; set; }
    public float ExecuteTime { get; set; }
}

#endregion

#region Example Event Classes

/// <summary>
/// Example event classes for your game
/// </summary>
public class CameraFOVEvent : IGameEvent
{
    public float TargetFOV { get; set; }
    public float EnterDuration { get; set; } = 1f;
    public float ExitDuration { get; set; } = 1f;
    public AnimationCurve EaseCurve { get; set; }
    public bool RestoreAfterDelay { get; set; } = true;

    public CameraFOVEvent(float targetFOV, float enterDuration = 1f, float exitDuration = 1f)
    {
        TargetFOV = targetFOV;
        EnterDuration = enterDuration;
        ExitDuration = exitDuration;
    }
}

#endregion