const examples = {
  alipay: {
    avatar: 'engineer', name: '工程师', amount: '+5,000.00',
    rows: [
      ['创建时间', '2026-10-07 19:57:33'], ['收款方式', '账户余额'],
      ['转账备注', '转账'], ['对方账户', '工程师'], ['订单号', 'PS202610071957330000000000000001']
    ]
  },
  wechat: {
    avatar: 'scavenger', name: '转账-来自拾荒者', amount: '¥2000.00',
    rows: [
      ['转账时间', '2026年06月21日 16:08:43'], ['收款时间', '2026年06月21日 17:12:18']
    ]
  }
};
const byId = id => document.getElementById(id);
const receipt = byId('receipt');
const systemDark = matchMedia('(prefers-color-scheme: dark)');
const reducedMotion = matchMedia('(prefers-reduced-motion: reduce)');
const stage = receipt.closest('.stage');
const introCopy = {
  alipay: { platform: '支付宝到账', person: '支付宝收款', money: '支付宝到账' },
  wechat: { platform: '微信支付', person: '微信转账', money: '已存入零钱' }
};
let revealTimer;
let entrance = [];

function applyTheme() {
  const choice = byId('theme').value;
  document.documentElement.dataset.scheme = choice === 'auto'
    ? (systemDark.matches ? 'dark' : 'light') : choice;
}

function showDetails() {
  clearTimeout(revealTimer);
  if (stage.dataset.phase !== 'intro') return;
  const { platform, variant } = stage.dataset;
  if (variant === 'money' && !reducedMotion.matches) {
    const amount = byId('amount');
    const first = byId('first-amount');
    const start = first.getBoundingClientRect();
    const end = amount.getBoundingClientRect();
    const dx = start.left + start.width / 2 - end.left - end.width / 2;
    const dy = start.top + start.height / 2 - end.top - end.height / 2;
    stage.dataset.phase = 'reveal';
    const travel = amount.animate([
      { transform: `translate(${dx}px, ${dy}px) scale(${start.height / end.height})`, color: getComputedStyle(first).color },
      { transform: 'none', color: getComputedStyle(amount).color }
    ], { duration: 480, easing: 'cubic-bezier(.22,1,.36,1)' });
    const dissolve = byId('intro').animate([{ opacity: 1 }, { opacity: 0 }],
      { duration: 480, easing: 'ease-in-out', fill: 'forwards' });
    entrance.push(travel, dissolve);
    dissolve.onfinish = () => {
      stage.dataset.phase = 'details';
      dissolve.cancel();
    };
    return;
  }
  stage.dataset.phase = 'details';
  if (reducedMotion.matches) return;
  const elements = variant === 'platform' || platform === 'wechat' ? [receipt] : variant === 'person'
    ? [byId('amount'), byId('status'), byId('details')]
    : [byId('avatar'), byId('payer'), byId('status'), byId('details')];
  // Alipay lifts the receipt into view. WeChat keeps the header still and reveals the ledger.
  const offset = platform === 'alipay' ? 'translateY(6px)' : 'none';
  entrance = elements.filter(el => !el.hidden).map(el => el.animate(
    [{ opacity: 0, transform: offset }, { opacity: 1, transform: 'none' }],
    { duration: 420, easing: 'ease-out' }
  ));
}

function replay() {
  clearTimeout(revealTimer);
  entrance.forEach(a => { a.onfinish = null; a.cancel(); });
  entrance = [];
  const platform = byId('platform').value;
  const variant = document.querySelector('[name="variant"]:checked').value;
  Object.assign(stage.dataset, { platform, variant, phase: 'intro' });
  stage.querySelector('.intro-label').textContent = introCopy[platform][variant];
  if (variant === 'money') {
    byId('slip-label').textContent = platform === 'alipay' ? '支付宝到账' : '微信转账';
    const first = byId('first-amount');
    const alipay = platform === 'alipay';
    if (!alipay) byId('sender-avatar').src = byId('avatar').src;
    first.textContent = byId('amount').textContent;
    const anchor = alipay ? 'translate(-50%, -50%) ' : '';
    const restScale = alipay ? 1.08 : 1;
    first.style.transform = anchor + `scale(${restScale})`;
    if (alipay) positionAlipaySlip();
    if (!reducedMotion.matches) {
      entrance.push(first.animate([
        { transform: anchor + `scale(${restScale * 1.035})`, opacity: .65 },
        { transform: anchor + `scale(${restScale})`, opacity: 1 }
      ], { duration: 250, easing: 'ease-out' }));
    }
  }
  if (!byId('hold').checked) revealTimer = setTimeout(showDetails, 1200);
}

function positionAlipaySlip() {
  if (stage.dataset.platform !== 'alipay' || stage.dataset.phase !== 'intro' || stage.dataset.variant !== 'money') return;
  const end = byId('amount').getBoundingClientRect();
  const offset = end.top - stage.getBoundingClientRect().top + end.height / 2 + 32;
  stage.style.setProperty('--alipay-start-y', offset + 'px');
}
new ResizeObserver(positionAlipaySlip).observe(stage);

function chevron() {
  const mark = document.createElement('span');
  mark.className = 'chevron';
  mark.setAttribute('aria-hidden', 'true');
  return mark;
}

function present(transaction) {
  const platform = byId('platform').value;
  const sample = transaction || examples[platform];
  receipt.className = platform;
  byId('avatar').src = transaction ? sample.portrait || 'assets/unknown.svg' : 'assets/' + sample.avatar + '.png';
  byId('payer').textContent = sample.name;
  if (platform === 'alipay') byId('payer').append(chevron());
  byId('amount').textContent = sample.amount;
  if (platform === 'wechat') {
    const currency = document.createElement('span');
    currency.className = 'currency';
    currency.textContent = sample.amount[0];
    byId('amount').replaceChildren(currency, sample.amount.slice(1));
  }
  byId('status').hidden = platform !== 'alipay';
  byId('details').replaceChildren(...sample.rows.flatMap(([label, value]) => {
    const term = document.createElement('dt');
    const description = document.createElement('dd');
    term.textContent = label;
    description.textContent = value;
    if (label === '收款方式') description.append(chevron());
    return [term, description];
  }));
  replay();
}
byId('platform').addEventListener('change', () => present());
document.querySelector('.wechat-back').addEventListener('click', () => {
  byId('hold').checked = true;
  replay();
});
byId('theme').addEventListener('change', applyTheme);
byId('replay').addEventListener('click', replay);
byId('variants').addEventListener('change', replay);
byId('hold').addEventListener('change', () => {
  if (byId('hold').checked) replay();
  else showDetails();
});
systemDark.addEventListener('change', applyTheme);
const previewOptions = new URLSearchParams(location.search);
if (previewOptions.get('platform') === 'wechat') byId('platform').value = 'wechat';
byId('hold').checked = previewOptions.get('hold') === '1';
applyTheme();
if (!document.documentElement.hasAttribute('data-live')) present();
