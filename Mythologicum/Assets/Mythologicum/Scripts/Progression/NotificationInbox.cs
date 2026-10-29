using System;
using System.Collections.Generic;
using UnityEngine;
namespace Mythologicum
{
    public enum ParkNoticeKind { FenceOpened, ExpeditionSuccess, ExpeditionFailure, ResearchFinished, ResearchInterrupted }
    [Serializable] public sealed class ParkNotice
    {
        public int id, count = 1;
        public string key, title, body;
        public ParkNoticeKind kind;
        public bool read, hasLocation;
        public Vector2Int location;
        public float time;
        public ParkNotice Copy() => (ParkNotice)MemberwiseClone();
    }
    /// <summary>Bounded letter inbox. Nearby repeated events share one unread letter.</summary>
    public sealed class NotificationInbox
    {
        public const int Capacity = 100;
        private readonly List<ParkNotice> entries = new List<ParkNotice>();
        private int nextId;
        public int Revision { get; private set; }
        public float Time { get; private set; }
        public IReadOnlyList<ParkNotice> Entries => entries;
        public int UnreadCount { get { int count=0; foreach(var n in entries) if(!n.read) count++; return count; } }
        public void Tick(float dt) { if(dt>0 && !float.IsNaN(dt) && !float.IsInfinity(dt)) Time+=dt; }
        public void Post(string key, ParkNoticeKind kind, string title, string body, Vector2Int? location=null)
        {
            ParkNotice notice = null;
            for(int i=entries.Count-1;i>=0;i--)
                if(entries[i].key==key && Time-entries[i].time<=5f) { notice=entries[i]; break; }
            if(notice==null)
            {
                notice=new ParkNotice {id=++nextId,key=key,kind=kind}; entries.Add(notice);
                if(entries.Count>Capacity) entries.RemoveAt(0);
            }
            else { notice.count++; entries.Remove(notice); entries.Add(notice); }
            notice.title=title; notice.body=body; notice.read=false; notice.time=Time;
            notice.hasLocation=location.HasValue; notice.location=location.GetValueOrDefault(); Revision++;
        }
        public void MarkAllRead() { foreach(var n in entries) n.read=true; Revision++; }
        public void MarkRead(int id) { foreach(var n in entries) if(n.id==id && !n.read) {n.read=true;Revision++;break;} }
        public void Restore(float time, IEnumerable<ParkNotice> saved)
        {
            entries.Clear(); nextId=0; Time=time;
            foreach(var n in saved) {entries.Add(n.Copy());nextId=Math.Max(nextId,n.id);}
            Revision++;
        }
    }
}
