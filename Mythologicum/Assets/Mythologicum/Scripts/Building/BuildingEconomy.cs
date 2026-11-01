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
                float baseRate = Amount(building.Footprint.Definition.operatingCostPerSecond);
                float salaries = (float)Math.Min(float.MaxValue,
                    building.HiredCount * (double)Amount(building.Footprint.Definition.staffSalaryPerSecond));
                economy.Tick(dt, baseRate, ParkTransactionCategory.BuildingUpkeep);
                economy.Tick(dt, salaries, ParkTransactionCategory.Salary);
            }
        }

        private static float Amount(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f ? value : 0f;
    }
}
