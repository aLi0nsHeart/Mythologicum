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
        private RockConstruction rocks;
        private MapGenerationOptions mapOptions;
        private bool selectedMapOptions;
        public MapGenerationOptions MapOptions=>mapOptions?.Clone();
        private void ChooseMap(Bounds bounds,out int width,out int depth,out Vector3 origin)
        {
            var saved=ParkSaveStore.Pending;
            if(saved!=null)
            {
                if(saved.width<1||saved.depth<1||saved.width>128||saved.depth>128||!Finite(saved.cellSize)||saved.cellSize<.25f||saved.cellSize>16f)
                    throw new System.IO.InvalidDataException("Unsupported saved map dimensions.");
                width=saved.width;depth=saved.depth;cellSize=saved.cellSize;mapOptions=saved.generation?.Clone();
            }
            else if(NewGameSetup.Pending!=null)
            {
                mapOptions=NewGameSetup.Pending.Clone();NewGameSetup.Pending=null;
                if(!mapOptions.Validate(out string error))throw new System.ArgumentException(error);
                selectedMapOptions=true;cellSize=MapGenerationOptions.PreviewCellSize;width=mapOptions.width;depth=mapOptions.depth;
            }
            else {width=Mathf.FloorToInt(bounds.size.x/cellSize);depth=Mathf.FloorToInt(bounds.size.z/cellSize);}
            origin=new Vector3(bounds.center.x-width*cellSize/2,bounds.max.y,bounds.center.z-depth*cellSize/2);
        }
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
            if(selectedMapOptions)return mapOptions.CreateHeightField(cellSize,settings.maximumWalkSlope,settings.maximumPathSlope,settings.maximumFoundationEarthwork);
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
            terrainSurface=new ParkTerrainSurface(grid,terrainArt!=null&&terrainArt.grass!=null?terrainArt.grass:material,terrainArt!=null?terrainArt.cliff:null,terrainArt!=null?terrainArt.soil:null);
            GetComponent<IsometricCameraController>()?.SetMapBounds(grid.Origin,grid.Width*grid.CellSize,grid.Depth*grid.CellSize);
        }
        private void SeedBorderForest()
        {
            if(ParkSaveStore.Pending==null&&terrainArt!=null)NatureGeneration.Seed(grid,trees,rocks,terrainArt,grid.Terrain?.Seed??0,
                selectedMapOptions?mapOptions.forestDensity:terrainArt.forest.density);
        }
        private bool GroundHit(Ray ray,out RaycastHit hit,float distance)=>terrainSurface!=null?terrainSurface.Raycast(ray,out hit,distance):ground.Raycast(ray,out hit,distance);
        private void RefreshTerrainAttachments()
        {
            if(grid.Terrain==null||attachmentRevision==grid.Terrain.Revision)return;attachmentRevision=grid.Terrain.Revision;
            if(gridMesh!=null)Destroy(gridMesh);gridMesh=BuildGridVisual.CreateMesh(grid);gridObject.GetComponent<MeshFilter>().sharedMesh=gridMesh;
            paths.RefreshTerrain();fences.RefreshTerrain();trees.RefreshTerrain();graves.RefreshTerrain();rocks?.RefreshTerrain();creatureWorld.RefreshGroundPositions();
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
