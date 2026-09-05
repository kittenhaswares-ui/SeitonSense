using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Lumina.Excel.Sheets;
using SeitonSense.Core;

namespace SeitonSense.Plugin.UI;

internal sealed partial class SettingsWindow
{
    private const int PlayerStatisticsPageSize = 100;

    private string playerStatisticsSearch = string.Empty;
    private string playerStatisticsRankedSearch = "\0";
    private CrystallineConflictPlayerStatsRankingMode playerStatisticsRankingMode =
        CrystallineConflictPlayerStatsRankingMode.LossesAgainst;
    private CrystallineConflictPlayerStatsRankingMode playerStatisticsRankedMode =
        CrystallineConflictPlayerStatsRankingMode.LossesAgainst;
    private long playerStatisticsCatalogGeneration = long.MinValue;
    private ulong playerStatisticsContentId;
    private int playerStatisticsPage;
    private int playerStatisticsRoleCount;
    private CrystallineConflictPlayerStatsEntry[] playerStatisticsEntries = [];
    private CrystallineConflictPlayerStatsRankRow[] playerStatisticsRows = [];
    private CrystallineConflictPlayerStatsRankRow? playerStatisticsArchNemesis;
    private CrystallineConflictPlayerStatsRankRow? playerStatisticsCannonFodder;

