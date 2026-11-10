using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mythologicum
{
    public enum ParkNoticeKind { FenceOpened, ExpeditionSuccess, ExpeditionFailure, ResearchFinished, ResearchInterrupted,
        CreatureEscaped, CreatureExposed, VisitorInjured, VisitorDied, FeederEmpty, StaffBlocked }
    public enum ParkNoticeSeverity { Information, Warning, Critical }
    [Serializable] public sealed class ParkNotice
    {
        public int id, count = 1;
        public string key, title, body;
        public string titleId, bodyId;
        public string[] titleArguments, bodyArguments;
        public string LocalizedTitle => FormatStored(titleId, title, titleArguments);
        public string LocalizedBody => FormatStored(bodyId, body, bodyArguments);
        public ParkNoticeKind kind;
        public ParkNoticeSeverity severity;
        public bool read, hasLocation;
        public Vector2Int location;
        public float time;
        public ParkNotice Copy()
        {
            var copy = (ParkNotice)MemberwiseClone();
            copy.titleArguments = titleArguments == null ? null : (string[])titleArguments.Clone();
            copy.bodyArguments = bodyArguments == null ? null : (string[])bodyArguments.Clone();
            return copy;
        }
        internal static string FormatStored(string textId, string legacy, string[] arguments)
        {
            if (string.IsNullOrEmpty(textId)) return legacy ?? "";
            if (!Loc.Has(textId)) return Loc.Resolve(textId);
            object[] values = arguments == null ? Array.Empty<object>() : new object[arguments.Length];
            for (int i = 0; i < values.Length; i++) values[i] = Loc.Resolve(arguments[i]);
            return Loc.Format(textId, values);
        }
    }
    /// <summary>Bounded letter inbox. Nearby repeated events share one unread letter.</summary>
    public sealed class NotificationInbox
    {
        public const int Capacity = 100;
        private readonly float mergeSeconds;
        public NotificationInbox(float mergeSeconds = 5f) => this.mergeSeconds = Math.Max(0, mergeSeconds);
        private readonly List<ParkNotice> entries = new List<ParkNotice>();
        private int nextId;
        public int Revision { get; private set; }
        public float Time { get; private set; }
        public IReadOnlyList<ParkNotice> Entries => entries;
        public int UnreadCount { get { int count=0; foreach(var n in entries) if(!n.read) count++; return count; } }
        public void Tick(float dt) { if(dt>0 && !float.IsNaN(dt) && !float.IsInfinity(dt)) Time+=dt; }
        public void Post(string key, ParkNoticeKind kind, string title, string body, Vector2Int? location=null,
            ParkNoticeSeverity? severity=null)
        {
            ParkNotice notice = null;
            for(int i=entries.Count-1;i>=0;i--)
                if(entries[i].key==key && Time-entries[i].time<=mergeSeconds) { notice=entries[i]; break; }
            if(notice==null)
            {
                notice=new ParkNotice {id=++nextId,key=key,kind=kind}; entries.Add(notice);
                if(entries.Count>Capacity) entries.RemoveAt(0);
            }
            else { notice.count++; entries.Remove(notice); entries.Add(notice); }
            notice.title=title; notice.body=body; notice.read=false; notice.time=Time;
            notice.titleId=notice.bodyId=null;notice.titleArguments=notice.bodyArguments=null;
            notice.severity=severity??SeverityFor(kind);
            notice.hasLocation=location.HasValue; notice.location=location.GetValueOrDefault(); Revision++;
        }
        public void PostLocalized(string key, ParkNoticeKind kind, string titleId, string bodyId, Vector2Int? location=null,
            string[] titleArguments=null, string[] bodyArguments=null, ParkNoticeSeverity? severity=null)
        {
            Post(key, kind, ParkNotice.FormatStored(titleId, null, titleArguments), ParkNotice.FormatStored(bodyId, null, bodyArguments), location, severity);
            var notice=entries[entries.Count-1];
            notice.titleId=titleId;notice.bodyId=bodyId;
            notice.titleArguments=titleArguments==null?null:(string[])titleArguments.Clone();
            notice.bodyArguments=bodyArguments==null?null:(string[])bodyArguments.Clone();
        }
        public void MarkAllRead() { foreach(var n in entries) n.read=true; Revision++; }
        public static ParkNoticeSeverity SeverityFor(ParkNoticeKind kind)
            => kind == ParkNoticeKind.VisitorDied ? ParkNoticeSeverity.Critical
                : kind == ParkNoticeKind.FenceOpened || kind == ParkNoticeKind.ExpeditionFailure
                    || kind == ParkNoticeKind.ResearchInterrupted || kind == ParkNoticeKind.CreatureEscaped
                    || kind == ParkNoticeKind.CreatureExposed || kind == ParkNoticeKind.VisitorInjured
                    || kind == ParkNoticeKind.FeederEmpty || kind == ParkNoticeKind.StaffBlocked
                    ? ParkNoticeSeverity.Warning : ParkNoticeSeverity.Information;
        public void MarkRead(int id) { foreach(var n in entries) if(n.id==id && !n.read) {n.read=true;Revision++;break;} }
        public void Restore(float time, IEnumerable<ParkNotice> saved)
        {
            entries.Clear(); nextId=0; Time=time;
            foreach(var n in saved) {entries.Add(n.Copy());nextId=Math.Max(nextId,n.id);}
            Revision++;
        }
    }
}
