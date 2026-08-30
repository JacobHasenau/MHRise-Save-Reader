namespace MHRise_Save_Reader;

internal sealed class MonsterKillCounter
{
    private static readonly MonsterDefinition[] LargeMonsters =
    [
        new(0x00000001, "Rathian", false),
        new(0x00000701, "Apex Rathian", false),
        new(0x00000002, "Rathalos", false),
        new(0x00000702, "Apex Rathalos", false),
        new(0x00000003, "Khezu", false),
        new(0x00000004, "Basarios", false),
        new(0x00000007, "Diablos", false),
        new(0x00000707, "Apex Diablos", false),
        new(0x00000017, "Rajang", false),
        new(0x00000018, "Kushala Daora", false),
        new(0x00000019, "Chameleos", false),
        new(0x0000001B, "Teostra", false),
        new(0x00000020, "Tigrex", false),
        new(0x00000025, "Nargacuga", false),
        new(0x0000002A, "Barioth", false),
        new(0x0000002C, "Barroth", false),
        new(0x0000002F, "Royal Ludroth", false),
        new(0x00000036, "Great Baggi", false),
        new(0x00000039, "Zinogre", false),
        new(0x00000739, "Apex Zinogre", false),
        new(0x0000003B, "Great Wroggi", false),
        new(0x0000003C, "Arzuros", false),
        new(0x0000073C, "Apex Arzuros", false),
        new(0x0000003D, "Lagombi", false),
        new(0x0000003E, "Volvidon", false),
        new(0x00000052, "Mizutsune", false),
        new(0x00000752, "Apex Mizutsune", false),
        new(0x00000556, "Crimson Glow Valstrax", false),
        new(0x00000059, "Magnamalo", false),
        new(0x0000005A, "Bishaten", false),
        new(0x0000005B, "Aknosom", false),
        new(0x0000005C, "Tetranadon", false),
        new(0x0000005D, "Somnacanth", false),
        new(0x0000005E, "Rakna-Kadaki", false),
        new(0x0000005F, "Almudron", false),
        new(0x00000060, "Wind Serpent Ibushi", false),
        new(0x00000061, "Goss Harag", false),
        new(0x00000062, "Great Izuchi", false),
        new(0x00000063, "Thunder Serpent Narwa", false),
        new(0x00000563, "Narwa the Allmother", false),
        new(0x00000064, "Anjanath", false),
        new(0x00000066, "Pukei-Pukei", false),
        new(0x0000006B, "Kulu-Ya-Ku", false),
        new(0x0000006C, "Jyuratodus", false),
        new(0x0000006D, "Tobi-Kadachi", false),
        new(0x00000076, "Bazelgeuse", false),
        new(0x00000201, "Gold Rathian", true),
        new(0x00000202, "Silver Rathalos", true),
        new(0x00000013, "Daimyo Hermitaur", true),
        new(0x00000014, "Shogun Ceanataur", true),
        new(0x00000517, "Furious Rajang", true),
        new(0x00000225, "Lucent Nargacuga", true),
        new(0x00000047, "Gore Magala", true),
        new(0x00000048, "Shagaru Magala", true),
        new(0x0000004D, "Seregios", true),
        new(0x00000051, "Astalos", true),
        new(0x00000252, "Violet Mizutsune", true),
        new(0x00000559, "Scorned Magnamalo", true),
        new(0x0000015A, "Blood Orange Bishaten", true),
        new(0x0000015D, "Aurora Somnacanth", true),
        new(0x0000015E, "Pyre Rakna-Kadaki", true),
        new(0x0000015F, "Magma Almudron", true),
        new(0x00000576, "Seething Bazelgeuse", true),
        new(0x00000084, "Malzeno", true),
        new(0x00000085, "Lunagaron", true),
        new(0x00000086, "Garangolm", true),
        new(0x00000087, "Gaismagorm", true),
        new(0x00000088, "Espinas", true),
        new(0x00000188, "Flaming Espinas", true),
        new(0x00000818, "Risen Kushala Daora", true),
        new(0x00000819, "Risen Chameleos", true),
        new(0x0000081B, "Risen Teostra", true),
        new(0x00000848, "Risen Shagaru Magala", true),
        new(0x00000856, "Risen Crimson Glow Valstrax", true),
        new(0x00000584, "Primordial Malzeno", true),
        new(0x00000547, "Chaotic Gore Magala", true),
        new(0x0000007C, "Velkhana", true),
        new(0x0000003A, "Amatsu", true)
    ];

    private static readonly int[] CandidateStructSizes = [8, 12, 16, 20, 24, 28, 32];

