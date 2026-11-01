using System;
using System.Collections.Generic;
using System.IO;

namespace Mythologicum
{
    public enum ParkTransactionCategory
    {
        Admission, Food, Rest, BuildingUpkeep, Salary, Supplies, Construction, Hiring, Research, Expeditions, Other
    }

    [Serializable]
    public sealed class FinanceWindowBucket
    {
        public float age;
        public double[] income = new double[ParkEconomy.CategoryCount];
        public double[] expenses = new double[ParkEconomy.CategoryCount];
    }

    [Serializable]
    public sealed class FinanceSnapshot
    {
        public float trackedSeconds;
        public List<FinanceWindowBucket> buckets = new List<FinanceWindowBucket>();
    }

    /// <summary>
    /// Cash ledger with a bounded, one-second-resolution window of the last 60 simulation seconds.
    /// Starting funds are capital, not income. Running costs can create debt; purchases cannot.
    /// </summary>
    public sealed class ParkEconomy
    {
        public const int CategoryCount = (int)ParkTransactionCategory.Other + 1;
        public const float WindowSeconds = 60f;
        private readonly List<FinanceWindowBucket> recent = new List<FinanceWindowBucket>();
        public float Balance { get; private set; }
        public float Income { get; private set; }
        public float Expenses { get; private set; }
        public bool UnlimitedFunds { get; set; }
        public float TrackedSeconds { get; private set; }
        public float IncomePerMinute => SumWindow(true);
        public float ExpensesPerMinute => SumWindow(false);
        public float ProfitPerMinute => Bounded((double)IncomePerMinute - ExpensesPerMinute);
        public float OperatingExpensesPerMinute => Bounded((double)ExpensesInWindow(ParkTransactionCategory.BuildingUpkeep)
            + ExpensesInWindow(ParkTransactionCategory.Salary) + ExpensesInWindow(ParkTransactionCategory.Supplies));
        public float OperatingProfitPerMinute => Bounded((double)IncomePerMinute - OperatingExpensesPerMinute);
        public float InvestmentExpensesPerMinute => Bounded((double)ExpensesInWindow(ParkTransactionCategory.Construction)
            + ExpensesInWindow(ParkTransactionCategory.Hiring) + ExpensesInWindow(ParkTransactionCategory.Research)
            + ExpensesInWindow(ParkTransactionCategory.Expeditions));
        public float OtherExpensesPerMinute => ExpensesInWindow(ParkTransactionCategory.Other);

        public void Restore(float balance, float income, float expenses, bool unlimited)
        {
            Balance = Finite(balance) ? balance : 0f;
            Income = ValidAmount(income) ? income : 0f;
            Expenses = ValidAmount(expenses) ? expenses : 0f;
            UnlimitedFunds = unlimited;
            recent.Clear(); TrackedSeconds = 0f;
        }

        public ParkEconomy(float startingBalance = 5000f)
        {
            Balance = ValidAmount(startingBalance) ? startingBalance : 0f;
        }

        public bool CanAfford(float amount)
        {
            return ValidAmount(amount) && (amount == 0f || UnlimitedFunds || Balance >= amount);
        }

        public bool TrySpend(float amount, ParkTransactionCategory category = ParkTransactionCategory.Other)
        {
            if (!CanAfford(amount)) return false;
            RecordExpense(amount, category);
            return true;
        }

        public void Earn(float amount, ParkTransactionCategory category = ParkTransactionCategory.Other)
        {
            if (!ValidAmount(amount)) return;
            Income = Bounded((double)Income + amount);
            if (!UnlimitedFunds) Balance = Bounded((double)Balance + amount);
            RecordWindow(amount, category, true);
        }

        public void Tick(float dt, float operatingCostPerSecond, ParkTransactionCategory category = ParkTransactionCategory.BuildingUpkeep)
        {
            if (!ValidAmount(dt) || dt == 0f || !ValidAmount(operatingCostPerSecond)) return;
            RecordExpense((double)dt * operatingCostPerSecond, category);
        }

        /// <summary>Call exactly once per simulation update, independently of the number of buildings.</summary>
        public void Advance(float dt)
        {
            if (!ValidAmount(dt) || dt == 0f) return;
            TrackedSeconds = (float)Math.Min(WindowSeconds, (double)TrackedSeconds + dt);
            for (int i = recent.Count - 1; i >= 0; i--)
            {
                var bucket = recent[i];
                bucket.age = (float)Math.Min(WindowSeconds, (double)bucket.age + dt);
                if (bucket.age >= WindowSeconds) recent.RemoveAt(i);
            }
        }

