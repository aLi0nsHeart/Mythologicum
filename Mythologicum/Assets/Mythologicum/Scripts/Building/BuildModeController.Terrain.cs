using UnityEngine;
namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        private ParkTerrainSurface terrainSurface;
        private Renderer originalGroundRenderer;
        private bool originalGroundColliderEnabled,originalGroundRendererEnabled;
        private int attachmentRevision=-1;
        private TerrainArtDefinition terrainArt;
        public GridHeightField Terrain=>grid?.Terrain;
        public float GroundElevation(Vector3 point)=>grid?.GroundHeight(point)??point.y;
        private GridHeightField CreateTerrain(int width,int depth)
        {
            var asset=Resources.Load<TerrainDefinition>("Terrain/DefaultTerrain");var settings=asset!=null?asset.settings:new TerrainSettings();
            var data=new GridHeightField(width,depth,cellSize,settings.maximumWalkSlope,settings.maximumPathSlope,settings.maximumFoundationEarthwork);
            var saved=ParkSaveStore.Pending;
            if(saved!=null)
            {
                return data; // Restore only after validation; earlier saves retain their flat ground.
            }
            if(settings.generateHills)
            {
                if(settings.heightmap!=null)
                {
                    if(!settings.heightmap.isReadable)throw new System.InvalidOperationException("Terrain heightmap needs Read/Write enabled in its import settings.");
                    var heights=new float[(width+1)*(depth+1)];
                    for(int z=0;z<=depth;z++)for(int x=0;x<=width;x++)heights[z*(width+1)+x]=settings.heightmap.GetPixelBilinear((float)x/width,(float)z/depth).r*settings.relief;
                    data.ImportHeights(heights);
                }
                else data.Generate(settings.seed,settings.relief,settings.featureSize);
            }
            return data;
        }
        private void CreateTerrainSurface(Material material)
        {
            originalGroundRenderer=ground.GetComponent<Renderer>();originalGroundRendererEnabled=originalGroundRenderer.enabled;
            originalGroundColliderEnabled=ground.enabled;ground.enabled=false;originalGroundRenderer.enabled=false;
            terrainArt=Resources.Load<TerrainArtDefinition>("Terrain/TerrainArt");
            terrainSurface=new ParkTerrainSurface(grid,terrainArt!=null&&terrainArt.grass!=null?terrainArt.grass:material,terrainArt!=null?terrainArt.cliff:null);
        }
        private void SeedBorderForest()
        {
            if(ParkSaveStore.Pending==null&&terrainArt!=null)trees.SeedBorder(terrainArt,grid.Terrain?.Seed??0);
        }
        private bool GroundHit(Ray ray,out RaycastHit hit,float distance)=>terrainSurface!=null?terrainSurface.Raycast(ray,out hit,distance):ground.Raycast(ray,out hit,distance);
        private void RefreshTerrainAttachments()
        {
            if(grid.Terrain==null||attachmentRevision==grid.Terrain.Revision)return;attachmentRevision=grid.Terrain.Revision;
            if(gridMesh!=null)Destroy(gridMesh);gridMesh=BuildGridVisual.CreateMesh(grid);gridObject.GetComponent<MeshFilter>().sharedMesh=gridMesh;
            paths.RefreshTerrain();fences.RefreshTerrain();trees.RefreshTerrain();graves.RefreshTerrain();creatureWorld.RefreshGroundPositions();
            pathSelection.Invalidate();fenceSelection.Invalidate();
        }
        private void DisposeTerrain()
        {
            terrainSurface?.Dispose();
            if(ground!=null)ground.enabled=originalGroundColliderEnabled;
            if(originalGroundRenderer!=null)originalGroundRenderer.enabled=originalGroundRendererEnabled;
        }
    }
}