    public IReadOnlyList<MonsterKillCount> GetMasterRankKillCounts(byte[] saveData)
    {
        var layout = FindMonsterTable(saveData) ?? throw new InvalidDataException("Could not locate the monster statistics table in the save data.");
        var counts = new List<MonsterKillCount>();

        foreach (var monster in LargeMonsters.Where(monster => monster.IsMasterRankMonster))
        {
            if (!layout.IndexByMonsterId.TryGetValue(monster.MonsterTypeId, out var monsterIndex))
            {
                continue;
            }

            var recordOffset = layout.StartOffset + (monsterIndex * layout.StructSize) + layout.CountOffset;
            var killCount = BitConverter.ToUInt32(saveData, recordOffset);
            counts.Add(new MonsterKillCount(monster.DisplayName, killCount));
        }

        return counts.OrderBy(entry => entry.MonsterName).ToArray();
    }

    private static MonsterTableLayout? FindMonsterTable(byte[] saveData)
    {
        MonsterTableLayout? bestLayout = null;

        foreach (var structSize in CandidateStructSizes)
        {
            for (var startOffset = 0; startOffset <= saveData.Length - (structSize * 12); startOffset += 4)
            {
                var matchedMonsters = new List<MonsterDefinition>();
                for (var monsterIndex = 0; monsterIndex < LargeMonsters.Length; monsterIndex++)
                {
                    var currentOffset = startOffset + (monsterIndex * structSize);
                    if (currentOffset + 4 > saveData.Length)
                    {
                        break;
                    }

                    var monsterTypeId = BitConverter.ToUInt32(saveData, currentOffset);
                    if (monsterTypeId != LargeMonsters[monsterIndex].MonsterTypeId)
                    {
                        break;
                    }

                    matchedMonsters.Add(LargeMonsters[monsterIndex]);
                }

                if (matchedMonsters.Count < 12)
                {
                    continue;
                }

                var countOffset = SelectCountOffset(saveData, startOffset, structSize, matchedMonsters.Count);
                if (!countOffset.HasValue)
                {
                    continue;
                }

                var layout = new MonsterTableLayout(
                    startOffset,
                    structSize,
                    countOffset.Value,
                    matchedMonsters
                        .Select((monster, index) => new KeyValuePair<uint, int>(monster.MonsterTypeId, index))
                        .ToDictionary(pair => pair.Key, pair => pair.Value));

                if (bestLayout is null || layout.IndexByMonsterId.Count > bestLayout.IndexByMonsterId.Count)
                {
                    bestLayout = layout;
                }
            }
        }

        return bestLayout;
    }

    private static int? SelectCountOffset(byte[] saveData, int startOffset, int structSize, int monsterCount)
    {
        int? bestOffset = null;
        double bestScore = double.MinValue;

        for (var offset = 4; offset + 4 <= structSize; offset += 4)
        {
            var values = new List<uint>(monsterCount);
            var isValid = true;

            for (var index = 0; index < monsterCount; index++)
            {
                var currentOffset = startOffset + (index * structSize) + offset;
                if (currentOffset + 4 > saveData.Length)
                {
                    isValid = false;
                    break;
                }

                values.Add(BitConverter.ToUInt32(saveData, currentOffset));
            }

            if (!isValid)
            {
                continue;
            }

            var plausibleValues = values.Count(value => value <= 100_000);
            var nonZeroValues = values.Count(value => value > 0 && value <= 100_000);
            var giganticValues = values.Count(value => value > 10_000_000);
            var totalValues = values.Aggregate(0UL, (total, value) => total + value);
            var percentile90 = values.Order().ElementAt((int)Math.Floor((values.Count - 1) * 0.9));
            var consecutivePlusOneSteps = values.Zip(values.Skip(1), (left, right) => right == left + 1 ? 1 : 0).Sum();

            var score = (plausibleValues * 4.0)
                + (nonZeroValues * 2.0)
                + (totalValues / 100.0)
                - (percentile90 / 10.0)
                - (giganticValues * 25.0)
                - (consecutivePlusOneSteps * 100.0);

            if (score > bestScore)
            {
                bestScore = score;
                bestOffset = offset;
            }
        }

        return bestOffset;
    }

    private sealed record MonsterDefinition(uint MonsterTypeId, string DisplayName, bool IsMasterRankMonster);
    private sealed record MonsterTableLayout(int StartOffset, int StructSize, int CountOffset, IReadOnlyDictionary<uint, int> IndexByMonsterId);
}

internal sealed record MonsterKillCount(string MonsterName, uint KillCount);
