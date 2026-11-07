'use strict';
const connect = byId('connect');
const notice = byId('connection-status');
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const buffers = new Map();
let context, cursor = 0, playing = false;
const pending = [];
const connectionPanel = byId('connection-panel');
const connectionLink = byId('connection-link');
const copyStatus = byId('copy-status');
const browserGuide = byId('browser-guide');
const embeddedContinue = byId('use-embedded');
connectionLink.textContent = location.href;
connectionLink.href = location.href;
const userAgent = navigator.userAgent;
const embeddedClient = /MicroMessenger/i.test(userAgent) ? '微信' :
  /\bQQ\/\d/i.test(userAgent) ? 'QQ' : '';
if (embeddedClient) {
  byId('waiting').textContent = '请在浏览器中打开';
  browserGuide.hidden = false;
  browserGuide.textContent = '点击' + embeddedClient + '右上角菜单，选择在浏览器中打开。也可复制下方链接，粘贴到' +
    (/iPhone|iPad|iPod/i.test(userAgent) ? ' Safari' : '手机浏览器') + '。';
  connect.hidden = true;
  embeddedContinue.hidden = false;
}
embeddedContinue.addEventListener('click', () => {
  embeddedContinue.hidden = true;
  connect.hidden = false;
});
byId('copy-link').addEventListener('click', async () => {
  let copied = false;
  try {
    await navigator.clipboard.writeText(connectionLink.href);
    copied = true;
  } catch {
    // LAN HTTP and some embedded browsers require the older, gesture-driven API.
    const field = document.createElement('textarea');
    field.value = connectionLink.href;
    field.readOnly = true;
    field.style.cssText = 'position:fixed;top:0;left:0;opacity:0;font-size:16px';
    document.body.append(field);
    field.focus();
    field.select();
    field.setSelectionRange(0, field.value.length);
    try { copied = document.execCommand('copy'); } catch {}
    field.remove();
  }
  copyStatus.textContent = copied ? '链接已复制' : '请长按上方链接复制';
});

async function get(path) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 8000);
  let reply;
  try { reply = await fetch(path, {cache:'no-store', signal: controller.signal}); }
  finally { clearTimeout(timeout); }
  if (!reply.ok) throw Error(path + ' (' + reply.status + ')');
  return reply;
}

function display(e) {
  byId('waiting').hidden = true;
  stage.hidden = false;
  byId('platform').value = e.platform;
  byId('live-sender').textContent = e.name;
  const wx = e.platform === 'wechat';
  const amount = (wx ? '¥' : '+') + e.amount.toLocaleString('en-US', {
    useGrouping: !wx, minimumFractionDigits: 2, maximumFractionDigits: 2
  });
  // The game emits one receipt time. There is no separate human confirmation step.
  const rows = wx ? [['收款时间', e.time]] :
    [['创建时间', e.time], ['收款方式','账户余额'], ['对方账户',e.name]];
  present({name:e.name, portrait:e.portrait, amount, rows});
}

async function drain() {
  if (playing) return;
  playing = true;
  try {
    while (pending.length) {
      if (context.state !== 'running') {
        connectionPanel.hidden = false;
        connect.hidden = false;
        notice.textContent = '点按恢复语音';
        break;
      }
      const e = pending.shift();
      display(e);
      const gain = context.createGain();
      gain.gain.value = e.volume;
      gain.connect(context.destination);
      let start = context.currentTime + 0.03, end = start;
      try {
        for (const token of e.sounds) {
          const source = context.createBufferSource();
          source.buffer = buffers.get(token);
          if (!source.buffer) throw Error('缺少语音 ' + token);
          source.playbackRate.value = e.speed;
          source.connect(gain);
          source.start(start);
          end = start + source.buffer.duration / e.speed;
          start = Math.max(start + 0.06, end + e.gap);
        }
        await sleep(Math.max(1720, (end - context.currentTime) * 1000 + 60));
      } finally { gain.disconnect(); }
    }
  } catch (err) { notice.textContent = '语音播放失败：' + err.message; }
  finally { playing = false; }
}

async function poll() {
  while (true) {
    try {
      const data = await (await get('events?after=' + cursor)).json();
      pending.push(...data.items);
      cursor = data.cursor;
      if (context.state === 'running') notice.textContent = stage.hidden ? '已连接，等待游戏收款' : '';
      drain();
      await sleep(300);
    } catch { notice.textContent = '连接中断，正在重连'; await sleep(1000); }
  }
}
let started = false;
connect.addEventListener('click', async () => {
  connect.disabled = true;
  try {
    context ||= new (window.AudioContext || window.webkitAudioContext)();
    await context.resume();
    if (!started) {
      cursor = await (await get('cursor')).json();
      notice.textContent = '正在载入到账语音';
      const clips = await (await get('clips')).json();
      await Promise.all(clips.map(async token => {
        const data = await (await get('sounds/' + encodeURIComponent(token) + '.mp3')).arrayBuffer();
        buffers.set(token, await context.decodeAudioData(data));
      }));
      started = true;
      poll();
    }
    connect.hidden = true;
    connectionPanel.hidden = true;
    drain();
  } catch (err) {
    notice.textContent = '连接失败：' + err.message;
  } finally { connect.disabled = false; }
});
