using System;
using System.Collections.Generic;
using System.Linq;

namespace RtmpCore
{
    public class RtmpContext
    {
        public class RtmpEventArgs : EventArgs
        {
            public RtmpEventArgs(Guid id, string path)
            {
                SessionId = id;
                StreamPath = path;
            }

            public Guid SessionId { get; }

            public string StreamPath { get; }
        }

        public IDictionary<string, Guid> Publishsers { get; } = new Dictionary<string, Guid>();

        public IDictionary<string, IList<Guid>> Players { get; } = new Dictionary<string, IList<Guid>>();

        public IDictionary<string, IList<Guid>> IdlePlayers { get; } = new Dictionary<string, IList<Guid>>();

        public IDictionary<Guid, RtmpSession> Sessions { get; } = new Dictionary<Guid, RtmpSession>();

        public event EventHandler<RtmpEventArgs> StreamPublished;

        public event EventHandler<RtmpEventArgs> StreamUnpublished;

        public event EventHandler<RtmpEventArgs> PlayerConnected;

        public event EventHandler<RtmpEventArgs> PlayerDisconnected;

        public void AddPlayer(string playPath, Guid sessionId)
        {
            if (!Players.TryGetValue(playPath, out var players))
            {
                players = new List<Guid>();
                Players.Add(playPath, players);
            }
            players.Add(sessionId);
            PlayerConnected?.Invoke(this, new RtmpEventArgs(sessionId, playPath));
        }

        public void RemovePlayer(string playPath, Guid sessionId)
        {
            if (Players.TryGetValue(playPath, out var players))
            {
                players.Remove(sessionId);
                PlayerDisconnected?.Invoke(this, new RtmpEventArgs(sessionId, playPath));
            }
        }

        public void AddIdlePlayer(string playPath, Guid sessionId)
        {
            if (!IdlePlayers.TryGetValue(playPath, out var players))
            {
                players = new List<Guid>();
                Players.Add(playPath, players);
            }
            players.Add(sessionId);
        }

        public bool TryAddPublishser(string publishPath, Guid sessionId)
        {
            if (Publishsers.TryGetValue(publishPath, out var id))
                return false;
            Publishsers.Add(publishPath, sessionId);
            StreamPublished?.Invoke(this, new RtmpEventArgs(sessionId, publishPath));
            return true;
        }

        public void RemovePublisher(string publishPath)
        {
            if (Publishsers.Remove(publishPath, out var id))
                StreamUnpublished?.Invoke(this, new RtmpEventArgs(id, publishPath));
        }

        public bool TryGetPublishser(string path, out RtmpSession publisher)
        {
            publisher = null;
            if (Publishsers.TryGetValue(path, out var sessionId) && Sessions.TryGetValue(sessionId, out publisher))
                return true;
            return false;
        }

        public IList<RtmpSession> GetPlayers(string path)
        {
            if (Players.TryGetValue(path, out var players))
            {
                return players.Select(id => 
                {
                    Sessions.TryGetValue(id, out var session);
                    return session;
                }).Where(session => session != null).ToList();
            }

            return Array.Empty<RtmpSession>();
        }
    }
}