        public float IncomeInWindow(ParkTransactionCategory category) => CategoryWindow(category, true);
        public float ExpensesInWindow(ParkTransactionCategory category) => CategoryWindow(category, false);

        public FinanceSnapshot CaptureFinance()
        {
            var result = new FinanceSnapshot { trackedSeconds = TrackedSeconds };
            foreach (var bucket in recent)
                result.buckets.Add(new FinanceWindowBucket
                { age = bucket.age, income = (double[])bucket.income.Clone(), expenses = (double[])bucket.expenses.Clone() });
            return result;
        }

        public void RestoreFinance(FinanceSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            recent.Clear(); TrackedSeconds = 0f;
            if (snapshot == null) return; // Older saves have no recent accounting history.
            TrackedSeconds = snapshot.trackedSeconds;
            foreach (var bucket in snapshot.buckets)
                recent.Add(new FinanceWindowBucket
                { age = bucket.age, income = (double[])bucket.income.Clone(), expenses = (double[])bucket.expenses.Clone() });
        }

        public static void ValidateSnapshot(FinanceSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (!Finite(snapshot.trackedSeconds) || snapshot.trackedSeconds < 0f || snapshot.trackedSeconds > WindowSeconds
                || snapshot.buckets == null || snapshot.buckets.Count > 61)
                throw new InvalidDataException("Invalid saved financial window.");
            float previousAge = WindowSeconds;
            foreach (var bucket in snapshot.buckets)
            {
                if (bucket == null || !Finite(bucket.age) || bucket.age < 0f || bucket.age >= WindowSeconds
                    || bucket.age > snapshot.trackedSeconds + .01f || bucket.age >= previousAge
                    || bucket.income == null || bucket.income.Length != CategoryCount
                    || bucket.expenses == null || bucket.expenses.Length != CategoryCount)
                    throw new InvalidDataException("Invalid saved financial bucket.");
                foreach (double value in bucket.income)
                    if (!ValidStoredAmount(value)) throw new InvalidDataException("Invalid saved financial income.");
                foreach (double value in bucket.expenses)
                    if (!ValidStoredAmount(value)) throw new InvalidDataException("Invalid saved financial expense.");
                previousAge = bucket.age;
            }
        }

        private void RecordExpense(double amount, ParkTransactionCategory category)
        {
            Expenses = Bounded(Expenses + amount);
            if (!UnlimitedFunds) Balance = Bounded(Balance - amount);
            RecordWindow(amount, category, false);
        }

        private void RecordWindow(double amount, ParkTransactionCategory category, bool income)
        {
            if (amount <= 0d) return;
            FinanceWindowBucket bucket = recent.Count > 0 ? recent[recent.Count - 1] : null;
            if (bucket == null || bucket.age >= 1f)
            {
                if (recent.Count >= 61) recent.RemoveAt(0);
                bucket = new FinanceWindowBucket(); recent.Add(bucket);
            }
            int index = CategoryIndex(category);
            double[] values = income ? bucket.income : bucket.expenses;
            values[index] = Math.Min(float.MaxValue, values[index] + amount);
        }

        private float CategoryWindow(ParkTransactionCategory category, bool income)
        {
            if ((int)category < 0 || (int)category >= CategoryCount) return 0f;
            double total = 0d;
            foreach (var bucket in recent) total += (income ? bucket.income : bucket.expenses)[(int)category];
            return Bounded(total);
        }

        private float SumWindow(bool income)
        {
            double total = 0d;
            foreach (var bucket in recent)
                foreach (double value in income ? bucket.income : bucket.expenses) total += value;
            return Bounded(total);
        }

        private static int CategoryIndex(ParkTransactionCategory category)
            => (int)category >= 0 && (int)category < CategoryCount ? (int)category : (int)ParkTransactionCategory.Other;
        private static bool ValidStoredAmount(double value) => !double.IsNaN(value) && !double.IsInfinity(value)
            && value >= 0d && value <= float.MaxValue;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidAmount(float value) => Finite(value) && value >= 0f;
        private static float Bounded(double value) => (float)Math.Max(-float.MaxValue, Math.Min(float.MaxValue, value));
    }
}
