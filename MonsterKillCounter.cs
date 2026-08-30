namespace MHRise_Save_Reader;

internal sealed class MonsterKillCounter
{
    private static readonly MonsterDefinition[] LargeMonsters =
    [
        new(0x00000001, "Rathian"),
        new(0x00000701, "Apex Rathian"),
        new(0x00000002, "Rathalos"),
        new(0x00000702, "Apex Rathalos"),
        new(0x00000003, "Khezu"),
        new(0x00000004, "Basarios"),
        new(0x00000007, "Diablos"),
        new(0x00000707, "Apex Diablos"),
        new(0x00000017, "Rajang"),
        new(0x00000018, "Kushala Daora"),
        new(0x00000019, "Chameleos"),
        new(0x0000001B, "Teostra"),
        new(0x00000020, "Tigrex"),
        new(0x00000025, "Nargacuga"),
        new(0x0000002A, "Barioth"),
        new(0x0000002C, "Barroth"),
        new(0x0000002F, "Royal Ludroth"),
        new(0x00000036, "Great Baggi"),
        new(0x00000039, "Zinogre"),
        new(0x00000739, "Apex Zinogre"),
        new(0x0000003B, "Great Wroggi"),
        new(0x0000003C, "Arzuros"),
        new(0x0000073C, "Apex Arzuros"),
        new(0x0000003D, "Lagombi"),
        new(0x0000003E, "Volvidon"),
        new(0x00000052, "Mizutsune"),
        new(0x00000752, "Apex Mizutsune"),
        new(0x00000556, "Crimson Glow Valstrax"),
        new(0x00000059, "Magnamalo"),
        new(0x0000005A, "Bishaten"),
        new(0x0000005B, "Aknosom"),
        new(0x0000005C, "Tetranadon"),
        new(0x0000005D, "Somnacanth"),
        new(0x0000005E, "Rakna-Kadaki"),
        new(0x0000005F, "Almudron"),
        new(0x00000060, "Wind Serpent Ibushi"),
        new(0x00000061, "Goss Harag"),
        new(0x00000062, "Great Izuchi"),
        new(0x00000063, "Thunder Serpent Narwa"),
        new(0x00000563, "Narwa the Allmother"),
        new(0x00000064, "Anjanath"),
        new(0x00000066, "Pukei-Pukei"),
        new(0x0000006B, "Kulu-Ya-Ku"),
        new(0x0000006C, "Jyuratodus"),
        new(0x0000006D, "Tobi-Kadachi"),
        new(0x00000076, "Bazelgeuse"),
        new(0x00000201, "Gold Rathian"),
        new(0x00000202, "Silver Rathalos"),
        new(0x00000013, "Daimyo Hermitaur"),
        new(0x00000014, "Shogun Ceanataur"),
        new(0x00000517, "Furious Rajang"),
        new(0x00000225, "Lucent Nargacuga"),
        new(0x00000047, "Gore Magala"),
        new(0x00000048, "Shagaru Magala"),
        new(0x0000004D, "Seregios"),
        new(0x00000051, "Astalos"),
        new(0x00000252, "Violet Mizutsune"),
        new(0x00000559, "Scorned Magnamalo"),
        new(0x0000015A, "Blood Orange Bishaten"),
        new(0x0000015D, "Aurora Somnacanth"),
        new(0x0000015E, "Pyre Rakna-Kadaki"),
        new(0x0000015F, "Magma Almudron"),
        new(0x00000576, "Seething Bazelgeuse"),
        new(0x00000084, "Malzeno"),
        new(0x00000085, "Lunagaron"),
        new(0x00000086, "Garangolm"),
        new(0x00000087, "Gaismagorm"),
        new(0x00000088, "Espinas"),
        new(0x00000188, "Flaming Espinas"),
        new(0x00000818, "Risen Kushala Daora"),
        new(0x00000819, "Risen Chameleos"),
        new(0x0000081B, "Risen Teostra"),
        new(0x00000848, "Risen Shagaru Magala"),
        new(0x00000856, "Risen Crimson Glow Valstrax"),
        new(0x00000584, "Primordial Malzeno"),
        new(0x00000547, "Chaotic Gore Magala"),
        new(0x0000007C, "Velkhana"),
        new(0x0000003A, "Amatsu")
    ];

    private static readonly int[] CandidateStructSizes = [8, 12, 16, 20, 24, 28, 32];
    private static readonly byte[] FirstMonsterIdBytes = BitConverter.GetBytes(LargeMonsters[0].MonsterTypeId);

    public IReadOnlyList<MonsterKillCount> GetMasterRankKillCounts(byte[] saveData)
    {
        var layout = FindMonsterTable(saveData) ?? throw new InvalidDataException("Could not locate the monster statistics table in the save data.");
        var counts = new List<MonsterKillCount>();

        foreach (var monster in LargeMonsters)
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
        var candidateOffsets = FindCandidateOffsets(saveData).ToArray();

        foreach (var structSize in CandidateStructSizes)
        {
            foreach (var startOffset in candidateOffsets)
            {
                if (startOffset > saveData.Length - (structSize * 12))
                {
                    continue;
                }

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

    private static IEnumerable<int> FindCandidateOffsets(byte[] saveData)
    {
        for (var startOffset = 0; startOffset <= saveData.Length - FirstMonsterIdBytes.Length; startOffset += 4)
        {
            if (saveData[startOffset] == FirstMonsterIdBytes[0]
                && saveData[startOffset + 1] == FirstMonsterIdBytes[1]
                && saveData[startOffset + 2] == FirstMonsterIdBytes[2]
                && saveData[startOffset + 3] == FirstMonsterIdBytes[3])
            {
                yield return startOffset;
            }
        }
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
            // Keep the original order here so ID-like columns get penalized for simple +1 sequences.
            var consecutivePlusOneSteps = values.Zip(values.Skip(1), (left, right) => right == left + 1 ? 1 : 0).Sum();
            values.Sort();
            var percentile90 = values[(int)Math.Floor((values.Count - 1) * 0.9)];

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

    private sealed record MonsterDefinition(uint MonsterTypeId, string DisplayName);
    private sealed record MonsterTableLayout(int StartOffset, int StructSize, int CountOffset, IReadOnlyDictionary<uint, int> IndexByMonsterId);
}

internal sealed record MonsterKillCount(string MonsterName, uint KillCount);
