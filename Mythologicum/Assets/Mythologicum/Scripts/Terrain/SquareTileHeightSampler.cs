namespace Mythologicum
{
    /// <summary>
    /// Runtime adaptation of SquareTileTerrain's four-corner height deformation
    /// (SquareTileEditor.SetHeightOnTile). Its four half-weighted interpolation
    /// terms simplify to these two lerps. See Documentation/ThirdParty/SquareTileTerrain.
    /// </summary>
    public static class SquareTileHeightSampler
    {
        public static float Bilinear(float h00,float h10,float h01,float h11,float x,float z)
            => (h00+(h10-h00)*x)*(1f-z)+(h01+(h11-h01)*x)*z;
    }
}
