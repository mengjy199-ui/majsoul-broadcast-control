namespace MajsoulBroadcastScorer.V27.Services;

public static class ObsOverlayPage
{
    public const string Html = """
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <style>
    @font-face {
      font-family: "Source Han Serif CN";
      src: url('/asset/font-source-han-serif-cn-digits') format('woff2');
      font-style: normal;
      font-weight: 100 900;
      font-display: block;
      unicode-range: U+0020-0040;
    }
    @font-face {
      font-family: "Source Han Serif CN";
      src: url('/asset/font-source-han-serif-cn-lower') format('woff2');
      font-style: normal;
      font-weight: 100 900;
      font-display: block;
      unicode-range: U+0060-007F,U+0086;
    }
    * { box-sizing: border-box; }
    html, body { margin: 0; width: 100%; height: 100%; overflow: hidden; background: transparent; }
    body { font-family: "Arial Narrow", "Microsoft YaHei UI", sans-serif; }
    #stage {
      --ice: #8bdcff;
      --ice-soft: rgba(139,220,255,.48);
      --ice-faint: rgba(139,220,255,.18);
      --data-pill: rgba(18,82,116,.88);
      --target: #ffe071;
      position: absolute; width: 1920px; height: 1080px; transform-origin: left top; overflow: hidden;
    }
    #left-board { position: absolute; left: 0; top: 0; width: 370px; height: 856px; object-fit: cover; display: none; }
    #logo { position: absolute; object-fit: contain; display: none; }
    #titles { position: absolute; left: 0; width: 370px; color: white; text-align: center; font-weight: 900; text-shadow: 0 3px 8px black; line-height: 1.2; }
    #department { font-size: 34px; }
    #match { margin-top: 8px; font-size: 30px; }
    #bottom-left { position: absolute; object-fit: contain; display: none; }
    #players { position: absolute; left: 370px; top: 856px; width: 1550px; height: 224px; display: grid; grid-template-columns: repeat(4, 1fr); }
    .player { position: relative; min-width: 0; height: 224px; overflow: hidden; border: 2px solid rgba(255,255,255,.92); background: transparent; }
    .player.riichi { border: 7px solid #ed2d2d; }
    .info { position: absolute; left: 0; top: 0; width: 37%; height: 100%; color: white; background: #050505; overflow: hidden; }
    .avatar-shell { position: absolute; right: 0; top: 0; width: 63%; height: 100%; overflow: hidden; }
    .avatar { width: 100%; height: 100%; object-fit: cover; display: none; }
    .avatar-frame { position: absolute; inset: 0; z-index: 5; width: 100%; height: 100%; object-fit: contain; display: none; pointer-events: none; }
    .avatar-shell.frame-hidden { border-color: transparent; box-shadow: none; }
    .wind { position: absolute; left: 0; top: 0; width: 48px; height: 48px; display: grid; place-items: center; background: white; color: black; font-size: 31px; font-weight: 1000; }
    .wind.east { background: #e52d2d; color: white; }
    .name { position: absolute; left: 12px; top: 60px; width: calc(37% - 19px); color: white; overflow: hidden; white-space: nowrap; font-size: 28px; font-weight: 900; line-height: 1.2; }
    .score { position: absolute; left: 12px; right: 7px; top: 105px; height: 48px; overflow: hidden; white-space: nowrap; font-size: 38px; font-weight: 1000; font-variant-numeric: tabular-nums; line-height: 1.2; }
    .total { position: absolute; left: 12px; top: 159px; font-size: 29px; font-weight: 900; font-variant-numeric: tabular-nums; }
    .point-split { display: none; }
    .team { display: none; }
    .avatar-orbit, .divider-effects { display: none; }
    #stage.vertical-avatar #left-board,
    #stage.vertical-avatar #logo,
    #stage.vertical-avatar #titles,
    #stage.vertical-avatar #bottom-left { visibility: hidden; }
    #stage.vertical-avatar #players { left: 0; top: 0; width: 370px; height: 1080px; grid-template-columns: 1fr; grid-template-rows: repeat(4, 1fr); padding: 12px 8px; }
    #stage.vertical-avatar .player { width: 354px; height: auto; border: 0; background: transparent; }
    #stage.vertical-avatar .player:not(:last-child)::after { content: ''; position: absolute; left: 18px; right: 18px; bottom: 0; height: 1px; background: linear-gradient(90deg, transparent, var(--ice-soft) 15%, var(--ice-soft) 85%, transparent); }
    #stage.vertical-avatar .player.riichi { border: 0; }
    #stage.vertical-avatar .avatar-shell { left: 17px; right: auto; top: 25px; width: 148px; height: 148px; border: 6px solid var(--ice); border-radius: 50%; background: transparent; box-shadow: 0 0 12px rgba(103,207,248,.7); }
    #stage.vertical-avatar .player.riichi .avatar-shell { border-color: #ff3349; animation: riichi-avatar-pulse 1.35s ease-in-out infinite; }
    #stage.vertical-avatar .avatar { border-radius: 50%; }
    #stage.vertical-avatar .avatar-frame { border-radius: 0; }
    #stage.vertical-avatar .info { left: 176px; top: 0; width: 162px; height: 100%; background: transparent; overflow: visible; }
    #stage.vertical-avatar .wind { left: 4px; top: 15px; width: 42px; height: 42px; display: grid; z-index: 4; border: 3px solid var(--ice); border-radius: 50%; font-size: 27px; box-shadow: 0 2px 7px rgba(0,0,0,.62); }
    #stage.vertical-avatar .wind.east { border-color: white; }
    #stage.vertical-avatar .name { left: 4px; top: 190px; width: 158px; color: white; text-align: center; font-size: 28px; line-height: 1.2; -webkit-text-stroke: 2.5px #187fc4; paint-order: stroke fill; text-shadow: 0 2px 4px #00111e; }
    #stage.vertical-avatar .score { left: 0; right: 0; top: 29px; height: 53px; color: white; text-align: left; font-family: "Source Han Serif CN", Georgia, serif; font-size: 45px; font-weight: 700; text-shadow: 0 2px 4px black, 0 0 6px black; }
    #stage.vertical-avatar .point-split { position: absolute; left: 0; right: 0; top: 159px; width: auto; height: 52px; display: grid; grid-template-columns: 1fr; grid-template-rows: repeat(2,1fr); gap: 2px; color: white; text-align: left; text-shadow: 0 2px 4px black; }
    #stage.vertical-avatar .point-cell { min-width: 0; display: grid; grid-template-columns: 55px minmax(0,1fr); align-items: center; }
    #stage.vertical-avatar .point-cell + .point-cell { border-left: 0; }
    #stage.vertical-avatar .point-cell small { display: block; color: var(--ice); font-size: 11px; font-weight: 900; letter-spacing: .7px; }
    #stage.vertical-avatar .point-cell strong { display: block; margin-top: 0; font: 700 18px/1 "Source Han Serif CN", Georgia, serif; white-space: nowrap; }
    #stage.vertical-avatar .total { left: 0; right: 0; top: 88px; height: 65px; color: white; text-align: left; font-family: "Source Han Serif CN", Georgia, serif; font-size: 44px; font-weight: 700; text-shadow: 0 2px 4px black, 0 0 6px black; }
    #stage.team-mode .player::before { content: ''; position: absolute; left: 8px; right: 8px; top: 11px; bottom: 11px; border-radius: 20px; background: linear-gradient(105deg, rgba(9,38,58,.12), rgba(25,102,139,.08)); border: 1px solid rgba(139,220,255,.15); }
    #stage.team-mode .avatar-shell { top: 31px; width: 140px; height: 140px; }
    #stage.team-mode .name { left: 14px; top: 184px; width: 146px; font-size: 25px; }
    #stage.team-mode .info { left: 176px; width: 162px; }
    #stage.team-mode .score { top: 24px; }
    #stage.team-mode .total { top: 79px; }
    #stage.team-mode .point-split { top: 173px; }
    #stage.team-mode .team { position: absolute; z-index: 4; left: 176px; top: 138px; width: 162px; height: 28px; padding: 0 12px; display: flex; align-items: center; overflow: hidden; border: 1px solid rgba(139,220,255,.56); border-radius: 999px; background: linear-gradient(90deg, rgba(19,87,121,.82), rgba(13,50,76,.58)); color: #e6f8ff; font-size: 16px; font-weight: 800; letter-spacing: .4px; white-space: nowrap; text-shadow: 0 1px 3px #00111e; box-shadow: inset 0 0 12px rgba(139,220,255,.09); }
    /* 新美术竖排：在左侧保留顶部赛事包装空间，四家只展示昵称与汇总精算点。 */
    #stage.brand-avatar #players { top: 300px; height: 600px; padding: 0 8px; }
    #stage.brand-avatar .player:not(:last-child)::after { display: none; }
    #stage.brand-avatar .player::before { content: ''; position: absolute; left: 170px; top: 61px; width: 176px; height: 24px; background: url('/asset/art-name-divider') center / 100% 100% no-repeat; pointer-events: none; }
    #stage.brand-avatar .avatar-shell { left: 18px; top: 7px; width: 136px; height: 136px; overflow: visible; border: 0; border-radius: 0; box-shadow: none; }
    #stage.brand-avatar .avatar-shell::before { content: ''; position: absolute; inset: 0; z-index: 2; background: url('/asset/art-avatar-frame') center / contain no-repeat; -webkit-mask-image: radial-gradient(circle closest-side at 50% 50%, transparent 74%, #000 76%); mask-image: radial-gradient(circle closest-side at 50% 50%, transparent 74%, #000 76%); pointer-events: none; }
    #stage.brand-avatar .avatar-shell.frame-hidden::before { display: none; }
    #stage.brand-avatar .avatar { position: absolute; left: 12.4%; top: 12.4%; width: 75.2%; height: 75.2%; border-radius: 50%; }
    #stage.brand-avatar .avatar-frame { inset: 0; width: 100%; height: 100%; }
    #stage.brand-avatar .info { left: 174px; top: 0; width: 172px; }
    #stage.brand-avatar .name { left: 174px; top: 24px; width: 172px; height: 34px; color: #f5f39a; text-align: left; font-family: "Microsoft YaHei UI", "Microsoft YaHei", sans-serif; font-size: 27px; font-weight: 900; line-height: 34px; -webkit-text-stroke: 0; text-shadow: 0 2px 4px rgba(0,0,0,.65); }
    #stage.brand-avatar .name.wrapped-name { top: 10px; height: 48px; white-space: normal; overflow-wrap: anywhere; line-height: 1.05; }
    #stage.brand-avatar .total { left: 0; top: 88px; height: 49px; overflow: hidden; color: #fff; font: 800 43px/1.1 Arial, "Microsoft YaHei UI", sans-serif; text-align: left; text-shadow: 0 2px 4px rgba(0,0,0,.65); }
    #stage.brand-avatar .score,
    #stage.brand-avatar .point-split,
    #stage.brand-avatar .wind,
    #stage.brand-avatar .point-unit { display: none !important; }
    #stage.brand-avatar .player.riichi .avatar-shell { border: 0; animation: none; }
    #stage.brand-avatar .player.riichi .avatar-shell::after { content: ''; position: absolute; inset: 9%; z-index: 6; border: 3px solid #ff3349; border-radius: 50%; animation: riichi-avatar-pulse 1.35s ease-in-out infinite; pointer-events: none; }
    #stage.brand-avatar .avatar-orbit { position: absolute; inset: -4px; z-index: 3; display: block; width: calc(100% + 8px); height: calc(100% + 8px); overflow: visible; color: #8bf7df; opacity: .88; shape-rendering: geometricPrecision; pointer-events: none; }
    #stage.brand-avatar .avatar-orbit .orbit-track { transform-box: view-box; transform-origin: 50% 50%; animation: art-orbit-spin 40s linear infinite; animation-delay: var(--orbit-phase, 0s); }
    #stage.brand-avatar .avatar-orbit circle { fill: none; stroke: currentColor; stroke-width: 2; stroke-linecap: round; }
    #stage.brand-avatar .avatar-orbit .orbit-base { stroke-width: 1.4; opacity: .32; }
    #stage.brand-avatar .avatar-orbit .orbit-arc { stroke: #caffec; stroke-width: 2.5; stroke-dasharray: 82 352; stroke-dashoffset: 82; opacity: 1; filter: drop-shadow(0 0 1.5px rgba(101,255,222,.5)); }
    #stage.brand-avatar .avatar-orbit .orbit-head { fill: #ddfff1; stroke: none; filter: drop-shadow(0 0 2px rgba(101,255,222,.55)); }
    #stage.brand-avatar .avatar-shell.frame-disabled .avatar-orbit { display: none; }
    #stage.brand-avatar .player.riichi .avatar-orbit { opacity: .42; }
    #stage.brand-avatar .divider-effects { position: absolute; left: 170px; top: 61px; width: 176px; height: 24px; display: block; overflow: hidden; pointer-events: none; }
    #stage.brand-avatar .star-glow { position: absolute; left: 50%; top: 50%; width: 18px; height: 18px; margin: -9px 0 0 -9px; border-radius: 50%; background: radial-gradient(circle, rgba(223,255,246,.7), rgba(123,238,216,.25) 40%, transparent 72%); opacity: .2; transform: scale(.85); animation: art-star-glow 4.6s ease-in-out infinite; animation-delay: var(--glow-phase, 0s); }
    #stage.brand-avatar .line-sweep { position: absolute; left: 0; top: 11px; width: 42px; height: 2px; border-radius: 50%; background: linear-gradient(90deg, transparent, rgba(232,255,249,.85), transparent); opacity: 0; transform: translateX(-46px); animation: art-line-sweep 7.2s linear infinite; animation-delay: var(--sweep-phase, 0s); }
    #gap-panel { display: none; }
    #stage.gap-conditions #left-board,
    #stage.gap-conditions #logo,
    #stage.gap-conditions #titles,
    #stage.gap-conditions #bottom-left,
    #stage.gap-conditions #players { display: none !important; }
    #stage.gap-conditions #gap-panel {
      position: absolute; left: 0; top: 0; width: 370px; height: 1080px;
      display: block; padding: 12px 8px;
      overflow: hidden;
    }
    .gap-card {
      position: relative; width: 354px; height: 1056px; overflow: hidden;
      border: 0; color: white; background: transparent; box-shadow: none;
      transition: transform 360ms ease, opacity 240ms ease;
    }
    .gap-rank { position: absolute; left: 142px; top: 110px; padding: 4px 10px; border-radius: 999px; background: var(--ice-faint); border: 1px solid var(--ice-soft); color: #d8f3ff; font-size: 15px; font-weight: 800; }
    .gap-self-avatar { position: absolute; left: 20px; top: 20px; width: 108px; height: 108px; border: 4px solid var(--ice); border-radius: 50%; overflow: hidden; background: rgba(63,146,188,.18); }
    .gap-self-avatar img { width: 100%; height: 100%; object-fit: cover; display: none; border-radius: 50%; }
    .gap-self-avatar img.avatar-frame,
    .gap-defense-avatar img.avatar-frame { position: absolute; inset: 0; z-index: 5; width: 100%; height: 100%; object-fit: contain; border-radius: 0; pointer-events: none; }
    .gap-avatar-fallback { position: absolute; inset: 0; display: grid; place-items: center; color: white; font-size: 28px; font-weight: 900; }
    .gap-name { position: absolute; left: 142px; right: 10px; top: 25px; overflow: hidden; white-space: nowrap; font-size: 27px; font-weight: 900; text-shadow: 0 2px 4px #062238; }
    .gap-score { position: absolute; left: 142px; right: 10px; top: 66px; text-align: left; font: 700 34px/1.1 "Source Han Serif CN", Georgia, serif; font-variant-numeric: tabular-nums; text-shadow: 0 2px 4px #062238; }
    .gap-points { position: absolute; left: 20px; right: 20px; top: 148px; height: 76px; display: grid; grid-template-columns: 128px minmax(0,1fr); column-gap: 18px; font-variant-numeric: tabular-nums; text-shadow: 0 2px 4px #062238; }
    .gap-point-mini { display: grid; grid-template-columns: 1fr; grid-template-rows: repeat(2, 28px); align-content: center; gap: 2px; color: white; text-align: left; }
    .gap-point-mini > div { display: grid; grid-template-columns: 50px minmax(0,1fr); align-items: baseline; }
    .gap-point-mini span { color: var(--ice); font-size: 11px; font-weight: 900; letter-spacing: 1px; }
    .gap-point-mini strong { margin-left: 6px; font: 700 21px/1 "Source Han Serif CN", Georgia, serif; }
    .gap-point-total { min-width: 0; display: flex; align-items: center; }
    .gap-point-total strong { display: block; overflow: hidden; white-space: nowrap; font: 700 44px/1 "Source Han Serif CN", Georgia, serif; }
    .gap-target { position: absolute; left: 20px; right: 20px; top: 234px; height: 43px; display: grid; place-items: center; border-radius: 10px; background: var(--ice-faint); border: 1px solid var(--ice-soft); color: var(--target); font-size: 19px; font-weight: 900; text-shadow: 0 2px 4px #062238; }
    .gap-tsumo { position: absolute; left: 20px; right: 20px; top: 290px; height: 43px; display: grid; grid-template-columns: 58px minmax(0,1fr); gap: 8px; align-items: center; }
    .gap-tsumo span { color: #bfe9fb; font-size: 22px; font-weight: 900; text-align: right; text-shadow: 0 2px 4px #062238; }
    .gap-tsumo strong { height: 34px; display: flex; align-items: center; justify-content: center; overflow: hidden; border: 1px solid var(--ice-soft); border-radius: 999px; background: var(--data-pill); color: white; font: 900 19px/1 "Source Han Serif CN", Georgia, serif; text-shadow: 0 2px 4px #062238; }
    .gap-defense-list { position: absolute; left: 12px; right: 12px; top: 350px; display: grid; grid-template-rows: repeat(3, 184px); gap: 10px; }
    .gap-defense-row { position: relative; min-height: 0; padding: 0; background: transparent; border: 0; box-shadow: none; }
    .gap-defense-row:not(:last-child)::after { content: ''; position: absolute; left: 8px; right: 8px; bottom: -6px; height: 1px; background: linear-gradient(90deg, transparent, var(--ice-soft) 16%, var(--ice-soft) 84%, transparent); }
    .gap-defense-avatar { position: absolute; left: 10px; top: 22px; width: 88px; height: 88px; overflow: hidden; border: 3px solid var(--ice); border-radius: 50%; background: rgba(63,146,188,.18); }
    .gap-defense-avatar img { width: 100%; height: 100%; object-fit: cover; display: none; border-radius: 50%; }
    .gap-defense-avatar .gap-avatar-fallback { font-size: 22px; }
    .gap-defense-name { position: absolute; left: 0; top: 119px; width: 108px; overflow: hidden; white-space: nowrap; text-align: center; color: white; font-size: 18px; font-weight: 900; text-shadow: 0 2px 4px #062238; }
    .gap-defense-line { position: absolute; left: 116px; right: 0; height: 44px; display: grid; grid-template-columns: 56px minmax(0,1fr); column-gap: 8px; align-items: center; min-width: 0; white-space: nowrap; }
    .gap-defense-line.ron-line { top: 25px; }
    .gap-defense-line.drop-line { top: 82px; }
    .gap-defense-label { color: #c8efff; font-size: 22px; font-weight: 900; line-height: 1; text-align: right; text-shadow: 0 2px 4px #062238; }
    .gap-defense-line strong { box-sizing: border-box; display: flex; align-items: center; justify-content: center; width: 100%; min-width: 0; height: 34px; margin: 0; padding: 0 8px; overflow: hidden; border: 1px solid var(--ice-soft); border-radius: 999px; background: var(--data-pill); color: white; font: 900 19px/1 "Source Han Serif CN", Georgia, serif; text-shadow: 0 2px 4px #062238; }
    .gap-waiting { position: absolute; inset: 0; display: grid; place-items: center; color: rgba(255,255,255,.82); font-size: 18px; text-shadow: 0 2px 5px #062238; }
    .number-current, .number-previous { position: absolute; inset: 0; display: block; white-space: nowrap; }
    .number-previous { pointer-events: none; }
    .point-unit { margin-left: 4px; font-size: .52em; font-weight: 700; }
    @keyframes riichi-avatar-pulse {
      0%, 100% { box-shadow: 0 0 8px rgba(255,45,68,.55), 0 0 18px rgba(255,45,68,.36); }
      50% { box-shadow: 0 0 18px rgba(255,45,68,1), 0 0 38px rgba(255,45,68,.88); }
    }
    @keyframes art-orbit-spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
    @keyframes art-star-glow { 0%, 100% { transform: scale(.85); opacity: .2; } 50% { transform: scale(1.3); opacity: .5; } }
    @keyframes art-line-sweep {
      0%, 10% { transform: translateX(-46px); opacity: 0; }
      16% { opacity: .42; }
      47% { transform: translateX(180px); opacity: .42; }
      53%, 100% { transform: translateX(180px); opacity: 0; }
    }
    @media (prefers-reduced-motion: reduce) {
      #stage.vertical-avatar .player.riichi .avatar-shell { animation: none; box-shadow: 0 0 22px rgba(255,45,68,.9); }
      #stage.brand-avatar .player.riichi .avatar-shell { box-shadow: none; }
      #stage.brand-avatar .player.riichi .avatar-shell::after { animation: none; box-shadow: 0 0 16px rgba(255,45,68,.9); }
      #stage.brand-avatar .avatar-orbit { opacity: .2; }
      #stage.brand-avatar .avatar-orbit .orbit-track { animation: none; }
      #stage.brand-avatar .star-glow, #stage.brand-avatar .line-sweep { display: none; animation: none; }
    }
    body.preview #logo, body.preview #bottom-left { pointer-events: auto; cursor: move; touch-action: none; }
  </style>
</head>
<body>
  <main id="stage">
    <img id="left-board" alt="">
    <img id="logo" alt="">
    <div id="titles"><div id="department"></div><div id="match"></div></div>
    <img id="bottom-left" alt="">
    <section id="players"></section>
    <section id="gap-panel"></section>
  </main>
  <script>
    const stage = document.getElementById('stage');
    const leftBoard = document.getElementById('left-board');
    const logo = document.getElementById('logo');
    const bottomLeft = document.getElementById('bottom-left');
    const department = document.getElementById('department');
    const matchText = document.getElementById('match');
    const titles = document.getElementById('titles');
    const playersRoot = document.getElementById('players');
    const gapPanel = document.getElementById('gap-panel');
    const playerNodes = [];
    let gapNodes;
    const previewMode = new URLSearchParams(location.search).get('preview') === '1';
    document.body.classList.toggle('preview', previewMode);

    function resize() {
      const scale = Math.min(innerWidth / 1920, innerHeight / 1080);
      stage.style.transform = `scale(${scale})`;
    }
    addEventListener('resize', resize);
    resize();

    function makePlayer(index) {
      const root = document.createElement('article');
      root.className = 'player';
      root.style.setProperty('--orbit-phase', `${-index * 10}s`);
      root.style.setProperty('--glow-phase', `${-index * 1.1}s`);
      root.style.setProperty('--sweep-phase', `${-index * 1.7}s`);
      const info = document.createElement('div'); info.className = 'info';
      const wind = document.createElement('div'); wind.className = 'wind';
      const name = document.createElement('div'); name.className = 'name';
      const team = document.createElement('div'); team.className = 'team';
      const score = document.createElement('div'); score.className = 'score';
      const pointSplit = document.createElement('div'); pointSplit.className = 'point-split';
      const startCell = document.createElement('div'); startCell.className = 'point-cell';
      const startLabel = document.createElement('small'); startLabel.textContent = 'START';
      const startValue = document.createElement('strong');
      startCell.append(startLabel, startValue);
      const gameCell = document.createElement('div'); gameCell.className = 'point-cell';
      const gameLabel = document.createElement('small'); gameLabel.textContent = 'GAME';
      const gameValue = document.createElement('strong');
      gameCell.append(gameLabel, gameValue);
      pointSplit.append(startCell, gameCell);
      const total = document.createElement('div'); total.className = 'total';
      const scoreValue = document.createElement('span'); scoreValue.className = 'number-current';
      const totalValue = document.createElement('span'); totalValue.className = 'number-current';
      score.append(scoreValue);
      total.append(totalValue);
      const avatarShell = document.createElement('div'); avatarShell.className = 'avatar-shell';
      const avatar = document.createElement('img'); avatar.className = 'avatar'; avatar.alt = '';
      const avatarFrame = document.createElement('img'); avatarFrame.className = 'avatar-frame'; avatarFrame.alt = '';
      const orbit = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      orbit.classList.add('avatar-orbit');
      orbit.setAttribute('viewBox', '0 0 144 144');
      orbit.setAttribute('aria-hidden', 'true');
      orbit.setAttribute('focusable', 'false');
      const orbitTrack = document.createElementNS('http://www.w3.org/2000/svg', 'g');
      orbitTrack.classList.add('orbit-track');
      for (const [className, radius] of [['orbit-base', '69'], ['orbit-arc', '69']]) {
        const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        circle.classList.add(className);
        circle.setAttribute('cx', '72'); circle.setAttribute('cy', '72'); circle.setAttribute('r', radius);
        orbitTrack.append(circle);
      }
      const orbitHead = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
      orbitHead.classList.add('orbit-head');
      orbitHead.setAttribute('cx', '141'); orbitHead.setAttribute('cy', '72'); orbitHead.setAttribute('r', '2.5');
      orbitTrack.append(orbitHead);
      orbit.append(orbitTrack);
      const divider = document.createElement('div'); divider.className = 'divider-effects';
      divider.setAttribute('aria-hidden', 'true');
      const starGlow = document.createElement('span'); starGlow.className = 'star-glow';
      const sweep = document.createElement('span'); sweep.className = 'line-sweep';
      divider.append(starGlow, sweep);
      avatarShell.append(avatar, avatarFrame, orbit);
      info.append(score, pointSplit, total);
      root.append(info, avatarShell, name, team, wind);
      root.append(divider);
      playersRoot.append(root);
      return { root, wind, name, team, score, pointSplit, startValue, gameValue, total, scoreValue, totalValue, avatarShell, avatar, avatarFrame, avatarVersion: null, lastScore: null, lastTotal: null };
    }
    for (let i = 0; i < 4; i++) playerNodes.push(makePlayer(i));

    function makeOpponentLine(label) {
      const root = document.createElement('div'); root.className = 'gap-defense-line';
      const labelNode = document.createElement('span'); labelNode.className = 'gap-defense-label'; labelNode.textContent = label;
      const value = document.createElement('strong');
      root.append(labelNode, value);
      return { root, value };
    }

    function makeOpponentRow() {
      const root = document.createElement('div'); root.className = 'gap-defense-row';
      const avatarShell = document.createElement('div'); avatarShell.className = 'gap-defense-avatar';
      const avatar = document.createElement('img'); avatar.alt = '';
      const avatarFrame = document.createElement('img'); avatarFrame.className = 'avatar-frame'; avatarFrame.alt = '';
      const fallback = document.createElement('span'); fallback.className = 'gap-avatar-fallback';
      avatarShell.append(avatar, fallback, avatarFrame);
      const name = document.createElement('div'); name.className = 'gap-defense-name';
      const ron = makeOpponentLine('荣和');
      ron.root.classList.add('ron-line');
      const drop = makeOpponentLine('放铳');
      drop.root.classList.add('drop-line');
      root.append(avatarShell, name, ron.root, drop.root);
      return { root, avatarShell, avatar, avatarFrame, fallback, name, ron, drop };
    }

    function makeGapCard() {
      const root = document.createElement('article'); root.className = 'gap-card';
      const rank = document.createElement('div'); rank.className = 'gap-rank';
      const avatarShell = document.createElement('div'); avatarShell.className = 'gap-self-avatar';
      const avatar = document.createElement('img'); avatar.alt = '';
      const avatarFrame = document.createElement('img'); avatarFrame.className = 'avatar-frame'; avatarFrame.alt = '';
      const avatarFallback = document.createElement('span'); avatarFallback.className = 'gap-avatar-fallback';
      avatarShell.append(avatar, avatarFallback, avatarFrame);
      const name = document.createElement('div'); name.className = 'gap-name';
      const score = document.createElement('div'); score.className = 'gap-score';
      const points = document.createElement('div'); points.className = 'gap-points';
      const pointMini = document.createElement('div'); pointMini.className = 'gap-point-mini';
      const start = document.createElement('div'); start.innerHTML = '<span>START</span><strong></strong>';
      const game = document.createElement('div'); game.innerHTML = '<span>GAME</span><strong></strong>';
      pointMini.append(start, game);
      const pointTotal = document.createElement('div'); pointTotal.className = 'gap-point-total';
      const pointTotalValue = document.createElement('strong');
      pointTotal.append(pointTotalValue);
      points.append(pointMini, pointTotal);
      const target = document.createElement('div'); target.className = 'gap-target';
      const tsumo = document.createElement('div'); tsumo.className = 'gap-tsumo';
      const tsumoLabel = document.createElement('span'); tsumoLabel.textContent = '自摸';
      const tsumoValue = document.createElement('strong');
      tsumo.append(tsumoLabel, tsumoValue);
      const defenseList = document.createElement('div'); defenseList.className = 'gap-defense-list';
      const defenseRows = [makeOpponentRow(), makeOpponentRow(), makeOpponentRow()];
      defenseRows.forEach(row => defenseList.append(row.root));
      const waiting = document.createElement('div'); waiting.className = 'gap-waiting'; waiting.textContent = '等待观战数据';
      root.append(rank, avatarShell, name, score, points, target, tsumo, defenseList, waiting);
      gapPanel.append(root);
      return { root, rank, avatar, avatarFrame, avatarFallback, avatarShell, name, score, points, startValue: start.querySelector('strong'), gameValue: game.querySelector('strong'), totalValue: pointTotalValue, target, tsumo, tsumoValue, defenseList, defenseRows, waiting };
    }
    gapNodes = makeGapCard();

    function applyAsset(element, hasImage, url, version) {
      if (!hasImage) { element.style.display = 'none'; element.removeAttribute('src'); return; }
      const next = `${url}?v=${version || 0}`;
      if (element.getAttribute('src') !== next) element.src = next;
      element.style.display = 'block';
    }

    function applyAvatarFrame(frame, shell, player) {
      const mode = player && player.avatarFrameMode || 'default';
      const hasCustom = mode === 'custom' && player && player.hasAvatarFrame;
      applyAsset(frame, hasCustom, `/asset/avatar-frame-${player ? player.seat : 0}`, player && player.avatarFrameVersion);
      if (shell) {
        shell.classList.toggle('frame-hidden', mode !== 'default');
        shell.classList.toggle('frame-disabled', mode === 'hidden');
      }
    }

    function applyGapAvatar(image, frame, fallback, shell, player) {
      const hasAvatar = player && player.hasAvatar && !player.transparentAvatar;
      applyAsset(image, hasAvatar, `/asset/avatar-${player ? player.seat : 0}`, player && player.avatarVersion);
      fallback.textContent = player && player.nickname ? [...player.nickname][0] : '?';
      fallback.style.display = hasAvatar ? 'none' : 'grid';
      applyAvatarFrame(frame, shell, player);
    }

    function fitGapName(element, text) {
      element.textContent = text || '';
      let size = 25;
      element.style.fontSize = `${size}px`;
      while (element.scrollWidth > element.clientWidth && size > 13) {
        size -= 1;
        element.style.fontSize = `${size}px`;
      }
    }

    function fitOpponentValue(element, text) {
      element.textContent = text || '';
      let size = 19;
      element.style.fontSize = `${size}px`;
      while (element.scrollWidth > element.clientWidth && size > 16) {
        size -= 1;
        element.style.fontSize = `${size}px`;
      }
    }

    function fitOpponentName(element, text) {
      element.textContent = text || '';
      let size = 18;
      element.style.fontSize = `${size}px`;
      while (element.scrollWidth > element.clientWidth && size > 12) {
        size -= 1;
        element.style.fontSize = `${size}px`;
      }
    }

    function renderGapPanel(state) {
      const players = state.players || [];
      const bySeat = new Map(players.map(player => [Number(player.seat), player]));
      const reversal = state.selectedReversal || null;
      const seat = reversal ? Number(reversal.seat) : Number(state.gapSelectedSeat || 0);
      const player = bySeat.get(seat) || { seat, nickname: '', score: 0 };
      const nodes = gapNodes;
      fitGapName(nodes.name, player.nickname);
      nodes.score.textContent = Number(player.score || 0).toLocaleString('zh-CN');
      applyGapAvatar(nodes.avatar, nodes.avatarFrame, nodes.avatarFallback, nodes.avatarShell, player);

      nodes.waiting.style.display = reversal ? 'none' : 'grid';
      nodes.defenseList.style.display = 'none';
      nodes.target.style.display = reversal ? 'grid' : 'none';
      nodes.tsumo.style.display = reversal ? 'grid' : 'none';
      nodes.rank.style.display = reversal ? 'block' : 'none';
      nodes.points.style.display = reversal && state.showTotalPoints ? 'grid' : 'none';
      nodes.score.style.display = reversal ? 'block' : 'none';
      if (!reversal) return;

      nodes.rank.textContent = `当前第 ${Number(reversal.rank)} 位`;
      nodes.startValue.textContent = signed(reversal.startPoints);
      nodes.gameValue.textContent = signed(reversal.currentPoints);
      nodes.totalValue.textContent = `${signed(reversal.projectedTotalPoints)}pt`;
      nodes.target.textContent = `目标：${reversal.targetLabel || '逆转上一位'}`;
      fitOpponentValue(nodes.tsumoValue, reversal.targetSeat === null
        ? (reversal.targetLabel === '保持前二' ? '任意和牌保持前二' : '任意和牌保持一位')
        : (reversal.tsumo || '六倍役满仍不足'));
      nodes.defenseList.style.display = 'grid';
      const opponents = reversal.opponentThresholds || [];
      nodes.defenseRows.forEach((row, index) => {
        const opponentState = opponents[index];
        row.root.style.display = opponentState ? 'block' : 'none';
        if (!opponentState) return;
        const opponent = bySeat.get(Number(opponentState.opponentSeat));
        applyGapAvatar(row.avatar, row.avatarFrame, row.fallback, row.avatarShell, opponent);
        fitOpponentName(row.name, opponent && opponent.nickname);
        fitOpponentValue(row.ron.value, opponentState.ronToTarget);
        fitOpponentValue(row.drop.value, opponentState.dropWarning);
      });
    }

    function place(element, layout) {
      element.style.left = `${layout.x || 0}px`;
      element.style.top = `${layout.y || 0}px`;
      element.style.width = `${layout.width || 1}px`;
      element.style.height = `${layout.height || 1}px`;
    }

    function placeFromState(element, layout) {
      const dragState = element.dataset.dragState;
      if (dragState === 'active') return;
      if (dragState === 'pending') {
        const serverX = Number(layout.x || 0);
        const serverY = Number(layout.y || 0);
        const targetX = Number(element.dataset.dragX || 0);
        const targetY = Number(element.dataset.dragY || 0);
        const saved = Math.abs(serverX - targetX) < 0.5 && Math.abs(serverY - targetY) < 0.5;
        const timedOut = Date.now() > Number(element.dataset.dragUntil || 0);
        if (!saved && !timedOut) return;
        delete element.dataset.dragState;
        delete element.dataset.dragX;
        delete element.dataset.dragY;
        delete element.dataset.dragUntil;
      }
      place(element, layout);
    }

    function signed(value) {
      const number = Number(value || 0);
      const sign = number >= 0 ? '+' : '-';
      const absolute = Math.abs(number).toFixed(1);
      return `${sign}${absolute}`;
    }

    function fitName(element, value) {
      const text = value || '';
      element.textContent = text;
      element.title = text;
      element.classList.remove('wrapped-name');
      let size = stage.classList.contains('brand-avatar') ? 27 : stage.classList.contains('vertical-avatar') ? 30 : 28;
      element.style.fontSize = `${size}px`;
      if (stage.classList.contains('brand-avatar')) {
        while (element.scrollWidth > element.clientWidth && size > 20) {
          size -= 1;
          element.style.fontSize = `${size}px`;
        }
        if (element.scrollWidth > element.clientWidth) {
          element.classList.add('wrapped-name');
          while ((element.scrollWidth > element.clientWidth || element.scrollHeight > element.clientHeight) && size > 10) {
            size -= 1;
            element.style.fontSize = `${size}px`;
          }
        }
        return;
      }
      while (element.scrollWidth > element.clientWidth && size > 10) {
        size -= 1;
        element.style.fontSize = `${size}px`;
      }
    }

    function fitTeam(element, value) {
      const text = value || '未设置队伍';
      element.textContent = text;
      element.title = text;
      let size = 16;
      element.style.fontSize = `${size}px`;
      while (element.scrollWidth > element.clientWidth && size > 11) {
        size -= 1;
        element.style.fontSize = `${size}px`;
      }
    }

    function fitScore(element, valueElement, value) {
      const verticalAvatar = stage.classList.contains('vertical-avatar');
      const text = verticalAvatar
        ? String(Math.trunc(Number(value || 0)))
        : Number(value || 0).toLocaleString('zh-CN');
      valueElement.textContent = text;
      let size = verticalAvatar ? 48 : 38;
      element.style.fontSize = `${size}px`;
      while (valueElement.scrollWidth > element.clientWidth && size > 18) {
        size -= 1;
        element.style.fontSize = `${size}px`;
      }
    }

    function fitTotal(element, valueElement) {
      const verticalAvatar = stage.classList.contains('vertical-avatar');
      let size = stage.classList.contains('brand-avatar') ? 43 : verticalAvatar ? 46 : 29;
      element.style.fontSize = `${size}px`;
      while (valueElement.scrollWidth > element.clientWidth && size > 16) {
        size -= 1;
        element.style.fontSize = `${size}px`;
      }
    }

    function renderTotal(valueElement, value) {
      const number = document.createElement('span');
      number.textContent = signed(value);
      const unit = document.createElement('span');
      unit.className = 'point-unit';
      unit.textContent = 'pt';
      valueElement.replaceChildren(number, unit);
    }

    function transitionNumber(container, current, previousValue, nextValue, render, fit) {
      const numeric = Number(nextValue || 0);
      if (previousValue !== null && numeric === previousValue) {
        render(current, numeric);
        fit();
        return numeric;
      }

      const previousNode = previousValue === null ? null : current.cloneNode(true);
      container.querySelectorAll('.number-previous').forEach(node => node.remove());
      current.getAnimations().forEach(animation => animation.cancel());
      container.getAnimations().forEach(animation => animation.cancel());
      render(current, numeric);
      fit();

      if (previousNode === null || matchMedia('(prefers-reduced-motion: reduce)').matches)
        return numeric;

      previousNode.className = 'number-previous';
      container.append(previousNode);
      current.animate([
        { opacity: 0, transform: 'translateY(18px)' },
        { opacity: 1, transform: 'translateY(0)' }
      ], { duration: 400, easing: 'cubic-bezier(.2,.8,.2,1)' });
      const leaving = previousNode.animate([
        { opacity: 1, transform: 'translateY(0)' },
        { opacity: 0, transform: 'translateY(-18px)' }
      ], { duration: 400, easing: 'cubic-bezier(.2,.8,.2,1)' });
      leaving.finished.then(() => previousNode.remove(), () => previousNode.remove());

      const glow = numeric > previousValue ? 'rgba(83,255,150,.96)' : 'rgba(255,70,92,.96)';
      container.animate([
        { filter: 'none' },
        { filter: `drop-shadow(0 0 13px ${glow})`, offset: .42 },
        { filter: 'none' }
      ], { duration: 560, easing: 'ease-out' });
      return numeric;
    }

    function enableDrag(element, kind) {
      let start = null;
      element.addEventListener('pointerdown', event => {
        if (!previewMode || element.style.display === 'none') return;
        const rect = stage.getBoundingClientRect();
        const scale = rect.width / 1920 || 1;
        start = {
          pointerX: event.clientX,
          pointerY: event.clientY,
          x: parseFloat(element.style.left) || 0,
          y: parseFloat(element.style.top) || 0,
          scale
        };
        element.dataset.dragState = 'active';
        element.setPointerCapture(event.pointerId);
        event.preventDefault();
      });
      element.addEventListener('pointermove', event => {
        if (!start || !element.hasPointerCapture(event.pointerId)) return;
        const width = parseFloat(element.style.width) || 1;
        const height = parseFloat(element.style.height) || 1;
        const x = Math.max(0, Math.min(1920 - width, start.x + (event.clientX - start.pointerX) / start.scale));
        const y = Math.max(0, Math.min(1080 - height, start.y + (event.clientY - start.pointerY) / start.scale));
        element.style.left = `${x}px`;
        element.style.top = `${y}px`;
      });
      const finish = event => {
        if (!start) return;
        if (element.hasPointerCapture(event.pointerId)) element.releasePointerCapture(event.pointerId);
        start = null;
        const x = parseFloat(element.style.left) || 0;
        const y = parseFloat(element.style.top) || 0;
        element.dataset.dragState = 'pending';
        element.dataset.dragX = String(x);
        element.dataset.dragY = String(y);
        element.dataset.dragUntil = String(Date.now() + 2000);
        window.chrome?.webview?.postMessage({
          type: 'asset-moved',
          kind,
          x,
          y
        });
      };
      element.addEventListener('pointerup', finish);
      element.addEventListener('pointercancel', finish);
    }
    enableDrag(logo, 'logo');
    enableDrag(bottomLeft, 'bottom-left');

    let lastRevision = -1;

    async function refresh() {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 2000);
      try {
        const stateEndpoint = previewMode ? '/api/preview-state' : '/api/state';
        const response = await fetch(stateEndpoint, { cache: 'no-store', signal: controller.signal });
        if (!response.ok) return;
        const state = await response.json();
        const revision = Number(state.revision || 0);
        if (revision === lastRevision) return;
        lastRevision = revision;
        const teamMode = state.layoutStyle === 'teamMode';
        const matchMode = state.layoutStyle === 'verticalMatch';
        const showCurrentScore = matchMode && state.verticalNumberMode === 'score';
        const verticalAvatar = state.layoutStyle === 'verticalAvatar' || teamMode || matchMode;
        const gapConditions = state.layoutStyle === 'gapConditions';
        stage.classList.toggle('vertical-avatar', verticalAvatar);
        stage.classList.toggle('brand-avatar', state.layoutStyle === 'verticalAvatar' || matchMode);
        stage.classList.toggle('team-mode', teamMode);
        stage.classList.toggle('gap-conditions', gapConditions);
        applyAsset(leftBoard, state.hasLeftBoard, '/asset/left-board', state.leftBoardVersion);
        leftBoard.style.objectPosition = `${Number(state.leftBoardFocusX ?? 50)}% ${Number(state.leftBoardFocusY ?? 50)}%`;
        applyAsset(logo, state.logo && state.logo.hasImage, '/asset/logo', state.logo && state.logo.version);
        applyAsset(bottomLeft, state.bottomLeft && state.bottomLeft.hasImage, '/asset/bottom-left', state.bottomLeft && state.bottomLeft.version);
        placeFromState(logo, state.logo || {});
        placeFromState(bottomLeft, state.bottomLeft || {});
        const logoBottom = Number.parseFloat(logo.style.top || '0') + Number.parseFloat(logo.style.height || '0');
        titles.style.top = `${Math.min(760, logoBottom + 18)}px`;
        department.textContent = state.department || '';
        matchText.textContent = state.matchText || '';
        renderGapPanel(state);

        for (let i = 0; i < 4; i++) {
          const player = (state.players || [])[i] || {};
          const nodes = playerNodes[i];
          nodes.root.classList.toggle('riichi', player.riichi === true);
          nodes.wind.textContent = player.wind || '?';
          nodes.wind.classList.toggle('east', player.wind === '东');
          if (matchMode && state.verticalNameMode === 'teamName')
            fitName(nodes.name, player.teamName || '暂无队伍');
          else
            fitName(nodes.name, player.nickname);
          fitTeam(nodes.team, player.teamName);
          nodes.lastScore = transitionNumber(
            nodes.score,
            nodes.scoreValue,
            nodes.lastScore,
            player.score,
            (element, value) => fitScore(nodes.score, element, value),
            () => fitScore(nodes.score, nodes.scoreValue, player.score));
          const numberKind = showCurrentScore ? 'score' : 'totalPoints';
          if (nodes.lastTotalKind !== numberKind) {
            nodes.lastTotal = null;
            nodes.lastTotalKind = numberKind;
          }
          nodes.lastTotal = transitionNumber(
            nodes.total,
            nodes.totalValue,
            nodes.lastTotal,
            showCurrentScore ? player.score : player.totalPoints,
            showCurrentScore ? (element, value) => { element.textContent = String(Math.trunc(Number(value || 0))); } : renderTotal,
            () => fitTotal(nodes.total, nodes.totalValue));
          nodes.startValue.textContent = signed(player.startPoints);
          nodes.gameValue.textContent = signed(player.gamePoints);
          nodes.pointSplit.style.display = verticalAvatar && state.showTotalPoints ? 'grid' : 'none';
          nodes.total.style.display = showCurrentScore || state.showTotalPoints ? 'block' : 'none';
          const showAvatar = player.hasAvatar && !player.transparentAvatar;
          applyAsset(nodes.avatar, showAvatar, `/asset/avatar-${i}`, player.avatarVersion);
          applyAvatarFrame(nodes.avatarFrame, nodes.avatarShell, player);
        }
      } catch {
        // 保留最后一次成功画面，网络抖动时不闪空。
      } finally {
        clearTimeout(timeout);
        setTimeout(refresh, 250);
      }
    }
    refresh();
  </script>
</body>
</html>
""";
}
