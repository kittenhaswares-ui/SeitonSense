using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SeitonSense.Plugin.Models;
using SeitonSense.Plugin.Services;

namespace SeitonSense.Plugin.UI;

/// <summary>
/// Movable team-reveal and in-match view for the deliberately playful,
/// local-only CC estimate.
/// The service owns all capture and persistence; this window only presents its
/// immutable snapshot.
/// </summary>
internal sealed class CrystallineConflictPredictionWindow : Window
{
    private const string MissingValue = "\u2014";
    private const ImGuiWindowFlags BaseFlags =
        ImGuiWindowFlags.NoTitleBar |
        ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.NoDocking;

    private readonly PluginConfiguration configuration;
    private readonly CrystallineConflictPredictionService predictionService;
    private readonly IGameGui gameGui;
    private readonly ITextureProvider textureProvider;
    private readonly OfficialCrystallineConflictRankService officialRanks;
    private bool resetPosition;
    private bool showAllies = true;

    internal CrystallineConflictPredictionWindow(
        PluginConfiguration configuration,
        CrystallineConflictPredictionService predictionService,
        IGameGui gameGui,
        ITextureProvider textureProvider,
        OfficialCrystallineConflictRankService officialRanks)
        : base("CC Prediction###SeitonSenseCrystallineConflictPrediction")
    {
        this.configuration = configuration;
        this.predictionService = predictionService;
        this.gameGui = gameGui;
        this.textureProvider = textureProvider;
        this.officialRanks = officialRanks;

        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        Flags = BaseFlags;
        Position = new Vector2(72f, 190f);
        PositionCondition = ImGuiCond.FirstUseEver;
    }

    public override bool DrawConditions() =>
        configuration.Enabled &&
        configuration.ShowCrystallineConflictPredictionPanel &&
        !gameGui.GameUiHidden &&
        predictionService.Snapshot.IsActive;

