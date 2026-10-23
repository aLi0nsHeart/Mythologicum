using System;

namespace Mythologicum
{
    /// <summary>Session money ledger. Operating expenses can create debt; purchases cannot.</summary>
    public sealed class ParkEconomy
    {
        public float Balance { get; private set; }
        public float Income { get; private set; }
        public float Expenses { get; private set; }
        public bool UnlimitedFunds { get; set; }

        public ParkEconomy(float startingBalance = 5000f)
        {
            Balance = ValidAmount(startingBalance) ? startingBalance : 0f;
        }

        public bool CanAfford(float amount)
        {
            return ValidAmount(amount) && (amount == 0f || UnlimitedFunds || Balance >= amount);
        }

        public bool TrySpend(float amount)
        {
            if (!CanAfford(amount)) return false;
            RecordExpense(amount);
            return true;
        }

        public void Earn(float amount)
        {
            if (!ValidAmount(amount)) return;
            Income = Bounded((double)Income + amount);
            if (!UnlimitedFunds) Balance = Bounded((double)Balance + amount);
        }

        public void Tick(float dt, float operatingCostPerSecond)
        {
            if (!ValidAmount(dt) || dt == 0f || !ValidAmount(operatingCostPerSecond)) return;
            RecordExpense((double)dt * operatingCostPerSecond);
        }

        private void RecordExpense(double amount)
        {
            Expenses = Bounded(Expenses + amount);
            if (!UnlimitedFunds) Balance = Bounded(Balance - amount);
        }

        private static bool ValidAmount(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        private static float Bounded(double value) => (float)Math.Max(-float.MaxValue, Math.Min(float.MaxValue, value));
    }
}
