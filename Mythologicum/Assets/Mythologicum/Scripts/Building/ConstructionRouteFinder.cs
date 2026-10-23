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
            public readonly int Cost, Priority, Order;
            public Entry(T node, int cost, int priority, int order)
            { Node = node; Cost = cost; Priority = priority; Order = order; }
        }

        public static List<T> Find<T>(T start, T goal, Func<T, IEnumerable<T>> neighbors,
            Predicate<T> canBuild, Func<T, T, int> estimate)
        {
            var allowed = new Dictionary<T, bool>();
            bool CanVisit(T node)
            {
                if (!allowed.TryGetValue(node, out bool valid)) allowed[node] = valid = canBuild(node);
                return valid;
            }
            if (!CanVisit(start) || !CanVisit(goal)) return new List<T>();
            var frontier = new SortedSet<Entry<T>>(Comparer<Entry<T>>.Create((a, b) =>
            {
                int comparison = a.Priority.CompareTo(b.Priority);
                return comparison != 0 ? comparison : a.Order.CompareTo(b.Order);
            }));
            var cost = new Dictionary<T, int> { [start] = 0 };
            var previous = new Dictionary<T, T>();
            int order = 0;
            frontier.Add(new Entry<T>(start, 0, estimate(start, goal), order++));
            while (frontier.Count > 0)
            {
                Entry<T> current = frontier.Min;
                frontier.Remove(current);
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
                    int nextCost = current.Cost + 1;
                    if (cost.TryGetValue(next, out int oldCost) && oldCost <= nextCost) continue;
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
            Predicate<T> canBuild, Func<T, T, int> estimate)
        {
            if (!HasStart || (hasGoal && EqualityComparer<T>.Default.Equals(goal, destination))) return;
            goal = destination;
            hasGoal = true;
            Route = ConstructionRouteFinder.Find(Start, goal, neighbors, canBuild, estimate);
        }
    }
}
