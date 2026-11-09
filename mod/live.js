'use strict';
const connect = byId('connect');
const notice = byId('connection-status');
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const buffers = new Map();
let availableClips = new Set();
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

async function loadClip(token) {
  if (!availableClips.has(token)) throw Error('前置模组缺少语音 ' + token);
  if (!buffers.has(token)) {
    const loading = get('sounds/' + encodeURIComponent(token) + '.mp3')
      .then(reply => reply.arrayBuffer()).then(bytes => context.decodeAudioData(bytes));
    buffers.set(token, loading);
    loading.catch(() => buffers.delete(token));
  }
  return buffers.get(token);
}

function isChime(token) {
  return ['diaoluo_da', 'diaoluo_xiao', 'wx_chime', 'wx_cashrecivedrevised'].includes(token);
}

// Match the prerequisite's 24 kHz mono voice preparation. Chimes stay separate
// and keep their original speed; only the stitched speech uses the speech rate.
function stitchVoice(clips, gapSeconds) {
  const rate = 24000;
  const parts = clips.map((clip, index) => {
    const channels = Array.from({length: clip.numberOfChannels}, (_, n) => clip.getChannelData(n));
    const length = Math.max(1, Math.round(clip.length * rate / clip.sampleRate));
    const samples = new Float32Array(length);
    const scale = (clip.length - 1) / Math.max(1, length - 1);
    for (let i = 0; i < length; i++) {
      const position = i * scale, left = Math.floor(position);
      const right = Math.min(clip.length - 1, left + 1), fraction = position - left;
      for (const channel of channels)
        samples[i] += (channel[left] * (1 - fraction) + channel[right] * fraction) / channels.length;
    }
    let first = 0, last = samples.length - 1;
    while (first < samples.length && Math.abs(samples[first]) < 0.0045) first++;
    while (last > first && Math.abs(samples[last]) < 0.0045) last--;
    const begin = Math.max(0, first - (index === 0 ? 288 : 144));
    const end = Math.min(samples.length, last + 1 + (index === clips.length - 1 ? 840 : 240));
    return samples.subarray(begin, Math.max(begin + 1, end));
  });
  const gap = Math.round(Math.max(-0.045, Math.min(0.12, gapSeconds + 0.035)) * rate);
  const mixed = new Float32Array(Math.max(256,
    parts.reduce((size, part) => size + part.length + Math.max(0, gap), 0)));
  let used = 0;
  parts.forEach((part, index) => {
    if (index === 0) {
      mixed.set(part);
      used = part.length;
    } else if (gap < 0) {
      const overlap = Math.min(-gap, Math.floor(used / 3), Math.floor(part.length / 3));
      for (let i = 0; i < overlap; i++) {
        const fraction = (i + 1) / (overlap + 1);
        mixed[used - overlap + i] = mixed[used - overlap + i] * (1 - fraction) + part[i] * fraction;
      }
      mixed.set(part.subarray(overlap), used);
      used += part.length - overlap;
    } else {
      used += gap;
      mixed.set(part, used);
      const fade = Math.min(96, used, part.length);
      for (let i = 0; i < fade; i++) mixed[used + i] *= i / fade;
      used += part.length;
    }
  });
  const result = context.createBuffer(1, Math.max(1, used), rate);
  result.getChannelData(0).set(mixed.subarray(0, used));
  return result;
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
      const sources = [];
      try {
        const clips = await Promise.all(e.sounds.map(loadClip));
        let start = context.currentTime + 0.03, end = start;
        let offset = 0;
        if (isChime(e.sounds[0])) {
          const source = context.createBufferSource();
          source.buffer = clips[0];
          source.connect(gain);
          source.start(start);
          sources.push(source);
          end = start + source.buffer.duration;
          start += Math.max(0.25, source.buffer.duration * 0.88);
          offset = 1;
        }
        if (offset < clips.length) {
          const source = context.createBufferSource();
          source.buffer = stitchVoice(clips.slice(offset), e.gap);
          source.playbackRate.value = e.speed;
          source.connect(gain);
          source.start(start);
          sources.push(source);
          end = Math.max(end, start + source.buffer.duration / e.speed);
        }
        await sleep(Math.max(1720, (end - context.currentTime) * 1000 + 60));
      } finally {
        for (const source of sources) { source.stop(); source.disconnect(); }
        gain.disconnect();
      }
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
      notice.textContent = '正在连接';
      const clips = await (await get('clips')).json();
      availableClips = new Set(clips);
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
