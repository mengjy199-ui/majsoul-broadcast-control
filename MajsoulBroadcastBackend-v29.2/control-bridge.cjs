"use strict";

module.exports = function installControlBridge(context) {
  const { broadcast, liqi, observe, liveObserve, logger, electron } = context;
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
            version: "29.2",
            capabilities: ["auth", "contest", "observe", "shutdown"],
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
