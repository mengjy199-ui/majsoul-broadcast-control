"use strict";

module.exports = function installControlBridge(context) {
  const { broadcast, liqi, observe, liveObserve, logger, electron } = context;
  installRoundResultCapture({ broadcast, observe, liveObserve, logger });
  const expectedToken = process.env.MAJSOUL_V29_CONTROL_TOKEN || "";
  const originalHandleMessage = broadcast.handleMessage.bind(broadcast);

  const sendResult = (session, requestId, ok, data, error) => {
    broadcast.send(session.ws, {
      type: "control-result",
      requestId,
      ok,
      data: data ?? null,
      error: error ?? null,
      time: Date.now()
    });
  };

  const messageOf = (error) => error instanceof Error ? error.message : String(error);

  broadcast.handleMessage = async function handleIntegratedMessage(session, text) {
    let message;
    try {
      message = JSON.parse(text);
    } catch {
      return originalHandleMessage(session, text);
    }

    if (message?.type !== "control")
      return originalHandleMessage(session, text);

    const requestId = typeof message.requestId === "string" ? message.requestId : "";
    if (!requestId) {
      sendResult(session, "", false, null, "缺少 requestId");
      return;
    }
    if (!expectedToken || message.token !== expectedToken) {
      sendResult(session, requestId, false, null, "控制通道验证失败");
      return;
    }

    const data = message.data && typeof message.data === "object" ? message.data : {};
    try {
      switch (message.command) {
        case "host.status":
          sendResult(session, requestId, true, {
            version: "0.292",
            capabilities: ["auth", "contest", "observe", "shutdown", "round-results"],
            auth: liqi.getState(),
            broadcast: broadcast.currentSnapshot()
          });
          return;
        case "auth.login": {
          const accountName = typeof data.accountName === "string" ? data.accountName.trim() : "";
          const password = typeof data.password === "string" ? data.password : "";
          if (!accountName || !password) throw new Error("请输入账号和密码");
          const account = await liqi.login(accountName, password);
          broadcast.setAuth(liqi.getState().connection, account);
          sendResult(session, requestId, true, { account });
          return;
        }
        case "auth.logout":
          liqi.logout();
          broadcast.setAuth("disconnected", null);
          sendResult(session, requestId, true, {});
          return;
        case "contest.fetch": {
          const contestId = Number.parseInt(data.contestId, 10);
          if (!Number.isInteger(contestId) || contestId <= 0) throw new Error("大会 ID 无效");
          const overview = await liqi.fetchContestLiveList(contestId);
          sendResult(session, requestId, true, { overview });
          return;
        }
        case "observe.start": {
          const gameUuid = typeof data.gameUuid === "string" ? data.gameUuid.trim() : "";
          const mode = data.mode === "realtime" ? "realtime" : "delayed";
          if (!gameUuid) throw new Error("未选择对局");
          broadcast.setObserve(null);
          broadcast.setMode(mode);
          if (mode === "realtime") {
            liveObserve.stop();
            await observe.start(gameUuid);
          } else {
            observe.stop();
            await liveObserve.start(gameUuid);
          }
          sendResult(session, requestId, true, { gameUuid, mode });
          return;
        }
        case "observe.stop":
          observe.stop();
          liveObserve.stop();
          broadcast.setMode("idle");
          sendResult(session, requestId, true, {});
          return;
        case "host.shutdown":
          observe.stop();
          liveObserve.stop();
          liqi.logout();
          sendResult(session, requestId, true, {});
          setTimeout(() => {
            try { broadcast.stop(); } catch {}
            electron.app.quit();
          }, 100);
          return;
        default:
          sendResult(session, requestId, false, null, `未知控制命令 ${message.command}`);
      }
    } catch (error) {
      logger.error(`统一控制命令失败 command=${message.command}`, error);
      sendResult(session, requestId, false, null, messageOf(error));
    }
  };
};

