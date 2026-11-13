using System;
using UnityEngine;

namespace Mythologicum
{
    public enum BuildCellContent { Empty, Building, Path }

    /// <summary>Cell coordinates and occupancy, independent of camera and presentation.</summary>
    public sealed class SquareBuildGrid
    {
        public float CellSize { get; }
        public Vector3 Origin { get; }
        public int Width { get; }
        public int Depth { get; }
        public int Revision { get; private set; }
        public GridHeightField Terrain { get; }
        public void NotifyPhysicalChange()=>Revision++;
        // Map bounds are fixed. Direct indexing avoids hashing coordinates on every
        // movement, sight, construction and path-search occupancy lookup.
        private readonly BuildCellContent[] occupied;
        private readonly float[] centerHeights;
        private readonly int[] centerHeightRevisions;

        public SquareBuildGrid(Vector3 origin, float cellSize, int width, int depth, GridHeightField terrain = null)
        {
            if (cellSize <= 0f || width < 1 || depth < 1)
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            Origin = origin;
            CellSize = cellSize;
            Width = width;
            Depth = depth;
            occupied = new BuildCellContent[checked(width * depth)];
            Terrain=terrain;
            if(terrain!=null)
            {
                centerHeights = new float[occupied.Length];
                centerHeightRevisions = new int[occupied.Length];
                // Terrain starts at revision zero; reserve -1 for an unsampled cell.
                for (int i = 0; i < centerHeightRevisions.Length; i++) centerHeightRevisions[i] = -1;
                terrain.Changed+=()=>Revision++;
            }
        }

        public Vector2Int WorldToCell(Vector3 position) => new Vector2Int(
            (int)Math.Floor((position.x - Origin.x) / CellSize),
            (int)Math.Floor((position.z - Origin.z) / CellSize));

        public Vector3 CellCenter(Vector2Int cell)
        {
            var point=Origin+new Vector3((cell.x+.5f)*CellSize,0,(cell.y+.5f)*CellSize);
            if (Terrain != null && Contains(cell))
            {
                // Sample lazily after edits, foundations, regeneration or save restore.
                // Exact centre positions are fixed until the terrain revision changes.
                int index = cell.y * Width + cell.x;
                if (centerHeightRevisions[index] != Terrain.Revision)
                {
                    centerHeights[index] = GroundHeight(point);
                    centerHeightRevisions[index] = Terrain.Revision;
                }
                point.y = centerHeights[index];
            }
            else point.y=GroundHeight(point);
            return point;
        }
        public float GroundHeight(Vector3 point)=>Origin.y+(Terrain?.Sample((point.x-Origin.x)/CellSize,(point.z-Origin.z)/CellSize)??0f);
        public Vector3 NodePosition(Vector2Int node)
        {var p=Origin+new Vector3(node.x*CellSize,0,node.y*CellSize);p.y=GroundHeight(p);return p;}
        public bool GroundWalkable(Vector2Int cell,bool path=false)=>Contains(cell)&&(Terrain?.Walkable(cell,path)??true);
        public bool CanTraverse(Vector2Int from,Vector2Int to,bool path=false)=>GroundWalkable(from,path)&&GroundWalkable(to,path);
        public float MovementCost(Vector2Int from,Vector2Int to)
        {
            float rise=CellCenter(to).y-CellCenter(from).y;
            return (float)Math.Sqrt(1f+rise*rise/(CellSize*CellSize))+Math.Max(0,rise)/CellSize*(Terrain?.UphillCostMultiplier ?? 0f);
        }
        public Vector3 ProjectToGround(Vector3 point,float bodyOffset=0f)
        {
            point.y=GroundHeight(point)+bodyOffset+(Terrain!=null&&ContentAt(WorldToCell(point))==BuildCellContent.Path?PathConstruction.Height+.01f:0f);
            return point;
        }
        public Vector3 ActorPosition(Vector2Int cell,float bodyOffset)=>ProjectToGround(CellCenter(cell),bodyOffset);
        public bool GroundLineOfSight(Vector2Int from,Vector2Int to,float eyeHeight=1.5f)
        {
            if(Terrain==null)return true;
            Vector3 a=CellCenter(from)+Vector3.up*eyeHeight,b=CellCenter(to)+Vector3.up*eyeHeight;
            int samples=Math.Max(Math.Abs(from.x-to.x),Math.Abs(from.y-to.y))*4;
            for(int i=1;i<samples;i++)
            {var p=Vector3.Lerp(a,b,(float)i/samples);if(GroundHeight(p)>p.y+.01f)return false;}
            return true;
        }

        public bool Contains(Vector2Int cell) => cell.x >= 0 && cell.x < Width
            && cell.y >= 0 && cell.y < Depth;

        public BuildCellContent ContentAt(Vector2Int cell) => Contains(cell)
            ? occupied[cell.y * Width + cell.x] : BuildCellContent.Empty;

        public bool IsAvailable(Vector2Int cell) => Contains(cell) && occupied[cell.y * Width + cell.x] == BuildCellContent.Empty;

        public bool TryOccupy(Vector2Int cell, BuildCellContent content = BuildCellContent.Building)
        {
            if (content == BuildCellContent.Empty || !IsAvailable(cell)) return false;
            occupied[cell.y * Width + cell.x] = content;
            Revision++;
            return true;
        }

        public bool TryRemove(Vector2Int cell, BuildCellContent expectedContent)
        {
            if (expectedContent == BuildCellContent.Empty || ContentAt(cell) != expectedContent) return false;
            occupied[cell.y * Width + cell.x] = BuildCellContent.Empty;
            Revision++;
            return true;
        }
    }
}
