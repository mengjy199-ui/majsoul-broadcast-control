using MajsoulBroadcastScorer.V27.Services;

namespace MajsoulBroadcastScorer.Tests.V27;

public sealed class OverlayPageTests
{
    [Fact]
    public void Overlay_HasFixedCanvasAndFourSeamlessCards()
    {
        Assert.Contains("width: 1920px", ObsOverlayPage.Html);
        Assert.Contains("height: 1080px", ObsOverlayPage.Html);
        Assert.Contains("left: 370px", ObsOverlayPage.Html);
        Assert.Contains("top: 856px", ObsOverlayPage.Html);
        Assert.Contains("width: 1550px", ObsOverlayPage.Html);
        Assert.Contains("grid-template-columns: repeat(4, 1fr)", ObsOverlayPage.Html);
        Assert.Contains("for (let i = 0; i < 4; i++)", ObsOverlayPage.Html);
    }

    [Fact]
    public void Overlay_HasSelectableVerticalAvatarLayout()
    {
        Assert.Contains("state.layoutStyle === 'verticalAvatar'", ObsOverlayPage.Html);
        Assert.Contains("stage.classList.toggle('vertical-avatar'", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar #players", ObsOverlayPage.Html);
        Assert.Contains("grid-template-rows: repeat(4, 1fr)", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .avatar-shell", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .name", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .score", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .total", ObsOverlayPage.Html);
    }

    [Fact]
    public void VerticalAvatar_IsTransparentAndHidesAllLeftBackgroundLayers()
    {
        Assert.Contains("#stage.vertical-avatar #left-board,", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .player { width: 354px; height: auto; border: 0; background: transparent; }", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .info", ObsOverlayPage.Html);
        Assert.Contains("background: transparent", ObsOverlayPage.Html);
        Assert.DoesNotContain("border-bottom: 3px solid", ObsOverlayPage.Html);
    }

    [Fact]
    public void VerticalAvatar_ShowsWindBadgeAndRiichiBreathingRing()
    {
        Assert.Contains("#stage.vertical-avatar .wind", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .wind.east", ObsOverlayPage.Html);
        Assert.Contains("border-radius: 50%", ObsOverlayPage.Html);
        Assert.Contains(".player.riichi .avatar-shell", ObsOverlayPage.Html);
        Assert.Contains("@keyframes riichi-avatar-pulse", ObsOverlayPage.Html);
        Assert.Contains("prefers-reduced-motion: reduce", ObsOverlayPage.Html);
        Assert.Contains("root.append(info, avatarShell, name, team, wind)", ObsOverlayPage.Html);
    }

    [Fact]
    public void TeamMode_ReusesVerticalLayoutAndRendersAdaptiveTeamPills()
    {
        Assert.Contains("state.layoutStyle === 'teamMode'", ObsOverlayPage.Html);
        Assert.Contains("stage.classList.toggle('team-mode', teamMode)", ObsOverlayPage.Html);
        Assert.Contains("const team = document.createElement('div'); team.className = 'team'", ObsOverlayPage.Html);
        Assert.Contains("#stage.team-mode .team", ObsOverlayPage.Html);
        Assert.Contains("border-radius: 999px", ObsOverlayPage.Html);
        Assert.Contains("function fitTeam", ObsOverlayPage.Html);
        Assert.Contains("fitTeam(nodes.team, player.teamName)", ObsOverlayPage.Html);
        Assert.Contains("未设置队伍", ObsOverlayPage.Html);
    }

    [Fact]
    public void Overlay_UsesApiScoreAndManualCalculationPointsAsSeparateFields()
    {
        Assert.Contains("player.score", ObsOverlayPage.Html);
        Assert.Contains("player.totalPoints", ObsOverlayPage.Html);
        Assert.Contains("unit.textContent = 'pt'", ObsOverlayPage.Html);
        Assert.DoesNotContain("absolute.padStart(4, '0')", ObsOverlayPage.Html);
        Assert.Contains("String(Math.trunc(Number(value || 0)))", ObsOverlayPage.Html);
    }

    [Fact]
    public void VerticalAvatar_UsesBundledSourceHanSerifForNumbers()
    {
        Assert.Contains("font-family: \"Source Han Serif CN\"", ObsOverlayPage.Html);
        Assert.Contains("/asset/font-source-han-serif-cn-digits", ObsOverlayPage.Html);
        Assert.Contains("/asset/font-source-han-serif-cn-lower", ObsOverlayPage.Html);
        Assert.Contains("font-display: block", ObsOverlayPage.Html);
    }

    [Fact]
    public void NumericTransitions_RunOnlyAfterTheFirstChangedValue()
    {
        Assert.Contains("function transitionNumber", ObsOverlayPage.Html);
        Assert.Contains("previousValue !== null && numeric === previousValue", ObsOverlayPage.Html);
        Assert.Contains("previousValue === null ? null", ObsOverlayPage.Html);
        Assert.Contains("translateY(18px)", ObsOverlayPage.Html);
        Assert.Contains("translateY(-18px)", ObsOverlayPage.Html);
        Assert.Contains("numeric > previousValue", ObsOverlayPage.Html);
        Assert.Contains("prefers-reduced-motion: reduce", ObsOverlayPage.Html);
    }

    [Fact]
    public void Overlay_PreservesLastFrameWhenPollingFails()
    {
        Assert.Contains("保留最后一次成功画面", ObsOverlayPage.Html);
        Assert.DoesNotContain("playersRoot.innerHTML", ObsOverlayPage.Html);
    }

    [Fact]
    public void Overlay_AppliesLeftBoardCropFocus()
    {
        Assert.Contains("leftBoard.style.objectPosition", ObsOverlayPage.Html);
        Assert.Contains("state.leftBoardFocusX", ObsOverlayPage.Html);
        Assert.Contains("state.leftBoardFocusY", ObsOverlayPage.Html);
    }

    [Fact]
    public void Overlay_FitsLongNamesWithoutEllipsis()
    {
        Assert.Contains("function fitName", ObsOverlayPage.Html);
        Assert.Contains("size > 10", ObsOverlayPage.Html);
        Assert.Contains("fitName(nodes.name, player.nickname)", ObsOverlayPage.Html);
        Assert.DoesNotContain("text-overflow: ellipsis", ObsOverlayPage.Html);
    }

    [Fact]
    public void Overlay_FitsScoresInsideTheInformationColumn()
    {
        Assert.Contains(".score { position: absolute; left: 12px; right: 7px; top: 105px; height: 48px", ObsOverlayPage.Html);
        Assert.Contains("function fitScore", ObsOverlayPage.Html);
        Assert.Contains("size > 18", ObsOverlayPage.Html);
        Assert.Contains("fitScore(nodes.score, nodes.scoreValue, player.score)", ObsOverlayPage.Html);
    }

    [Fact]
    public void Overlay_AllowsAssetDraggingOnlyInEmbeddedPreview()
    {
        Assert.Contains("const previewMode", ObsOverlayPage.Html);
        Assert.Contains("asset-moved", ObsOverlayPage.Html);
        Assert.Contains("window.chrome?.webview?.postMessage", ObsOverlayPage.Html);
        Assert.Contains("body.preview #logo", ObsOverlayPage.Html);
        Assert.Contains("function placeFromState", ObsOverlayPage.Html);
        Assert.Contains("dragState === 'active'", ObsOverlayPage.Html);
        Assert.Contains("dragState === 'pending'", ObsOverlayPage.Html);
        Assert.Contains("Date.now() + 2000", ObsOverlayPage.Html);
        Assert.Contains("placeFromState(logo", ObsOverlayPage.Html);
        Assert.Contains("placeFromState(bottomLeft", ObsOverlayPage.Html);
    }

    [Fact]
    public void GapConditions_StayInsideLeftColumnAndHideCurrentSituationLayers()
    {
        Assert.Contains("state.layoutStyle === 'gapConditions'", ObsOverlayPage.Html);
        Assert.Contains("#stage.gap-conditions #gap-panel", ObsOverlayPage.Html);
        Assert.Contains("left: 0; top: 0; width: 370px; height: 1080px", ObsOverlayPage.Html);
        Assert.Contains("#stage.gap-conditions #left-board", ObsOverlayPage.Html);
        Assert.Contains("#stage.gap-conditions #titles", ObsOverlayPage.Html);
        Assert.Contains("#stage.gap-conditions #players", ObsOverlayPage.Html);
        Assert.DoesNotContain("gap-round", ObsOverlayPage.Html);
    }

    [Fact]
    public void GapConditions_UseThreeOpponentRowsAndAdaptiveValues()
    {
        Assert.Contains("gap-defense-avatar", ObsOverlayPage.Html);
        Assert.Contains("reversal.opponentThresholds", ObsOverlayPage.Html);
        Assert.Contains("applyGapAvatar(row.avatar", ObsOverlayPage.Html);
        Assert.Contains("fitOpponentValue(row.drop.value", ObsOverlayPage.Html);
        Assert.Contains("fitOpponentValue(row.ron.value", ObsOverlayPage.Html);
        Assert.Contains("fitOpponentValue(nodes.tsumoValue", ObsOverlayPage.Html);
        Assert.Contains("gap-defense-name", ObsOverlayPage.Html);
        Assert.Contains("fitOpponentName(row.name, opponent && opponent.nickname)", ObsOverlayPage.Html);
        Assert.Contains("element.scrollWidth > element.clientWidth", ObsOverlayPage.Html);
        Assert.Contains("size > 16", ObsOverlayPage.Html);
    }

    [Fact]
    public void GapConditions_RenderOnlySelectedPlayerWithoutSeatOrRiichiMarker()
    {
        var renderStart = ObsOverlayPage.Html.IndexOf("function renderGapPanel", StringComparison.Ordinal);
        var renderEnd = ObsOverlayPage.Html.IndexOf("function place", renderStart, StringComparison.Ordinal);
        var renderGapPanel = ObsOverlayPage.Html[renderStart..renderEnd];

        Assert.Contains("const reversal = state.selectedReversal", ObsOverlayPage.Html);
        Assert.Contains("gapNodes = makeGapCard()", ObsOverlayPage.Html);
        Assert.DoesNotContain("gap-self-wind", ObsOverlayPage.Html);
        Assert.DoesNotContain("gapNodes.push(makeGapCard", ObsOverlayPage.Html);
        Assert.DoesNotContain("classList.toggle('riichi'", renderGapPanel);
    }

    [Fact]
    public void GapConditions_AreTallTransparentAndHaveNoLeftAccentLine()
    {
        var cardStart = ObsOverlayPage.Html.IndexOf(".gap-card {", StringComparison.Ordinal);
        var cardEnd = ObsOverlayPage.Html.IndexOf(".gap-rank", cardStart, StringComparison.Ordinal);
        var cardCss = ObsOverlayPage.Html[cardStart..cardEnd];

        Assert.Contains("height: 1056px", cardCss);
        Assert.Contains("background: transparent", cardCss);
        Assert.Contains("border: 0", cardCss);
        Assert.Contains("box-shadow: none", cardCss);
        Assert.DoesNotContain("border-left", cardCss);
        Assert.Contains("grid-template-rows: repeat(3", ObsOverlayPage.Html);
        Assert.Contains("makeOpponentLine('荣和')", ObsOverlayPage.Html);
        Assert.Contains("makeOpponentLine('放铳')", ObsOverlayPage.Html);
        Assert.Contains("ron.root.classList.add('ron-line')", ObsOverlayPage.Html);
        Assert.Contains("drop.root.classList.add('drop-line')", ObsOverlayPage.Html);
        Assert.Contains("tsumo.className = 'gap-tsumo'", ObsOverlayPage.Html);
        Assert.Contains("grid-template-columns: 56px minmax(0,1fr)", ObsOverlayPage.Html);
        Assert.Contains(".gap-defense-line.ron-line { top: 25px; }", ObsOverlayPage.Html);
        Assert.Contains(".gap-defense-line.drop-line { top: 82px; }", ObsOverlayPage.Html);
        Assert.Contains("padding: 0; background: transparent; border: 0; box-shadow: none", ObsOverlayPage.Html);
        Assert.Contains(".gap-defense-row:not(:last-child)::after", ObsOverlayPage.Html);
        Assert.Contains(".gap-defense-label { color: #c8efff; font-size: 22px", ObsOverlayPage.Html);
        Assert.Contains("font: 900 19px/1", ObsOverlayPage.Html);
        Assert.Contains("width: 100%; min-width: 0; height: 34px", ObsOverlayPage.Html);
        Assert.Contains("border-radius: 999px; background: var(--data-pill)", ObsOverlayPage.Html);
    }

    [Fact]
    public void PointSummary_UsesSmallStartAndGameWithLargestTotal()
    {
        Assert.Contains("startLabel.textContent = 'START'", ObsOverlayPage.Html);
        Assert.Contains("gameLabel.textContent = 'GAME'", ObsOverlayPage.Html);
        Assert.DoesNotContain("textContent = 'TOTAL'", ObsOverlayPage.Html);
        Assert.Contains("nodes.startValue.textContent", ObsOverlayPage.Html);
        Assert.Contains("nodes.gameValue.textContent", ObsOverlayPage.Html);
        Assert.Contains("nodes.startValue.textContent = signed(reversal.startPoints)", ObsOverlayPage.Html);
        Assert.Contains("nodes.gameValue.textContent = signed(reversal.currentPoints)", ObsOverlayPage.Html);
        Assert.Contains("nodes.startValue.textContent = signed(player.startPoints)", ObsOverlayPage.Html);
        Assert.Contains("nodes.gameValue.textContent = signed(player.gamePoints)", ObsOverlayPage.Html);
        Assert.Contains("grid-template-columns: 128px minmax(0,1fr)", ObsOverlayPage.Html);
        Assert.Contains("nodes.points.style.display = reversal && state.showTotalPoints ? 'grid' : 'none'", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .score { left: 0; right: 0; top: 29px; height: 53px; color: white; text-align: left", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .point-split { position: absolute; left: 0; right: 0; top: 159px", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .point-cell { min-width: 0; display: grid; grid-template-columns: 55px minmax(0,1fr)", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .info { left: 176px; top: 0; width: 162px", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .name { left: 4px; top: 190px; width: 158px", ObsOverlayPage.Html);
        Assert.DoesNotContain("totalCaption", ObsOverlayPage.Html);
        Assert.Contains("#stage.vertical-avatar .total { left: 0; right: 0; top: 88px; height: 65px", ObsOverlayPage.Html);
        Assert.Contains("font-size: 44px", ObsOverlayPage.Html);
        Assert.Contains("stage.classList.contains('brand-avatar') ? 43 : verticalAvatar ? 46 : 29", ObsOverlayPage.Html);
        Assert.Contains(".gap-score { position: absolute; left: 142px; right: 10px; top: 66px; text-align: left", ObsOverlayPage.Html);
        Assert.Contains("grid-template-rows: repeat(2, 28px); align-content: center; gap: 2px", ObsOverlayPage.Html);
        Assert.Contains("font: 700 21px/1", ObsOverlayPage.Html);
        Assert.Contains(".gap-point-total { min-width: 0; display: flex; align-items: center; }", ObsOverlayPage.Html);
        Assert.Contains("font: 700 44px/1", ObsOverlayPage.Html);
        Assert.Contains("grid-template-rows: repeat(3, 184px); gap: 10px", ObsOverlayPage.Html);
        Assert.Contains("目标：${reversal.targetLabel", ObsOverlayPage.Html);
    }

    [Fact]
    public void Refresh_IsSequential_AndSkipsUnchangedState()
    {
        Assert.Contains("previewMode ? '/api/preview-state' : '/api/state'", ObsOverlayPage.Html);
        Assert.Contains("let lastRevision = -1", ObsOverlayPage.Html);
        Assert.Contains("if (revision === lastRevision) return", ObsOverlayPage.Html);
        Assert.Contains("setTimeout(refresh, 250)", ObsOverlayPage.Html);
        Assert.DoesNotContain("setInterval(refresh, 250)", ObsOverlayPage.Html);
        Assert.Contains("controller.abort()", ObsOverlayPage.Html);
    }
}