// ObserveTracker already decodes both Record* and Action* events before emitting
// its public action log. Capture only settlement data from that existing hook;
// never execute a second decoder or infer a result from score differences.
function installRoundResultCapture({ broadcast, observe, liveObserve, logger }) {
  const crypto = require("node:crypto");
  const games = new Map();
  const contexts = new WeakMap();
  const terminalTypes = new Map([
    ["RecordHule", "hule"], ["ActionHule", "hule"],
    ["RecordNoTile", "noTile"], ["ActionNoTile", "noTile"],
    ["RecordLiuJu", "liuJu"], ["ActionLiuJu", "liuJu"]
  ]);
  const number = (value) => typeof value === "number" && Number.isFinite(value) ? value : null;
  const seatNumber = (value) => Number.isInteger(value) && value >= 0 && value <= 3 ? value : null;
  const protoSeat = (object) => seatNumber(object?.seat === undefined ? 0 : object.seat);
  const strings = (value) => Array.isArray(value) ? value.filter((item) => typeof item === "string") : [];
  const numbers = (value) => Array.isArray(value) && value.every((item) => number(item) !== null) ? value.slice() : [];
  const publicCopy = (value) => value === undefined ? null : JSON.parse(JSON.stringify(value));
  const pick = (object, fields) => Object.fromEntries(fields.filter((key) => object?.[key] !== undefined).map((key) => [key, publicCopy(object[key])]));
  const validSanmaFinal = (scores) => scores.length === 3 &&
    scores.every(score => Number.isInteger(score) && score % 100 === 0) &&
    scores.reduce((sum, score) => sum + score, 0) === 105000;

  function contextFor(tracker) {
    let state = contexts.get(tracker);
    if (!state || state.gameUuid !== tracker.gameUuid) {
      state = { gameUuid: tracker.gameUuid, round: null, lastSource: null };
      contexts.set(tracker, state);
    }
    return state;
  }

  function gameFor(uuid) {
    if (!games.has(uuid)) games.set(uuid, { results: new Map() });
    return games.get(uuid);
  }

  function publicSettlement(type, data) {
    if (type === "hule") {
      return pick(data, ["hules", "old_scores", "delta_scores", "scores", "gameend", "doras"]);
    }
    if (type === "noTile") {
      const result = pick(data, ["liujumanguan", "gameend"]);
      // Only tenpai hands are publicly revealed at an exhaustive draw.
      result.players = (Array.isArray(data.players) ? data.players : []).map((player) => ({
        ...pick(player, ["tingpai", "already_hule"]),
        ...(player.tingpai === true ? pick(player, ["hand", "tings"]) : {})
      }));
      result.scores = (Array.isArray(data.scores) ? data.scores : []).map((score) =>
        pick(score, ["seat", "old_scores", "delta_scores", "score", "taxes", "lines"]));
      if (Array.isArray(data.hules_history)) result.hules_history = publicCopy(data.hules_history);
      return result;
    }
    // Do not include allplayertiles: abortive draws do not necessarily reveal
    // every opponent's concealed hand. Keep the announcing player's reveal.
    return pick(data, ["type", "seat", "tiles", "gameend", "hules_history"]);
  }

  function winnerFrom(hule, state) {
    const seat = protoSeat(hule);
    const isTsumo = hule.zimo === true;
    const last = state.lastSource;
    const knownSource = !isTsumo && last && last.seat !== seat &&
      (!hule.hu_tile || !last.tile || hule.hu_tile === last.tile);
    const isYakuman = hule.yiman === true;
    return {
      seat,
      fromSeat: knownSource ? last.seat : null,
      fromSeatSource: knownSource ? last.kind : null,
      isTsumo,
      tiles: strings(hule.hand),
      melds: strings(hule.ming),
      winTile: typeof hule.hu_tile === "string" ? hule.hu_tile : null,
      fans: (Array.isArray(hule.fans) ? hule.fans : []).map((fan) => ({
        id: number(fan.id),
        name: typeof fan.name === "string" && fan.name.trim() ? fan.name : null,
        value: number(fan.val)
      })),
      han: isYakuman ? null : number(hule.count),
      fu: number(hule.fu),
      isYakuman,
      yakumanCount: isYakuman ? number(hule.count) : null,
      isDealer: hule.qinjia === true,
      points: number(hule.point_sum),
      pointRon: number(hule.point_rong),
      pointTsumoDealer: number(hule.point_zimo_qin),
      pointTsumoNonDealer: number(hule.point_zimo_xian),
      doras: strings(hule.doras),
      uraDoras: strings(hule.li_doras)
    };
  }

  function capture(tracker, actionName, data) {
    const state = contextFor(tracker);
    if (actionName === "NotifyGameEndResult" && tracker.gameUuid) {
      const ranking = data.result?.players;
      if (Array.isArray(ranking) && ranking.length === 3 &&
          ranking.map(protoSeat).sort().join(",") === "0,1,2") {
        const game = gameFor(tracker.gameUuid);
        game.playerCount = 3;
        game.sanmaFinalScores = ranking.slice().sort((left, right) => protoSeat(left) - protoSeat(right))
          .map(player => player.total_point ?? 0);
      }
      return;
    }
    if (actionName === "RecordNewRound" || actionName === "ActionNewRound") {
      const scores = numbers(data.scores);
      if (tracker.gameUuid && (scores.length === 3 || scores.length === 4))
        gameFor(tracker.gameUuid).playerCount = scores.length;
      // Protobuf omits scalar zero values in the current decoder.
      state.round = {
        chang: number(data.chang === undefined ? 0 : data.chang),
        ju: number(data.ju === undefined ? 0 : data.ju),
        ben: number(data.ben === undefined ? 0 : data.ben)
      };
      state.lastSource = null;
      return;
    }
    if (actionName === "RecordDiscardTile" || actionName === "ActionDiscardTile") {
      const seat = protoSeat(data);
      state.lastSource = seat === null ? null : { seat, tile: typeof data.tile === "string" ? data.tile : null, kind: "last-discard" };
      return;
    }
    if (actionName === "RecordAnGangAddGang" || actionName === "ActionAnGangAddGang") {
      const seat = protoSeat(data);
      state.lastSource = seat === null ? null : { seat, tile: typeof data.tiles === "string" ? data.tiles : null, kind: "last-kan" };
      return;
    }
    const type = terminalTypes.get(actionName);
    const uuid = typeof tracker.gameUuid === "string" ? tracker.gameUuid : "";
    if (!type || !uuid) return;
    const raw = publicSettlement(type, data);
    const digest = crypto.createHash("sha256").update(JSON.stringify(raw)).digest("hex").slice(0, 24);
    const roundKnown = state.round && state.round.chang !== null && state.round.ju !== null && state.round.ben !== null;
    const key = roundKnown
      ? `${uuid}:${state.round.chang}:${state.round.ju}:${state.round.ben}`
      : `${uuid}:unknown:${digest}`;
    const game = gameFor(uuid);
    const isFinal = data.gameend === true ||
      (data.gameend && typeof data.gameend === "object" && Object.keys(data.gameend).length > 0);
    if (isFinal && game.playerCount === 3) {
      const draw = type === "noTile" && raw.scores.find(group =>
        group.old_scores?.length === 3 && group.delta_scores?.length === 3);
      const drawScores = draw ? draw.old_scores.map((score, seat) => score + draw.delta_scores[seat]) : [];
      game.sanmaFinalScores = [numbers(data.gameend?.scores), numbers(data.scores), drawScores]
        .find(validSanmaFinal) || [];
    }
    const previous = game.results.get(key);
    const winners = type === "hule"
      ? (Array.isArray(data.hules) ? data.hules : []).map((hule) => winnerFrom(hule, state)).filter((winner) => winner.seat !== null)
      : [];
    // A single Hule message can contain multiple winners. Replayed settlement
    // messages update their original entry and preserve any earlier winners.
    const mergedWinners = new Map((previous?.winners || []).map((winner) => [winner.seat, winner]));
    for (const winner of winners) {
      const known = mergedWinners.get(winner.seat);
      if (known) {
        // Partial history replays must not erase previously observed details.
        for (const field of [
          "fromSeat", "fromSeatSource", "winTile", "fans", "tiles", "melds",
          "han", "fu", "yakumanCount", "points", "pointRon",
          "pointTsumoDealer", "pointTsumoNonDealer", "doras", "uraDoras"
        ]) {
          const value = winner[field];
          if ((value === null || value === "" || (Array.isArray(value) && value.length === 0)) && known[field] != null)
            winner[field] = known[field];
        }
      }
      mergedWinners.set(winner.seat, winner);
    }
    const publicGroups = type === "noTile" ? raw.scores : [];
    const directDeltas = numbers(data.delta_scores);
    const deltaScores = directDeltas.length ? directDeltas
      : publicGroups.length === 1 ? numbers(publicGroups[0].delta_scores) : [];
    const directScores = numbers(data.scores);
    game.results.set(key, {
      key, gameUuid: uuid,
      chang: roundKnown ? state.round.chang : null,
      ju: roundKnown ? state.round.ju : null,
      ben: roundKnown ? state.round.ben : null,
      roundKnown: !!roundKnown,
      type, actionName,
      source: "decoded-observe-action",
      timestamp: previous?.timestamp ?? Date.now(),
      players: (Array.isArray(tracker.players) ? tracker.players : []).map((player) => ({
        seat: seatNumber(player.seat),
        nickname: typeof player.nickname === "string" ? player.nickname : ""
      })),
      winners: [...mergedWinners.values()],
      deltaScores,
      scores: directScores.length ? directScores : numbers(data.gameend?.scores),
      scoreGroups: publicGroups,
      drawType: type === "liuJu" ? number(data.type === undefined ? 0 : data.type) : null,
      nagashiMangan: type === "noTile" && data.liujumanguan === true,
      rawScope: "public-settlement-fields",
      raw
    });
  }

  for (const tracker of [observe, liveObserve]) {
    const reset = tracker.resetState;
    tracker.resetState = function (...args) {
      contexts.delete(this);
      return reset.apply(this, args);
    };
    tracker.onLog((line) => {
      const match = typeof line === "string" && line.match(/\] action ([A-Za-z0-9_.]+) seat=[^ ]+ data=([\s\S]*)$/);
      if (!match) return;
      try {
        capture(tracker, match[1], JSON.parse(match[2]));
      } catch {
        logger.error("逐局结算记录解析失败；原始观战状态继续更新");
      }
    });
  }

  const setObserve = broadcast.setObserve;
  broadcast.setObserve = function (state) {
    if (state && typeof state.gameUuid === "string") {
      const game = games.get(state.gameUuid);
      if (game?.playerCount === 3 && Array.isArray(state.players)) {
        const players = state.players.filter((player) => player.seat < 3).map((player) => ({
          ...player,
          wind: player.wind < 0 ? -1 : (player.seat - state.round.ju + 3) % 3
        }));
        const finalScores = state.ended ? game.sanmaFinalScores : undefined;
        const finalRanking = finalScores === undefined
          ? (state.finalRanking || []).filter((player) => player.seat < 3)
          : validSanmaFinal(finalScores)
            ? players.map(player => ({ seat: player.seat, accountId: player.accountId, nickname: player.nickname,
                totalPoint: finalScores[player.seat] })).sort((left, right) => right.totalPoint - left.totalPoint)
            : [];
        state = {
          ...state,
          players,
          finalRanking: finalRanking.map((player, index) => ({ ...player, rank: index + 1 }))
        };
      }
      state = {
        ...state,
        roundResults: game ? [...game.results.values()] : [],
        roundResultSchema: 1
      };
    }
    return setObserve.call(this, state);
  };
}