    private bool DrawPlayerStatsPage()
    {
        var changed = false;
        ImGui.Spacing();
        ImGui.TextWrapped(
            "Browse separate opponent and teammate histories for the character currently logged in. " +
            "Your W-L always describes your match result, not an individual duel or a player's overall skill.");
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.45f, 0.82f, 0.94f, 1f));
        ImGui.TextWrapped(
            "LOCAL ONLY: player names, worlds, and these match totals stay in Seiton's local statistics file; this history is not uploaded.");
        ImGui.PopStyleColor();

        if (ImGui.CollapsingHeader("Recording and saved history", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= Checkbox(
                "Record local per-map CC W/L",
                configuration.EnableLocalCrystallineConflictMapStatisticsCapture,
                value => configuration.EnableLocalCrystallineConflictMapStatisticsCapture = value);
            changed |= Checkbox(
                "Save local player W/L history",
                configuration.EnableLocalCrystallineConflictPlayerHistory,
                value => configuration.EnableLocalCrystallineConflictPlayerHistory = value);

            DrawPvpStatsImportControls();
            DrawClearPlayerStatisticsControl();

            ImGui.TextDisabled(
                "Only completed Casual and Ranked 5v5 matches count. Unclear results and invalid player identities are ignored.");
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("CC win prediction panel", ImGuiTreeNodeFlags.DefaultOpen))
            changed |= DrawCrystallineConflictPredictionControls();

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Player history by role", ImGuiTreeNodeFlags.DefaultOpen))
            DrawPlayerStatisticsRanking();

        return changed;
    }

    private void DrawPvpStatsImportControls()
    {
        var importSnapshot = pvpStatsHistoryImport.Snapshot;
        if (importSnapshot.IsBusy)
        {
            if (ImGui.Button("Cancel PvpStats import"))
                pvpStatsHistoryImport.Cancel();
            ImGui.ProgressBar(
                (float)Math.Clamp(importSnapshot.Progress, 0d, 1d),
                new Vector2(420f, 0f));
        }
        else if (ImGui.Button("Import old PvpStats player history"))
        {
            pvpStatsHistoryImport.TryStart();
            importSnapshot = pvpStatsHistoryImport.Snapshot;
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("One-time, local import for the character currently logged in.");
            ImGui.TextUnformatted("Wolves' Den is supported while you are out of combat.");
            ImGui.TextUnformatted("Unload PvpStats first so Seiton can prove exclusive read-only access.");
            ImGui.TextUnformatted("Only completed Casual and Ranked 5v5 matches count.");
            ImGui.EndTooltip();
        }

        if (string.IsNullOrWhiteSpace(importSnapshot.Status)) return;
        if (importSnapshot.IsComplete)
        {
            ImGui.TextColored(
                importSnapshot.Success
                    ? new Vector4(0.4f, 0.9f, 0.62f, 1f)
                    : new Vector4(1f, 0.45f, 0.42f, 1f),
                importSnapshot.Status);
        }
        else
        {
            ImGui.TextDisabled(importSnapshot.Status);
        }
    }

    private void DrawClearPlayerStatisticsControl()
    {
        if (ImGui.Button("Clear all characters' saved local CC statistics"))
        {
            crystallineConflictMapStatisticsResetSucceeded =
                resetCrystallineConflictMapStatistics();
            crystallineConflictMapStatisticsResetFeedback =
                crystallineConflictMapStatisticsResetSucceeded
                    ? "All saved local CC map and player statistics were cleared."
                    : "Could not clear local CC statistics; the existing file was left unchanged.";
            crystallineConflictMapStatisticsResetFeedbackUntil =
                Environment.TickCount64 + 6_000;
            if (crystallineConflictMapStatisticsResetSucceeded)
                playerStatisticsCatalogGeneration = long.MinValue;
        }

        if (!string.IsNullOrEmpty(crystallineConflictMapStatisticsResetFeedback) &&
            Environment.TickCount64 <= crystallineConflictMapStatisticsResetFeedbackUntil)
        {
            ImGui.TextColored(
                crystallineConflictMapStatisticsResetSucceeded
                    ? new Vector4(0.4f, 0.9f, 0.62f, 1f)
                    : new Vector4(1f, 0.45f, 0.42f, 1f),
                crystallineConflictMapStatisticsResetFeedback);
        }
        else if (!string.IsNullOrEmpty(crystallineConflictMapStatisticsResetFeedback))
        {
            crystallineConflictMapStatisticsResetFeedback = string.Empty;
            crystallineConflictMapStatisticsResetFeedbackUntil = 0;
        }
    }

    private bool DrawCrystallineConflictPredictionControls()
    {
        var changed = false;
        changed |= Checkbox(
            "Show CC win prediction from team reveal through the match",
            configuration.ShowCrystallineConflictPredictionPanel,
            value => configuration.ShowCrystallineConflictPredictionPanel = value);
        changed |= Checkbox(
            "Update the prediction while the match changes",
            configuration.EnableDynamicCrystallineConflictPrediction,
            value => configuration.EnableDynamicCrystallineConflictPrediction = value);
        changed |= Checkbox(
            "Lock prediction panel",
            configuration.CrystallineConflictPredictionPanelLocked,
            value => configuration.CrystallineConflictPredictionPanelLocked = value);
        ImGui.SameLine();
        changed |= Checkbox(
            "Show background##CrystallineConflictPrediction",
            configuration.CrystallineConflictPredictionPanelShowBackground,
            value => configuration.CrystallineConflictPredictionPanelShowBackground = value);
        changed |= Slider(
            "Prediction panel scale",
            configuration.CrystallineConflictPredictionPanelScale,
            0.75f,
            1.75f,
            value => configuration.CrystallineConflictPredictionPanelScale = value,
            "%.2f x");
        changed |= Slider(
            "Prediction panel background opacity",
            configuration.CrystallineConflictPredictionPanelBackgroundOpacity,
            0f,
            1f,
            value => configuration.CrystallineConflictPredictionPanelBackgroundOpacity = value,
            "%.2f");
        if (ImGui.Button("Reset prediction panel position"))
            resetCrystallineConflictPredictionWindowPosition();

        ImGui.TextDisabled(
            "A playful estimate from matches saved on this PC. Unknown players count as 50%; nothing is uploaded.");
        ImGui.TextDisabled(
            "The movable match panel keeps only the current match's deaths, damage, healing, and crystal time.");
        changed |= DrawOfficialRankControls();
        return changed;
    }

    private void DrawPlayerStatisticsRanking()
    {
        if (playerState.ContentId == 0)
        {
            InvalidatePlayerStatisticsCache();
            ImGui.TextDisabled("Log in on the character whose local CC history you want to browse.");
            return;
        }

        RefreshPlayerStatisticsCache();
        var teammates = CrystallineConflictPlayerStatsRules.IsTeammateMode(playerStatisticsRankingMode);
        var roleWidth = (Math.Max(260f, ImGui.GetContentRegionAvail().X) - ImGui.GetStyle().ItemSpacing.X) * .5f;
        if (DrawPlayerStatisticsModeButton("Opponents", !teammates, roleWidth))
        {
            playerStatisticsRankingMode = CrystallineConflictPlayerStatsRankingMode.LossesAgainst;
            playerStatisticsPage = 0;
            RefreshPlayerStatisticsRanking();
        }
        ImGui.SameLine();
        if (DrawPlayerStatisticsModeButton("Teammates", teammates, roleWidth))
        {
            playerStatisticsRankingMode = CrystallineConflictPlayerStatsRankingMode.WinsTogether;
            playerStatisticsPage = 0;
            RefreshPlayerStatisticsRanking();
        }
        teammates = CrystallineConflictPlayerStatsRules.IsTeammateMode(playerStatisticsRankingMode);
        ImGui.TextWrapped(teammates
            ? "Teammates: only matches on your team. W = you won together; L = you lost together."
            : "Opponents: only matches on the other team. W = your team won against them; L = your team lost against them.");
        ImGui.TextWrapped("Ranked by totals, not skill: a frequently encountered player can lead both wins and losses. Check W-L, win rate, and games together.");
        if (teammates)
            ImGui.TextWrapped("Older teammate history may be incomplete: unknown old role totals are not guessed. New matches and eligible new PvpStats imports record teammate results separately.");
        ImGui.Spacing();
        DrawPlayerStatisticsLeaders();

        ImGui.Spacing();
        ImGui.SetNextItemWidth(430f * ImGuiHelpers.GlobalScale);
        if (ImGui.InputText("Search player or world", ref playerStatisticsSearch, 96))
        {
            playerStatisticsPage = 0;
            playerStatisticsRankedSearch = "\0";
            RefreshPlayerStatisticsRanking();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear##PlayerStatisticsSearch"))
        {
            playerStatisticsSearch = string.Empty;
            playerStatisticsPage = 0;
            playerStatisticsRankedSearch = "\0";
            RefreshPlayerStatisticsRanking();
        }

        var availableWidth = Math.Max(260f, ImGui.GetContentRegionAvail().X);
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var modeWidth = (availableWidth - gap) * 0.5f;
        if (DrawPlayerStatisticsModeButton(
                teammates ? "Most wins together" : "Most losses against",
                playerStatisticsRankingMode == (teammates ? CrystallineConflictPlayerStatsRankingMode.WinsTogether :
                    CrystallineConflictPlayerStatsRankingMode.LossesAgainst),
                modeWidth))
        {
            playerStatisticsRankingMode = teammates ? CrystallineConflictPlayerStatsRankingMode.WinsTogether :
                CrystallineConflictPlayerStatsRankingMode.LossesAgainst;
            playerStatisticsPage = 0;
            RefreshPlayerStatisticsRanking();
        }
        ImGui.SameLine();
        if (DrawPlayerStatisticsModeButton(
                teammates ? "Most losses together" : "Most wins against",
                playerStatisticsRankingMode == (teammates ? CrystallineConflictPlayerStatsRankingMode.LossesTogether :
                    CrystallineConflictPlayerStatsRankingMode.WinsAgainst),
                modeWidth))
        {
            playerStatisticsRankingMode = teammates ? CrystallineConflictPlayerStatsRankingMode.LossesTogether :
                CrystallineConflictPlayerStatsRankingMode.WinsAgainst;
            playerStatisticsPage = 0;
            RefreshPlayerStatisticsRanking();
        }

        DrawPlayerStatisticsPageControls();
        DrawPlayerStatisticsTable();
    }

    private void RefreshPlayerStatisticsCache()
    {
        var contentId = playerState.ContentId;
        var catalog = mapStatistics.GetPlayerStatisticsSnapshot(contentId);
        if (contentId != playerStatisticsContentId ||
            catalog.Generation != playerStatisticsCatalogGeneration)
        {
            var worldSheet = dataManager.GetExcelSheet<World>();
            var entries = new List<CrystallineConflictPlayerStatsEntry>(catalog.Players.Length);
            foreach (var player in catalog.Players)
            {
                if (!worldSheet.TryGetRow(player.WorldId, out var world)) continue;
                var worldName = world.Name.ToString().Trim();
                if (string.IsNullOrWhiteSpace(worldName)) continue;
                entries.Add(new CrystallineConflictPlayerStatsEntry(
                    player.PlayerName,
                    worldName,
                    player.WinsAgainst,
                    player.LossesAgainst,
                    player.LastSeenUnixSeconds,
                    player.WinsTogether,
                    player.LossesTogether));
            }

            playerStatisticsContentId = contentId;
            playerStatisticsCatalogGeneration = catalog.Generation;
            playerStatisticsEntries = entries.ToArray();
            playerStatisticsPage = 0;
            playerStatisticsRankedSearch = "\0";
        }

        RefreshPlayerStatisticsRanking();
    }

    private void RefreshPlayerStatisticsRanking()
    {
        if (playerStatisticsRankingMode == playerStatisticsRankedMode &&
            string.Equals(
                playerStatisticsSearch,
                playerStatisticsRankedSearch,
                StringComparison.Ordinal))
        {
            return;
        }

        playerStatisticsRows = CrystallineConflictPlayerStatsRules.BuildRanking(
            playerStatisticsEntries,
            playerStatisticsRankingMode,
            playerStatisticsSearch);
        var teammates = CrystallineConflictPlayerStatsRules.IsTeammateMode(playerStatisticsRankingMode);
        var leaders = CrystallineConflictPlayerStatsRules.BuildRanking(playerStatisticsEntries,
            teammates ? CrystallineConflictPlayerStatsRankingMode.WinsTogether :
                CrystallineConflictPlayerStatsRankingMode.WinsAgainst);
        playerStatisticsRoleCount = leaders.Length;
        playerStatisticsArchNemesis = FindLeader(leaders, CrystallineConflictPlayerStatsBadge.MostLosses);
        playerStatisticsCannonFodder = FindLeader(leaders, CrystallineConflictPlayerStatsBadge.MostWins);
        playerStatisticsRankedMode = playerStatisticsRankingMode;
        playerStatisticsRankedSearch = playerStatisticsSearch;
        var pageCount = GetPlayerStatisticsPageCount();
        playerStatisticsPage = Math.Clamp(playerStatisticsPage, 0, pageCount - 1);
    }

    private void InvalidatePlayerStatisticsCache()
    {
        playerStatisticsContentId = 0;
        playerStatisticsCatalogGeneration = long.MinValue;
        playerStatisticsEntries = [];
        playerStatisticsRows = [];
        playerStatisticsArchNemesis = null;
        playerStatisticsCannonFodder = null;
        playerStatisticsRoleCount = 0;
        playerStatisticsPage = 0;
        playerStatisticsRankedSearch = "\0";
    }

    private void DrawPlayerStatisticsLeaders()
    {
        var teammates = CrystallineConflictPlayerStatsRules.IsTeammateMode(playerStatisticsRankingMode);
        DrawPlayerStatisticsLeader(
            teammates ? "Most losses together" : "Most losses against",
            playerStatisticsArchNemesis,
            new Vector4(1f, 0.44f, 0.38f, 1f),
            playerStatisticsRoleCount > 0);
        DrawPlayerStatisticsLeader(
            teammates ? "Most wins together" : "Most wins against",
            playerStatisticsCannonFodder,
            new Vector4(0.38f, 0.9f, 0.58f, 1f),
            playerStatisticsRoleCount > 0);
    }

    private static void DrawPlayerStatisticsLeader(
        string label,
        CrystallineConflictPlayerStatsRankRow? leader,
        Vector4 color,
        bool hasRecordedOpponents)
    {
        ImGui.TextColored(color, label);
        ImGui.SameLine();
        if (leader is not { } row)
        {
            ImGui.TextDisabled(
                hasRecordedOpponents
                    ? $"Needs at least {CrystallineConflictPlayerStatsRules.BadgeMinimumEnemyMeetings} games in this role and one matching result"
                    : "No matches recorded in this role yet");
            return;
        }

        ImGui.TextWrapped(
            $"{row.PlayerName} @ {row.WorldName}  ·  {row.Wins:N0}W {row.Losses:N0}L / {row.Games:N0} games  ·  {FormatPlayerStatisticsWinRate(row.WinRate)}");
    }

    private static bool DrawPlayerStatisticsModeButton(string label, bool selected, float width)
    {
        if (selected)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.12f, 0.42f, 0.54f, 0.95f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.16f, 0.52f, 0.66f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.18f, 0.59f, 0.72f, 1f));
        }

        var pressed = ImGui.Button($"{label}##PlayerStatisticsMode", new Vector2(width, 30f * ImGuiHelpers.GlobalScale));
        if (selected) ImGui.PopStyleColor(3);
        return pressed;
    }

    private void DrawPlayerStatisticsPageControls()
    {
        var pageCount = GetPlayerStatisticsPageCount();
        playerStatisticsPage = Math.Clamp(playerStatisticsPage, 0, pageCount - 1);
        ImGui.TextDisabled(
            $"{playerStatisticsRows.Length:N0} players  ·  page {playerStatisticsPage + 1:N0} / {pageCount:N0}");
        ImGui.SameLine();
        ImGui.BeginDisabled(playerStatisticsPage <= 0);
        if (ImGui.SmallButton("< Previous##PlayerStatisticsPage"))
            playerStatisticsPage--;
        ImGui.EndDisabled();
        ImGui.SameLine();
        ImGui.BeginDisabled(playerStatisticsPage + 1 >= pageCount);
        if (ImGui.SmallButton("Next >##PlayerStatisticsPage"))
            playerStatisticsPage++;
        ImGui.EndDisabled();
    }

    private void DrawPlayerStatisticsTable()
    {
        var flags =
            ImGuiTableFlags.Borders |
            ImGuiTableFlags.RowBg |
            ImGuiTableFlags.SizingStretchProp |
            ImGuiTableFlags.ScrollY |
            ImGuiTableFlags.NoSavedSettings;
        var tableHeight = Math.Max(230f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y);
        if (!ImGui.BeginTable(
                "##SeitonSensePlayerStatistics",
                6,
                flags,
                new Vector2(-1f, tableHeight)))
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("RANK", ImGuiTableColumnFlags.WidthFixed, 54f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("PLAYER", ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(CrystallineConflictPlayerStatsRules.IsTeammateMode(playerStatisticsRankingMode)
            ? "W-L TOGETHER" : "W-L AGAINST", ImGuiTableColumnFlags.WidthFixed, 110f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("WIN %", ImGuiTableColumnFlags.WidthFixed, 64f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("ROLE GAMES", ImGuiTableColumnFlags.WidthFixed, 82f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("LAST SEEN (ANY)", ImGuiTableColumnFlags.WidthFixed, 115f * ImGuiHelpers.GlobalScale);
        ImGui.TableHeadersRow();

        var start = playerStatisticsPage * PlayerStatisticsPageSize;
        var end = Math.Min(start + PlayerStatisticsPageSize, playerStatisticsRows.Length);
        for (var index = start; index < end; index++)
        {
            var row = playerStatisticsRows[index];
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted($"#{row.Rank:N0}");

            ImGui.TableSetColumnIndex(1);
            DrawPlayerStatisticsName(row);

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted($"{row.Wins:N0}W {row.Losses:N0}L");

            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(FormatPlayerStatisticsWinRate(row.WinRate));

            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted(row.Games.ToString("N0"));

            ImGui.TableSetColumnIndex(5);
            ImGui.TextUnformatted(FormatPlayerStatisticsLastSeen(row.LastSeenUnixSeconds));
        }

        if (playerStatisticsRows.Length == 0)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TableSetColumnIndex(1);
            ImGui.TextDisabled(
                string.IsNullOrWhiteSpace(playerStatisticsSearch)
                    ? "No matches recorded in this role yet."
                    : "No player matches this search.");
        }

        ImGui.EndTable();
    }

    private static void DrawPlayerStatisticsName(CrystallineConflictPlayerStatsRankRow row)
    {
        var color = (row.Badges & CrystallineConflictPlayerStatsBadge.ArchNemesis) != 0
            ? new Vector4(1f, 0.5f, 0.44f, 1f)
            : (row.Badges & CrystallineConflictPlayerStatsBadge.CannonFodder) != 0
                ? new Vector4(0.42f, 0.9f, 0.6f, 1f)
                : Vector4.One;
        ImGui.TextColored(color, $"{row.PlayerName} @ {row.WorldName}");
    }

    private int GetPlayerStatisticsPageCount() =>
        Math.Max(1, (playerStatisticsRows.Length + PlayerStatisticsPageSize - 1) / PlayerStatisticsPageSize);

    private static CrystallineConflictPlayerStatsRankRow? FindLeader(
        IReadOnlyList<CrystallineConflictPlayerStatsRankRow> rows,
        CrystallineConflictPlayerStatsBadge badge)
    {
        foreach (var row in rows)
        {
            if ((row.Badges & badge) != 0) return row;
        }

        return null;
    }

    private static string FormatPlayerStatisticsWinRate(double winRate) =>
        $"{Math.Clamp(winRate, 0d, 1d) * 100d:0.#}%";

    private static string FormatPlayerStatisticsLastSeen(long unixSeconds)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
                .ToLocalTime()
                .ToString("yyyy-MM-dd");
        }
        catch (ArgumentOutOfRangeException)
        {
            return "—";
        }
    }
}
