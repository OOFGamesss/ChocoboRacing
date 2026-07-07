using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChocoboRacing.Config;
using ChocoboRacing.Models;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using LiteDB;

/// <summary>
/// Persists race history to a LiteDB database separate from the plugin config file.
/// </summary>
namespace ChocoboRacing.Services;

public sealed class RaceHistoryService : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<SessionRecord> _sessions;
    private readonly IPluginLog _log;
    private readonly object _lock = new();

    private Guid _activeSessionId = Guid.Empty;
    private DateTime _activeSessionLastRaceTime = DateTime.MinValue;
    private List<SessionRecord> _cachedSessions = new();

    public IReadOnlyList<SessionRecord> Sessions => _cachedSessions;

    public Guid ActiveSessionId { get { lock (_lock) return _activeSessionId; } }

    public RaceHistoryService(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _log = log;

        var mapper = new BsonMapper();
        mapper.Entity<SessionRecord>().Id(s => s.SessionId);

        var dbPath = Path.Combine(pluginInterface.ConfigDirectory.FullName, "race_history.db");
        _db = new LiteDatabase(new ConnectionString(dbPath) { Connection = ConnectionType.Shared }, mapper);

        _sessions = _db.GetCollection<SessionRecord>("sessions");
        _sessions.EnsureIndex(s => s.StartTime);

        LoadLastSession();
        RefreshCacheUnderLock();
    }

    public (Guid Id, bool IsNew) EnsureActiveSession(DateTime now)
    {
        lock (_lock)
        {
            if (_activeSessionId != Guid.Empty && (now - _activeSessionLastRaceTime).TotalHours < 1)
            {
                TouchLastRaceTimeUnderLock(_activeSessionId, now);
                return (_activeSessionId, false);
            }

            var session = new SessionRecord { SessionId = Guid.NewGuid(), StartTime = now, LastRaceTime = now };
            _sessions.Insert(session);

            _activeSessionId = session.SessionId;
            _activeSessionLastRaceTime = now;
            RefreshCacheUnderLock();
            return (session.SessionId, true);
        }
    }

    public void RecordRace(Guid sessionId, RaceRecord race)
    {
        lock (_lock)
        {
            var session = _sessions.FindById(new BsonValue(sessionId));
            if (session == null) return;

            session.Races.Add(race);
            session.LastRaceTime = DateTime.Now;
            _sessions.Update(session);

            if (sessionId == _activeSessionId)
                _activeSessionLastRaceTime = session.LastRaceTime;

            RefreshCacheUnderLock();
        }
    }

    public void AddTip(long amount)
    {
        if (amount <= 0) return;
        lock (_lock)
        {
            var (sessionId, _) = EnsureActiveSessionUnderLock(DateTime.Now);
            var session = _sessions.FindById(new BsonValue(sessionId));
            if (session == null) return;

            session.Tips += amount;
            _sessions.Update(session);
            RefreshCacheUnderLock();
        }
    }

    public void DeleteSession(Guid sessionId)
    {
        lock (_lock)
        {
            _sessions.Delete(new BsonValue(sessionId));

            if (_activeSessionId == sessionId)
            {
                _activeSessionId = Guid.Empty;
                _activeSessionLastRaceTime = DateTime.MinValue;
                LoadLastSession();
            }

            RefreshCacheUnderLock();
        }
    }

    public void DeleteRaceAt(Guid sessionId, int index)
    {
        lock (_lock)
        {
            var session = _sessions.FindById(new BsonValue(sessionId));
            if (session == null) return;
            if (index < 0 || index >= session.Races.Count) return;

            session.Races.RemoveAt(index);
            _sessions.Update(session);
            RefreshCacheUnderLock();
        }
    }

    public void MigrateFromConfig(PluginConfig config)
    {
        if (config.HasMigratedHistoryToSqlite) return;

        lock (_lock)
        {
            try
            {
                var count = config.Sessions.Count;
                if (count > 0)
                    _sessions.InsertBulk(config.Sessions);

                config.Sessions.Clear();
                config.HasMigratedHistoryToSqlite = true;
                config.Save();

                LoadLastSession();
                RefreshCacheUnderLock();

                _log.Information($"[RaceHistoryService] Migrated {count} session(s) to LiteDB.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[RaceHistoryService] History migration failed.");
            }
        }
    }

    private void LoadLastSession()
    {
        var last = _sessions.FindOne(Query.All(nameof(SessionRecord.StartTime), Query.Descending));
        if (last == null) return;
        _activeSessionId = last.SessionId;
        _activeSessionLastRaceTime = last.LastRaceTime;
    }

    private void TouchLastRaceTimeUnderLock(Guid sessionId, DateTime time)
    {
        _sessions.UpdateMany(
            s => new SessionRecord
            {
                SessionId    = s.SessionId,
                StartTime    = s.StartTime,
                LastRaceTime = time,
                Tips         = s.Tips,
                Races        = s.Races
            },
            s => s.SessionId == sessionId);

        if (_activeSessionId == sessionId)
            _activeSessionLastRaceTime = time;
    }

    private (Guid Id, bool IsNew) EnsureActiveSessionUnderLock(DateTime now)
    {
        if (_activeSessionId != Guid.Empty && (now - _activeSessionLastRaceTime).TotalHours < 1)
        {
            TouchLastRaceTimeUnderLock(_activeSessionId, now);
            return (_activeSessionId, false);
        }

        var session = new SessionRecord { SessionId = Guid.NewGuid(), StartTime = now, LastRaceTime = now };
        _sessions.Insert(session);
        _activeSessionId = session.SessionId;
        _activeSessionLastRaceTime = now;
        return (session.SessionId, true);
    }

    private void RefreshCacheUnderLock()
    {
        try
        {
            _cachedSessions = _sessions
                .FindAll()
                .OrderByDescending(s => s.StartTime)
                .ToList();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[RaceHistoryService] Failed to refresh session cache.");
        }
    }

    public void Dispose() => _db.Dispose();
}
