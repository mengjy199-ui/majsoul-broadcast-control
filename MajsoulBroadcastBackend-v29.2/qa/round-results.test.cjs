const { test } = require('node:test');
const assert = require('node:assert/strict');
const install = require('../control-bridge.cjs');

function setup() {
  function tracker() {
    const listeners = [];
    return {
      gameUuid: 'audit-game', players: [0, 1, 2, 3].map(seat => ({ seat, nickname: `测试${seat + 1}` })),
      resetState() {}, onLog(callback) { listeners.push(callback); },
      action(name, data) { for (const listener of listeners) listener(`[audit] action ${name} seat=0 data=${JSON.stringify(data)}`); }
    };
  }
  const observe = tracker(), liveObserve = tracker();
  const broadcast = { handleMessage() {}, send() {}, setObserve(state) { this.state = state; } };
  install({ broadcast, observe, liveObserve, liqi: {}, logger: { error() {} }, electron: {} });
  const read = () => { broadcast.setObserve({gameUuid:'audit-game'}); return broadcast.state.roundResults; };
  return { observe, liveObserve, read, broadcast };
}

test('three-player snapshots remove the padded seat and use three seat winds', () => {
  const { observe, broadcast } = setup();
  observe.action('RecordNewRound', { chang: 1, ju: 2, scores: [30000, 41000, 34000] });
  const players = [30000, 41000, 34000, 0].map((score, seat) => ({ seat, score, wind: 0 }));
  broadcast.setObserve({
    gameUuid: 'audit-game',
    round: { ju: 2 },
    players,
    ended: true,
    finalRanking: [players[1], players[2], players[0], players[3]]
  });
  assert.deepEqual(broadcast.state.players.map(player => player.wind), [1, 2, 0]);
  assert.deepEqual(broadcast.state.finalRanking.map(player => [player.seat, player.rank]), [[1, 1], [2, 2], [0, 3]]);
});

test('three-player terminal draw uses public deltas instead of stale tracker scores', () => {
  const { observe, broadcast } = setup();
  observe.action('RecordNewRound', { chang: 1, ju: 2, scores: [30000, 41000, 34000] });
  observe.action('RecordNoTile', {
    gameend: true, scores: [{ old_scores: [30000, 41000, 34000], delta_scores: [2000, -1000, -1000] }]
  });
  const players = [30000, 41000, 34000, 0].map((score, seat) => ({ seat, score, wind: 0 }));
  broadcast.setObserve({ gameUuid: 'audit-game', round: { ju: 2 }, players, ended: true, finalRanking: [] });
  assert.deepEqual(broadcast.state.finalRanking.map(player => player.totalPoint), [40000, 33000, 32000]);
});

test('multiple winners and replay keep a single round with known payer', () => {
  const { observe, read } = setup();
  observe.action('RecordNewRound', {chang:0,ju:0,ben:0});
  observe.action('RecordDiscardTile', {seat:3,tile:'3m'});
  observe.action('RecordHule', {hules:[{seat:0,hu_tile:'3m',point_sum:8000,fans:[{id:1,val:1}]},{seat:1,hu_tile:'3m',point_sum:3900}],scores:[33000,28900,25000,13100]});
  const first = read()[0];
  assert.equal(first.winners.length,2);
  assert.equal(first.winners[0].fromSeat,3);
  observe.resetState();
  observe.action('RecordNewRound', {chang:0,ju:0,ben:0});
  observe.action('RecordHule', {hules:[{seat:0,hu_tile:'3m'}]});
  const result = read();
  assert.equal(result.length,1);
  assert.equal(result[0].winners.length,2);
  assert.equal(result[0].winners[0].fromSeat,3);
  assert.equal(result[0].winners[0].points,8000);
  assert.equal(result[0].timestamp,first.timestamp);
});

test('missing round and payer stay unknown instead of becoming east one', () => {
  const { observe, read } = setup();
  observe.action('RecordHule', {hules:[{seat:1,hu_tile:'3m'}]});
  const result = read()[0];
  assert.equal(result.chang,null);
  assert.equal(result.roundKnown,false);
  assert.equal(result.winners[0].fromSeat,null);
  assert.match(result.key,/:unknown:/);
});

test('exhaustive draw retains only publicly revealed hands', () => {
  const { liveObserve, read } = setup();
  liveObserve.action('RecordNewRound', {});
  liveObserve.action('RecordNoTile', {players:[{tingpai:false,hand:['private-unrevealed-hand']},{tingpai:true,hand:['1m']}],scores:[]});
  const result = read()[0];
  assert.equal(result.type,'noTile');
  assert.equal(result.raw.players[0].hand,undefined);
  assert.deepEqual(result.raw.players[1].hand,['1m']);
  assert.doesNotMatch(JSON.stringify(result),/private-unrevealed-hand/);
});

test('yakuman uses its own count and preserves exact points', () => {
  const { observe, read } = setup();
  observe.action('RecordNewRound', {});
  observe.action('RecordHule', {hules:[{seat:0,zimo:true,yiman:true,count:2,fu:0,point_sum:96000,point_zimo_xian:32000}]});
  const winner=read()[0].winners[0];
  assert.equal(winner.han,null);
  assert.equal(winner.yakumanCount,2);
  assert.equal(winner.fromSeat,null);
  assert.equal(winner.points,96000);
});
