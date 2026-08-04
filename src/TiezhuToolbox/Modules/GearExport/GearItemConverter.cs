using System.Globalization;
using System.Text.Json;

namespace TiezhuToolbox.Modules.GearExport;

/// <summary>将云端原始装备字段转换为 Fribbels 风格 gear.txt 条目。</summary>
public static class GearItemConverter
{
    private static readonly string[] RankByGrade =
        ["Unknown", "Normal", "Good", "Rare", "Heroic", "Epic"];

    private static readonly Dictionary<string, string> GearByType = new(StringComparer.OrdinalIgnoreCase)
    {
        ["weapon"] = "Weapon",
        ["helm"] = "Helmet",
        ["armor"] = "Armor",
        ["neck"] = "Necklace",
        ["ring"] = "Ring",
        ["boot"] = "Boots",
    };

    private static readonly Dictionary<char, string> GearByLetter = new()
    {
        ['w'] = "Weapon",
        ['h'] = "Helmet",
        ['a'] = "Armor",
        ['n'] = "Necklace",
        ['r'] = "Ring",
        ['b'] = "Boots",
    };

    private static readonly Dictionary<string, string> SetByCode = new(StringComparer.Ordinal)
    {
        ["set_acc"] = "HitSet",
        ["set_att"] = "AttackSet",
        ["set_coop"] = "UnitySet",
        ["set_counter"] = "CounterSet",
        ["set_cri_dmg"] = "DestructionSet",
        ["set_cri"] = "CriticalSet",
        ["set_def"] = "DefenseSet",
        ["set_immune"] = "ImmunitySet",
        ["set_max_hp"] = "HealthSet",
        ["set_penetrate"] = "PenetrationSet",
        ["set_rage"] = "RageSet",
        ["set_res"] = "ResistSet",
        ["set_revenge"] = "RevengeSet",
        ["set_scar"] = "InjurySet",
        ["set_speed"] = "SpeedSet",
        ["set_vampire"] = "LifestealSet",
        ["set_shield"] = "ProtectionSet",
        ["set_torrent"] = "TorrentSet",
        ["set_revenant"] = "ReversalSet",
        ["set_riposte"] = "RiposteSet",
        ["set_chase"] = "PursuitSet",
        ["set_opener"] = "WarfareSet",
        ["set_weak"] = "WeakeningSet",
        ["set_might"] = "FervorSet",
    };

    private static readonly Dictionary<string, string> StatByCode = new(StringComparer.Ordinal)
    {
        ["att_rate"] = "AttackPercent",
        ["max_hp_rate"] = "HealthPercent",
        ["def_rate"] = "DefensePercent",
        ["att"] = "Attack",
        ["max_hp"] = "Health",
        ["def"] = "Defense",
        ["speed"] = "Speed",
        ["res"] = "EffectResistancePercent",
        ["cri"] = "CriticalHitChancePercent",
        ["cri_dmg"] = "CriticalHitDamagePercent",
        ["acc"] = "EffectivenessPercent",
        ["coop"] = "DualAttackChancePercent",
    };

    private static readonly Dictionary<string, int> CountByRank = new(StringComparer.Ordinal)
    {
        ["Normal"] = 5,
        ["Good"] = 6,
        ["Rare"] = 7,
        ["Heroic"] = 8,
        ["Epic"] = 9,
    };

    private static readonly Dictionary<string, int> OffsetByRank = new(StringComparer.Ordinal)
    {
        ["Normal"] = 0,
        ["Good"] = 1,
        ["Rare"] = 2,
        ["Heroic"] = 3,
        ["Epic"] = 4,
    };

