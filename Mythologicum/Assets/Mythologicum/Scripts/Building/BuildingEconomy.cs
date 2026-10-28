using System;

namespace Mythologicum
{
    /// <summary>Attributes operating expenses to each live building and charges the session ledger once.</summary>
    public static class BuildingEconomy
    {
        public static void Tick(ParkBuildingNetwork network, ParkEconomy economy, float dt)
        {
            if (network == null || economy == null || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            foreach (ParkBuildingRecord building in network.Records)
            {
                float rate = building.UpkeepPerSecond;
                float cost = (float)Math.Min(float.MaxValue, (double)rate * dt);
                building.RecordExpense(cost);
                economy.Tick(dt, rate);
            }
        }
    }
}