    public override void PreDraw()
    {
        var flags = BaseFlags;
        if (configuration.CrystallineConflictPredictionPanelLocked)
            flags |= ImGuiWindowFlags.NoMove;
        if (!configuration.CrystallineConflictPredictionPanelShowBackground)
            flags |= ImGuiWindowFlags.NoBackground;
        Flags = flags;
        BgAlpha = configuration.CrystallineConflictPredictionPanelShowBackground
            ? Math.Clamp(configuration.CrystallineConflictPredictionPanelBackgroundOpacity, 0f, 1f)
            : 0f;

        var globalScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale);
        var panelScale = Math.Clamp(configuration.CrystallineConflictPredictionPanelScale, 0.75f, 1.75f);
        var uiScale = globalScale * panelScale;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12f, 10f) * uiScale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(7f, 5f) * uiScale);

        if (!resetPosition) return;
        ImGui.SetNextWindowPos(new Vector2(72f, 190f), ImGuiCond.Always);
        resetPosition = false;
    }

    public override void PostDraw() => ImGui.PopStyleVar(2);

    public override void Draw()
    {
        var snapshot = predictionService.Snapshot;
        var panelScale = Math.Clamp(configuration.CrystallineConflictPredictionPanelScale, 0.75f, 1.75f);
        var uiScale = Math.Max(0.5f, ImGuiHelpers.GlobalScale) * panelScale;
        var width = (configuration.ShowOfficialCrystallineConflictRanks ? 820f : 720f) * uiScale;

        ImGui.SetWindowFontScale(panelScale * 1.03f);
        DrawHeader();
        DrawPrediction(snapshot, width, uiScale);
        DrawTeamTabs(width, uiScale);
        DrawPlayerTable(snapshot, width, uiScale);
        DrawFooter(snapshot);
    }

    internal void ResetWindowPosition() => resetPosition = true;

    private void DrawHeader()
    {
        ImGui.TextColored(new Vector4(0.36f, 0.88f, 1f, 1f), "CC WIN PREDICTION");
        ImGui.SameLine();
        ImGui.TextDisabled("LOCAL ESTIMATE  [?]");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("For fun only. This is not a rating or a guaranteed outcome.");
            ImGui.TextUnformatted("It uses only CC matches observed and saved on this PC.");
            ImGui.TextUnformatted("Unknown players count as 50%. Nothing is uploaded.");
            ImGui.TextUnformatted("Official tiers are a separate daily public snapshot, not part of this estimate.");
            ImGui.TextUnformatted("Each W/L belongs to that player: their wins and losses in matches you both played.");
            ImGui.TextUnformatted("It follows their own result whether they were your ally or enemy in that older match.");
            ImGui.TextUnformatted("Live damage and healing can be incomplete until the final scoreboard arrives.");
            ImGui.EndTooltip();
        }

        ImGui.SameLine();
        var lockLabel = configuration.CrystallineConflictPredictionPanelLocked ? "LOCKED" : "MOVE";
        if (!ImGui.SmallButton($"{lockLabel}##SeitonSensePredictionLock")) return;

        configuration.CrystallineConflictPredictionPanelLocked =
            !configuration.CrystallineConflictPredictionPanelLocked;
        configuration.Save();
    }

    private void DrawPrediction(
        CrystallineConflictPredictionSnapshot snapshot,
        float width,
        float uiScale)
    {
        var start = NormalizeProbability(snapshot.StartWinChance);
        var current = configuration.EnableDynamicCrystallineConflictPrediction &&
                      snapshot.HasCombatStarted
            ? NormalizeProbability(snapshot.CurrentWinChance)
            : start;
        var percent = (int)Math.Round(current * 100d, MidpointRounding.AwayFromZero);
        var favorable = current >= 0.5d;
        var accent = favorable
            ? new Vector4(0.25f, 0.86f, 0.58f, 1f)
            : new Vector4(1f, 0.48f, 0.32f, 1f);

        ImGui.Spacing();
        ImGui.SetWindowFontScale(configuration.CrystallineConflictPredictionPanelScale * 1.28f);
        ImGui.TextColored(accent, $"WIN CHANCE  {percent}%");
        ImGui.SetWindowFontScale(configuration.CrystallineConflictPredictionPanelScale * 1.03f);

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, accent);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.08f, 0.10f, 0.14f, 0.92f));
        ImGui.ProgressBar((float)current, new Vector2(width, 24f * uiScale), string.Empty);
        ImGui.PopStyleColor(2);

        if (configuration.EnableDynamicCrystallineConflictPrediction &&
            snapshot.HasCombatStarted &&
            !snapshot.IsFinal)
        {
            var startPercent = (int)Math.Round(start * 100d, MidpointRounding.AwayFromZero);
            ImGui.TextDisabled($"START {startPercent}%  \u00b7  LIVE {percent}%");
        }
        else if (snapshot.IsFinal)
        {
            ImGui.TextDisabled("FINAL SCOREBOARD");
        }
        else
        {
            ImGui.TextDisabled($"OPENING ESTIMATE {percent}%");
        }
    }

    private void DrawTeamTabs(float width, float uiScale)
    {
        ImGui.Spacing();
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var buttonWidth = (width - gap) * 0.5f;
        if (DrawTeamTab("ALLIES", showAllies, buttonWidth, uiScale))
            showAllies = true;
        ImGui.SameLine();
        if (DrawTeamTab("ENEMIES", !showAllies, buttonWidth, uiScale))
            showAllies = false;
    }

    private static bool DrawTeamTab(string label, bool selected, float width, float uiScale)
    {
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.12f, 0.42f, 0.54f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.16f, 0.52f, 0.66f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.18f, 0.59f, 0.72f, 1f));
        }

        var pressed = ImGui.Button($"{label}##SeitonSensePrediction{label}", new Vector2(width, 31f * uiScale));
        if (selected) ImGui.PopStyleColor(3);
        return pressed;
    }

    private void DrawPlayerTable(
        CrystallineConflictPredictionSnapshot snapshot,
        float width,
        float uiScale)
    {
        var players = showAllies ? snapshot.Allies : snapshot.Enemies;
        players ??= [];
        var flags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.SizingFixedFit |
            ImGuiTableFlags.NoSavedSettings;
        var showRanks = configuration.ShowOfficialCrystallineConflictRanks;
        var columns = showRanks ? 6 : 5;
        if (!ImGui.BeginTable("##SeitonSensePredictionPlayers", columns, flags, new Vector2(width, 0f)))
            return;

        ImGui.TableSetupColumn("NAME", ImGuiTableColumnFlags.WidthStretch, 1f);
        if (showRanks) ImGui.TableSetupColumn("OFFICIAL TIER", ImGuiTableColumnFlags.WidthFixed, 112f * uiScale);
        ImGui.TableSetupColumn("D", ImGuiTableColumnFlags.WidthFixed, 42f * uiScale);
        ImGui.TableSetupColumn("DMG", ImGuiTableColumnFlags.WidthFixed, 78f * uiScale);
        ImGui.TableSetupColumn("HEAL", ImGuiTableColumnFlags.WidthFixed, 78f * uiScale);
        ImGui.TableSetupColumn("CRYSTAL", ImGuiTableColumnFlags.WidthFixed, 76f * uiScale);
        ImGui.TableHeadersRow();

        for (var row = 0; row < 5; row++)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, 31f * uiScale);
            if (row < players.Length && players[row] is { } player)
            {
                ImGui.TableSetColumnIndex(0);
                DrawPlayerName(player, uiScale);

                var offset = showRanks ? 1 : 0;
                if (showRanks)
                {
                    ImGui.TableSetColumnIndex(1);
                    DrawOfficialTier(player);
                }
                ImGui.TableSetColumnIndex(1 + offset);
                DrawCell(player.Deaths.ToString(), false);

                ImGui.TableSetColumnIndex(2 + offset);
                DrawCell(FormatCompact(player.DamageDealt), false);

                ImGui.TableSetColumnIndex(3 + offset);
                DrawCell(FormatCompact(player.HealingDone), false);

                ImGui.TableSetColumnIndex(4 + offset);
                DrawCell(FormatCrystalTime(player.CrystalSeconds), false);
            }
            else
            {
                ImGui.TableSetColumnIndex(0);
                ImGui.TextDisabled("WAITING...");
                for (var column = 1; column < columns; column++)
                {
                    ImGui.TableSetColumnIndex(column);
                    DrawCell(MissingValue, true);
                }
            }
        }

        ImGui.EndTable();
    }

    private void DrawOfficialTier(CrystallineConflictPredictionPlayerSnapshot player)
    {
        var status = officialRanks.Status;
        var entry = officialRanks.Find(player.Name, player.HomeWorldId);
        var stale = status.Cache is { } cache && DateTimeOffset.UtcNow - cache.FetchedAt > TimeSpan.FromHours(24);
        if (entry is null) ImGui.TextDisabled("Unknown");
        else ImGui.TextUnformatted(entry.Tier + (stale ? " *" : string.Empty));
        if (!ImGui.IsItemHovered()) return;
        ImGui.BeginTooltip();
        ImGui.TextUnformatted("Published Lodestone tier — not a live rank lookup.");
        ImGui.TextUnformatted("Only the top 300 overall and top 10 per tier are published.");
        ImGui.TextUnformatted("Unknown means not listed or no recent saved data; it does not mean unranked.");
        ImGui.TextUnformatted(status.Message);
        if (status.Cache is { Season: > 0 } saved)
        {
            ImGui.TextUnformatted($"Official snapshot: season {saved.Season}, {saved.SourceUpdatedText}");
            if (stale) ImGui.TextUnformatted("* Saved over 24 hours ago. Refresh waits until outside combat and duties.");
        }
        ImGui.EndTooltip();
    }

    private void DrawPlayerName(
        CrystallineConflictPredictionPlayerSnapshot player,
        float uiScale)
    {
        var iconSize = 23f * uiScale;
        var topLeft = ImGui.GetCursorScreenPos();
        var bottomRight = topLeft + new Vector2(iconSize);
        if (!TryDrawJobIcon(player.JobId, topLeft, bottomRight))
        {
            ImGui.GetWindowDrawList().AddRectFilled(
                topLeft,
                bottomRight,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0.10f, 0.12f, 0.18f, 1f)),
                3f * uiScale);
        }

        ImGui.Dummy(new Vector2(iconSize));
        ImGui.SameLine();
        var name = string.IsNullOrWhiteSpace(player.Name) ? $"PLAYER {player.Slot + 1}" : player.Name;
        if (player.IsLocal) name += "  (YOU)";
        if (player.IsCurrentlyDead)
            ImGui.TextColored(new Vector4(1f, 0.48f, 0.42f, 1f), name);
        else
            ImGui.TextUnformatted(name);
    }

    private bool TryDrawJobIcon(uint jobId, Vector2 topLeft, Vector2 bottomRight)
    {
        if (jobId == 0 ||
            !textureProvider.TryGetFromGameIcon(new GameIconLookup(62000u + jobId), out var shared) ||
            !shared.TryGetWrap(out var wrap, out _))
        {
            return false;
        }

        ImGui.GetWindowDrawList().AddImage(wrap.Handle, topLeft, bottomRight);
        return true;
    }

    private static void DrawCell(string value, bool disabled)
    {
        if (disabled)
            ImGui.TextDisabled(value);
        else
            ImGui.TextUnformatted(value);
    }

    private static void DrawFooter(CrystallineConflictPredictionSnapshot snapshot)
    {
        if (snapshot.LiveTotalsIncomplete && !snapshot.IsFinal)
            ImGui.TextDisabled("Live DMG and HEAL are observed totals; the final scoreboard replaces them.");
        if (!string.IsNullOrWhiteSpace(snapshot.Status))
            ImGui.TextDisabled(snapshot.Status);
    }

    private static double NormalizeProbability(double probability) =>
        double.IsFinite(probability) ? Math.Clamp(probability, 0d, 1d) : 0.5d;

    private static string FormatCompact(long value)
    {
        if (value < 0) return MissingValue;
        if (value >= 1_000_000) return $"{value / 1_000_000d:0.#}m";
        if (value >= 1_000) return $"{value / 1_000d:0.#}k";
        return value.ToString();
    }

    private static string FormatCrystalTime(int? seconds)
    {
        if (seconds is null || seconds < 0) return MissingValue;
        return $"{seconds.Value / 60}:{seconds.Value % 60:00}";
    }
}