    public static GearUnpackResult ConvertDocument(JsonDocument response, int minimumEnhance = 0)
    {
        if (!response.RootElement.TryGetProperty("status", out var status)
            || !string.Equals(status.GetString(), "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "第三方解包失败（status 非 SUCCESS）。可改用 Fribbels 导出 gear.txt。");
        }

        var rawItems = new List<JsonElement>();
        if (response.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;
                if (!item.TryGetProperty("f", out var setCode) || setCode.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                    continue;
                if (string.IsNullOrWhiteSpace(setCode.ToString()))
                    continue;
                rawItems.Add(item);
            }
        }

        var converted = new List<GearTxtItem>();
        foreach (var raw in rawItems)
        {
            var item = ConvertItem(raw);
            if (item.Enhance >= minimumEnhance)
                converted.Add(item);
        }

        return new GearUnpackResult
        {
            Document = new GearTxtDocument { Items = converted, Heroes = new List<object>() },
            RawItemCount = rawItems.Count,
            ExportedItemCount = converted.Count,
            LevelZeroCount = converted.Count(x => x.Level == 0),
        };
    }

    private static GearTxtItem ConvertItem(JsonElement raw)
    {
        var rank = ConvertRank(raw);
        var op = raw.TryGetProperty("op", out var opEl) && opEl.ValueKind == JsonValueKind.Array
            ? opEl
            : default;

        return new GearTxtItem
        {
            Gear = ConvertGear(raw),
            Rank = rank,
            Set = ConvertSet(raw),
            Level = raw.TryGetProperty("level", out var level) && level.TryGetInt32(out var lv) ? lv : 0,
            Enhance = ConvertEnhance(rank, op),
            Main = ConvertMainStat(raw, op),
            Substats = ConvertSubStats(op),
            Name = raw.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                ? (name.GetString() ?? "Unknown")
                : "Unknown",
            IngameId = raw.TryGetProperty("id", out var id) ? id.ToString() : null,
            IngameEquippedId = raw.TryGetProperty("p", out var equipped) ? equipped.ToString() : null,
        };
    }

    private static string ConvertGear(JsonElement raw)
    {
        if (raw.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
        {
            var typeText = type.GetString() ?? "";
            if (GearByType.TryGetValue(typeText, out var mapped))
                return mapped;
        }

        if (raw.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
        {
            var codeText = code.GetString() ?? "";
            var baseCode = codeText.Split('_')[0];
            if (baseCode.Length > 0 && GearByLetter.TryGetValue(baseCode[^1], out var byLetter))
                return byLetter;
        }

        return "Unknown";
    }

    private static string ConvertRank(JsonElement raw)
    {
        if (!raw.TryGetProperty("g", out var grade) || !grade.TryGetInt32(out var index))
            return "Unknown";
        if (index < 0 || index >= RankByGrade.Length)
            return "Unknown";
        return RankByGrade[index];
    }

    private static string ConvertSet(JsonElement raw)
    {
        if (!raw.TryGetProperty("f", out var set) || set.ValueKind != JsonValueKind.String)
            return "Unknown";
        var code = set.GetString() ?? "";
        return SetByCode.TryGetValue(code, out var mapped) ? mapped : code;
    }

    private static int ConvertEnhance(string rank, JsonElement op)
    {
        if (op.ValueKind != JsonValueKind.Array)
            return 0;
        var count = Math.Min(op.GetArrayLength() - 1, CountByRank.GetValueOrDefault(rank, 5));
        var offset = OffsetByRank.GetValueOrDefault(rank, 0);
        return Math.Max((count - offset) * 3, 0);
    }

    private static GearTxtStat ConvertMainStat(JsonElement raw, JsonElement op)
    {
        if (op.ValueKind != JsonValueKind.Array || op.GetArrayLength() == 0)
            return new GearTxtStat { Type = "Attack", Value = 0 };

        var mainOp = op[0];
        if (mainOp.ValueKind != JsonValueKind.Array || mainOp.GetArrayLength() == 0)
            return new GearTxtStat { Type = "Attack", Value = 0 };

        var opType = mainOp[0].GetString() ?? "";
        var type = StatByCode.GetValueOrDefault(opType, opType);
        var mainValue = 0d;
        if (raw.TryGetProperty("mainStatValue", out var msv))
            mainValue = ReadNumber(msv);

        if (!IsFlat(opType))
            mainValue = RoundTenths(mainValue * 100);
        if (double.IsNaN(mainValue) || double.IsInfinity(mainValue))
            mainValue = 0;

        return new GearTxtStat { Type = type, Value = mainValue };
    }

    private static List<GearTxtStat> ConvertSubStats(JsonElement op)
    {
        var result = new List<GearTxtStat>();
        if (op.ValueKind != JsonValueKind.Array || op.GetArrayLength() <= 1)
            return result;

        var acc = new Dictionary<string, (double Value, int Rolls, bool Modified)>(StringComparer.Ordinal);
        for (var i = 1; i < op.GetArrayLength(); i++)
        {
            var entry = op[i];
            if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() < 2)
                continue;

            var opType = entry[0].GetString() ?? "";
            var type = StatByCode.GetValueOrDefault(opType, opType);
            var opValue = ReadNumber(entry[1]);
            var value = IsFlat(opType) ? opValue : RoundTenths(opValue * 100);
            var annotation = entry.GetArrayLength() > 2 && entry[2].ValueKind == JsonValueKind.String
                ? entry[2].GetString()
                : null;

            if (acc.TryGetValue(type, out var existing))
            {
                var rolls = existing.Rolls;
                var modified = existing.Modified;
                if (annotation == "c")
                    modified = true;
                else if (annotation != "u")
                    rolls += 1;
                acc[type] = (existing.Value + value, rolls, modified);
            }
            else
            {
                acc[type] = (value, 1, false);
            }
        }

        foreach (var (type, data) in acc)
        {
            result.Add(new GearTxtStat
            {
                Type = type,
                Value = data.Value,
                Rolls = data.Rolls,
                Modified = data.Modified ? true : null,
            });
        }

        return result;
    }

    private static bool IsFlat(string text)
        => text is "max_hp" or "speed" or "att" or "def";

    private static double RoundTenths(double value)
        => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private static double ReadNumber(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String when double.TryParse(
                element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                => parsed,
            _ => 0,
        };
}
