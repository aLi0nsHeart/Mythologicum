using System;
using System.Collections.Generic;

namespace Mythologicum
{
    /// <summary>Shared A* search for cell routes and connected fence edges.</summary>
    public static class ConstructionRouteFinder
    {
        private readonly struct Entry<T>
        {
            public readonly T Node;
            public readonly float Cost, Priority;
            public readonly int Order;
            public Entry(T node, float cost, float priority, int order)
            { Node = node; Cost = cost; Priority = priority; Order = order; }
        }

        // A tree-backed sorted set allocates a node for every A* insertion. The heap
        // retains the exact priority/insertion-order ordering in a contiguous buffer.
        private sealed class Frontier<T>
        {
            private readonly List<Entry<T>> entries = new List<Entry<T>>();
            public int Count => entries.Count;
            private static bool Before(Entry<T> a, Entry<T> b)
            {
                int comparison = a.Priority.CompareTo(b.Priority);
                return comparison < 0 || (comparison == 0 && a.Order < b.Order);
            }
            public void Add(Entry<T> entry)
            {
                int index = entries.Count;
                entries.Add(entry);
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (!Before(entry, entries[parent])) break;
                    entries[index] = entries[parent]; index = parent;
                }
                entries[index] = entry;
            }
            public Entry<T> RemoveFirst()
            {
                Entry<T> first = entries[0], last = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (entries.Count == 0) return first;
                int index = 0;
                while (index * 2 + 1 < entries.Count)
                {
                    int child = index * 2 + 1;
                    if (child + 1 < entries.Count && Before(entries[child + 1], entries[child])) child++;
                    if (!Before(entries[child], last)) break;
                    entries[index] = entries[child]; index = child;
                }
                entries[index] = last;
                return first;
            }
        }

        public static List<T> Find<T>(T start, T goal, Func<T, IEnumerable<T>> neighbors,
            Predicate<T> canBuild, Func<T, T, int> estimate, Func<T,T,float> traversalCost = null)
        {
            var allowed = new Dictionary<T, bool>();
            bool CanVisit(T node)
            {
                if (!allowed.TryGetValue(node, out bool valid)) allowed[node] = valid = canBuild(node);
                return valid;
            }
            if (!CanVisit(start) || !CanVisit(goal)) return new List<T>();
            var frontier = new Frontier<T>();
            var cost = new Dictionary<T, float> { [start] = 0 };
            var previous = new Dictionary<T, T>();
            int order = 0;
            frontier.Add(new Entry<T>(start, 0, estimate(start, goal), order++));
            while (frontier.Count > 0)
            {
                Entry<T> current = frontier.RemoveFirst();
                if (cost[current.Node] != current.Cost) continue;
                if (EqualityComparer<T>.Default.Equals(current.Node, goal))
                {
                    var result = new List<T> { goal };
                    T node = goal;
                    while (!EqualityComparer<T>.Default.Equals(node, start))
                    { node = previous[node]; result.Add(node); }
                    result.Reverse();
                    return result;
                }
                foreach (T next in neighbors(current.Node))
                {
                    if (!CanVisit(next)) continue;
                    float step=traversalCost?.Invoke(current.Node,next)??1f;
                    // Unit Manhattan estimates remain admissible when every edge costs at least one.
                    if(float.IsNaN(step)||float.IsInfinity(step)||step<1f)continue;
                    float nextCost = current.Cost + step;
                    if (cost.TryGetValue(next, out float oldCost) && oldCost <= nextCost) continue;
                    cost[next] = nextCost;
                    previous[next] = current.Node;
                    frontier.Add(new Entry<T>(next, nextCost, nextCost + estimate(next, goal), order++));
                }
            }
            return new List<T>();
        }
    }

    /// <summary>Keeps the first click and caches the route until the hovered destination changes.</summary>
    public sealed class ConstructionRouteSelection<T>
    {
        public bool HasStart { get; private set; }
        public T Start { get; private set; }
        public List<T> Route { get; private set; } = new List<T>();
        private bool hasGoal;
        private T goal;

        public void Begin(T start) { Reset(); HasStart = true; Start = start; }
        public void Reset() { HasStart = false; hasGoal = false; Route.Clear(); }
        public void Invalidate() => hasGoal = false;

        public void Update(T destination, Func<T, IEnumerable<T>> neighbors,
            Predicate<T> canBuild, Func<T, T, int> estimate, Func<T,T,float> traversalCost = null)
        {
            if (!HasStart || (hasGoal && EqualityComparer<T>.Default.Equals(goal, destination))) return;
            goal = destination;
            hasGoal = true;
            Route = ConstructionRouteFinder.Find(Start, goal, neighbors, canBuild, estimate,traversalCost);
        }
    }
}
