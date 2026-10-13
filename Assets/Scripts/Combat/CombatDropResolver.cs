using System;
using System.Collections.Generic;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 전투 드롭 테이블의 확률과 스테이지별 보상 수량을 계산합니다.
    /// </summary>
    public static class CombatDropResolver
    {
        private static bool Finite(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false;
        }

        public static List<ResolvedDrop> Resolve(
            IEnumerable<DropTableDataRow> rows,
            int stage,
            Func<double> random,
            Action<string> warning = null,
            float itemDropMultiplier = 1f)
        {
            var result = new List<ResolvedDrop>();
            using var dropRows = rows.GetEnumerator();

            while (dropRows.MoveNext() == true)
            {
                var row = dropRows.Current;

                if (row == null ||
                    Finite(row.DropChance) == false ||
                    row.DropChance < 0 ||
                    row.DropChance > 1 ||
                    row.RollCount < 0 ||
                    row.RollCount > 100 ||
                    row.MinAmount < 0 ||
                    row.MaxAmount < row.MinAmount ||
                    row.MaxAmount > 1000000 ||
                    Finite(row.StageAmountWeight) == false ||
                    row.StageAmountWeight < 0 ||
                    string.IsNullOrWhiteSpace(row.RewardKey) == true ||
                    Enum.IsDefined(typeof(RewardType), row.RewardType) == false ||
                    (row.RewardType == RewardType.Gold &&
                    row.RewardKey != "Key_gold") ||
                    (row.RewardType == RewardType.Item &&
                    row.MaxAmount > 100))
                {
                    warning?.Invoke("Invalid drop row: " + row?.Index);
                    continue;
                }

                for (int i = 0; i < row.RollCount; i++)
                {
                    double chance = random();

                    if (double.IsNaN(chance) == true || chance < 0 || chance > 1)
                    {
                        throw new ArgumentOutOfRangeException(nameof(random));
                    }

                    var dropChance = row.RewardType == RewardType.Item ? Math.Min(1d, row.DropChance * Math.Max(0, itemDropMultiplier)) : row.DropChance;

                    if (dropChance <= 0 || (dropChance < 1 && chance >= dropChance))
                    {
                        continue;
                    }

                    double quantity = random();

                    if (double.IsNaN(quantity) == true || quantity < 0 || quantity > 1)
                    {
                        throw new ArgumentOutOfRangeException(nameof(random));
                    }

                    int amount = row.MinAmount + (int)Math.Min(row.MaxAmount - row.MinAmount, Math.Floor(quantity * (row.MaxAmount - row.MinAmount + 1d)));

                    if (row.RewardType == RewardType.Gold)
                    {
                        amount = (int)Math.Min(int.MaxValue, amount + Math.Floor((double)Math.Max(0, stage - 1) * row.StageAmountWeight));
                    }

                    if (amount > 0)
                    {
                        result.Add(new ResolvedDrop(row.RewardType, row.RewardKey, amount));
                    }
                }
            }

            return result;
        }

    }
}
