const API        = (window.BRADFORD_API || 'http://localhost:5000') + '/api/chat';
const STREAM_API = (window.BRADFORD_API || 'http://localhost:5000') + '/api/chat/stream';
const SID  = 'bca-' + Math.random().toString(36).slice(2,10);
let busy                  = false;
let chatEnabled           = true;
let settingsOpen          = false;
let _chipsDisplay         = '';
let sessionProfileInjected = false;

/* Person icon SVGs — matches React AssistantAvatar / UserAvatar */
const ALEX_ICON = `<svg viewBox="0 0 24 24" fill="none" stroke="#0f4ca3" stroke-width="1.8" width="18" height="18"><circle cx="12" cy="8" r="3.2"/><path d="M5.5 19c1.4-3 4-4.5 6.5-4.5S17 16 18.5 19"/></svg>`;
const USER_ICON = `<svg viewBox="0 0 24 24" fill="none" stroke="#fff"   stroke-width="1.8" width="18" height="18"><circle cx="12" cy="8" r="3.2"/><path d="M5.5 19c1.4-3 4-4.5 6.5-4.5S17 16 18.5 19"/></svg>`;

/* ── Open / close ── */
function chatOpen() {
  document.getElementById('chat-modal').classList.add('show');
  document.getElementById('backdrop').classList.add('show');
  document.getElementById('fab').classList.add('hide');
  document.body.style.overflow = 'hidden';
  scrollEnd();
  setTimeout(() => document.getElementById('chat-input')?.focus(), 320);
}
function chatClose() {
  if (settingsOpen) closeSettings();
  document.getElementById('chat-modal').classList.remove('show');
  document.getElementById('backdrop').classList.remove('show');
  document.getElementById('fab').classList.remove('hide');
  document.body.style.overflow = '';
  document.getElementById('fab')?.focus();
}

/* ── Restart — reset to initial state ── */
function chatClear() {
  if (settingsOpen) closeSettings();
  sessionProfileInjected = false;
  chatEnabled = true;
  const msgs = document.getElementById('chat-msgs');
  msgs.innerHTML = dateChip()
    + introBubble(
        'Hello, welcome to Bradford Council Assistant.',
        'I can help you find the right council service, explain the next steps, and guide you to the correct contact or online page.'
      )
    + introBubble(
        'What would you like help with today?',
        'Choose one of the service areas below, or type your question.'
      );
  document.getElementById('chat-chips').style.display = '';
  const inp = document.getElementById('chat-input');
  inp.disabled = false;
  inp.value = '';
  inp.style.height = 'auto';
  inp.placeholder = 'Ask me anything about Bradford Council…';
  setSendLoading(false);
  document.getElementById('chat-send').disabled = true;
  busy = false;
  scrollEnd();
}

/* ── Enable input ── */
function enableInput() {
  chatEnabled = true;
  const inp = document.getElementById('chat-input');
  inp.disabled = false;
  inp.placeholder = 'Ask me anything about Bradford Council…';
  inp.focus();
}

/* ── Input handlers ── */
function onKey(e) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    if (!busy && chatEnabled) doSend();
  }
}
function onType(el) {
  el.style.height = 'auto';
  el.style.height = Math.min(el.scrollHeight, 130) + 'px';
  if (!busy) document.getElementById('chat-send').disabled = !el.value.trim();
}

/* ── Chip click — just sends the example question, chips stay visible ── */
function usechip(btn) {
  doSend(btn.textContent.trim());
}

/* ── Address selection ── */
function selectAddress(addressText, postcode) {
  doSend(`My address is: ${addressText}, ${postcode}`);
}

/* ── Send ── */
// displayText: what to show in the bubble (optional — defaults to text)
// text: what to actually send to the API
async function doSend(override, displayText) {
  if (busy) return;
  const el   = document.getElementById('chat-input');
  const text = (override || el.value).trim();
  if (!text) return;

  chatEnabled = true;
  el.value = ''; el.style.height = 'auto';
  document.getElementById('chat-chips').style.display = 'none';

  setSendLoading(true);
  addUserMsg(displayText || text);
  saveToCurrentSession('user', displayText || text);
  showDots();
  busy = true;

  try {
    const r = await fetch(STREAM_API, {
      method : 'POST',
      headers: { 'Content-Type': 'application/json' },
      body   : JSON.stringify({ sessionId: SID, message: buildApiText(text) })
    });
    if (!r.ok) throw new Error(r.status);

    let fullText = '', structured = null, buffer = '';

    const reader  = r.body.getReader();
    const decoder = new TextDecoder();

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop();
      for (const line of lines) {
        if (!line.startsWith('data: ')) continue;
        const data = line.slice(6);
        if (data === '[DONE]') break;
        if (data.startsWith('[STRUCTURED]')) {
          try { structured = JSON.parse(data.slice(12)); } catch {}
        } else {
          fullText += data.replace(/\\n/g, '\n');
        }
      }
    }

    hideDots();
    const streamDiv = createStreamBubble();
    finaliseStreamBubble(streamDiv, fullText,
      structured?.addresses, structured?.binDates,
      structured?.libraries, structured?.councilTaxInfo,
      structured?.councilTaxProperties,
      structured?.schools, structured?.schoolDetails);

  } catch (err) {
    hideDots();
    addAlexMsg("Sorry, I'm having trouble connecting. Please call Bradford Council on **01274 431000**.", [], null, null, null, null, null);
    console.error(err);
  }
  busy = false;
  setSendLoading(false);
  el.focus();
}

/* Show spinner on send button while waiting */
function setSendLoading(on) {
  const btn = document.getElementById('chat-send');
  btn.innerHTML = on
    ? `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" width="18" height="18" style="animation:spin .8s linear infinite"><path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"/></svg>`
    : `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" width="19" height="19"><path d="M22 2L11 13"/><path d="M22 2L15 22 11 13 2 9l20-7z"/></svg>`;
  btn.disabled = on;
}

/* ── Streaming bubble helpers ── */
function createStreamBubble() {
  const div = document.createElement('div');
  div.className = 'msg-row alex-row';
  div.innerHTML = `
    <div class="alex-ava">${ALEX_ICON}</div>
    <div style="display:flex;flex-direction:column;gap:6px;flex:1;min-width:0">
      <div class="bubble alex-bubble stream-bubble">
        <span class="stream-text"></span>
        <span class="bubble-time" style="display:none">${now()}</span>
      </div>
    </div>`;
  document.getElementById('chat-msgs').appendChild(div);
  scrollEnd();
  return div;
}

function updateStreamBubble(div, text) {
  const span = div.querySelector('.stream-text');
  if (span) { span.innerHTML = renderMarkdown(text); scrollEnd(); }
}

function finaliseStreamBubble(div, text, addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails) {
  // Remove cursor, show time
  const cursor = div.querySelector('.stream-cursor');
  if (cursor) cursor.remove();
  const timeEl = div.querySelector('.bubble-time');
  if (timeEl) { timeEl.textContent = now(); timeEl.style.display = ''; }
  saveToCurrentSession('alex', text);

  // Add structured cards
  const wrapper = div.querySelector('.bubble.alex-bubble').parentElement;
  let extras = '';
  if (addresses && addresses.length > 0)       extras += buildAddressCard(addresses);
  if (binDates)                                 extras += buildBinDateCard(binDates);
  if (libraries && libraries.length > 0)       extras += buildLibraryCard(libraries);
  if (ctProperties && ctProperties.length > 0) extras += buildCouncilTaxPropertyPicker(ctProperties);
  else if (councilTax)                          extras += buildCouncilTaxCard(councilTax);
  if (schools && schools.length > 0)           extras += buildSchoolListCard(schools);
  if (schoolDetails)                            extras += buildSchoolCard(schoolDetails);
  if (extras) wrapper.insertAdjacentHTML('beforeend', extras);

  // Run multi-bubble split on finalised text
  const parts = splitIntoMessages(text);
  const firstBubble = div.querySelector('.bubble.alex-bubble');
  if (firstBubble) firstBubble.querySelector('.stream-text').innerHTML = renderMarkdown(parts[0] ?? text);
  if (parts.length > 1) {
    let delay = Math.min(1600, parts[0].length * 20 + 600);
    for (let i = 1; i < parts.length; i++) {
      const idx = i, isLast = idx === parts.length - 1;
      setTimeout(showInterimDots, Math.max(0, delay - 650));
      setTimeout(() => {
        hideInterimDots();
        appendAlexBubble(parts[idx],
          [], isLast ? addresses : null, isLast ? binDates : null,
          isLast ? libraries : null, isLast ? councilTax : null, isLast ? ctProperties : null,
          isLast ? schools : null, isLast ? schoolDetails : null);
      }, delay);
      delay += Math.min(1600, parts[idx].length * 20 + 600);
    }
  }
  scrollEnd();
}

/* ══════════════════════════════════════════════════════
   MULTI-BUBBLE SPLIT (unchanged logic)
══════════════════════════════════════════════════════ */
function splitIntoMessages(text) {
  const paras = text.split(/\n{2,}/).map(p => p.trim()).filter(Boolean);
  if (paras.length <= 1) return [text.trim()];
  const chunks = [];
  let cur = '';
  for (const para of paras) {
    const isHeading  = /^#{1,3}\s/.test(para);
    const isListPara = para.split('\n').filter(Boolean).every(l => /^[-•*]|\d+\./.test(l.trim()));
    const shouldCut  = (isHeading && cur.length > 80) || (!isListPara && cur.length > 360 && cur.trim());
    if (shouldCut) { if (cur.trim()) chunks.push(cur.trim()); cur = para; }
    else           { cur += (cur ? '\n\n' : '') + para; }
  }
  if (cur.trim()) chunks.push(cur.trim());
  return chunks.reduce((acc, c) => {
    if (acc.length && acc[acc.length-1].length < 70 && c.length < 70)
      acc[acc.length-1] += '\n\n' + c;
    else acc.push(c);
    return acc;
  }, []).filter(c => c.trim().length > 0);
}

let interimDots = null;
function showInterimDots() {
  if (interimDots) return;
  const box = document.getElementById('chat-msgs');
  interimDots = document.createElement('div');
  interimDots.className = 'msg-row alex-row';
  interimDots.setAttribute('aria-hidden', 'true');
  interimDots.innerHTML = `
    <div class="alex-ava">${ALEX_ICON}</div>
    <div class="typing-wrap">
      <div class="typing-dot"></div><div class="typing-dot"></div><div class="typing-dot"></div>
      <span class="typing-label">Bradford Council is typing…</span>
    </div>`;
  box.appendChild(interimDots);
  scrollEnd();
}
function hideInterimDots() { interimDots?.remove(); interimDots = null; }

/* Entry point — single or multi-bubble */
function addAlexMsg(text, sources, addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails) {
  const parts = splitIntoMessages(text);
  const hasCards = addresses || binDates || libraries || councilTax || ctProperties || schools || schoolDetails;
  if (parts.length <= 1 || hasCards) {
    appendAlexBubble(text, sources, addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails);
    return;
  }
  appendAlexBubble(parts[0], [], null, null, null, null, null, null, null);
  let delay = Math.min(1600, parts[0].length * 20 + 600);
  for (let i = 1; i < parts.length; i++) {
    const idx = i, isLast = idx === parts.length - 1;
    setTimeout(showInterimDots, Math.max(0, delay - 650));
    setTimeout(() => {
      hideInterimDots();
      appendAlexBubble(parts[idx],
        isLast ? sources : [], isLast ? addresses : null, isLast ? binDates : null,
        isLast ? libraries : null, isLast ? councilTax : null, isLast ? ctProperties : null,
        isLast ? schools : null, isLast ? schoolDetails : null);
    }, delay);
    delay += Math.min(1600, parts[idx].length * 20 + 600);
  }
}

/* Render one Alex bubble — time inside bubble (matches React) */
function appendAlexBubble(text, sources, addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails) {
  const div = document.createElement('div');
  div.className = 'msg-row alex-row';

  let extras = '';
  if (addresses && addresses.length > 0)       extras += buildAddressCard(addresses);
  if (binDates)                                 extras += buildBinDateCard(binDates);
  if (libraries && libraries.length > 0)       extras += buildLibraryCard(libraries);
  if (ctProperties && ctProperties.length > 0) extras += buildCouncilTaxPropertyPicker(ctProperties);
  else if (councilTax)                          extras += buildCouncilTaxCard(councilTax);
  if (schools && schools.length > 0)           extras += buildSchoolListCard(schools);
  if (schoolDetails)                            extras += buildSchoolCard(schoolDetails);

  div.innerHTML = `
    <div class="alex-ava">${ALEX_ICON}</div>
    <div style="display:flex;flex-direction:column;gap:6px;flex:1;min-width:0">
      <div class="bubble alex-bubble">
        ${renderMarkdown(text)}
        <span class="bubble-time">${now()}</span>
      </div>
      ${extras}
      ${renderSources(sources)}
    </div>`;
  document.getElementById('chat-msgs').appendChild(div);
  scrollEnd();
}

/* User message — time inside bubble, user avatar circle (matches React) */
function addUserMsg(text) {
  const div = document.createElement('div');
  div.className = 'msg-row user-row';
  div.innerHTML = `
    <div style="display:flex;flex-direction:column;align-items:flex-end;gap:0">
      <div class="bubble user-bubble">
        ${esc(text)}
        <span class="bubble-time">${now()}</span>
      </div>
    </div>
    <div class="user-ava">${USER_ICON}</div>`;
  document.getElementById('chat-msgs').appendChild(div);
  scrollEnd();
}

/* ── Intro bubble (title + body + time) — matches React introMessages ── */
function introBubble(title, body) {
  return `<div class="msg-row alex-row">
    <div class="alex-ava">${ALEX_ICON}</div>
    <div class="intro-bubble">
      <p class="intro-title">${title}</p>
      <p class="intro-body">${body}</p>
      <span class="intro-time">${now()}</span>
    </div>
  </div>`;
}

/* Legacy helper used by chatClear welcome */
function welcomeBubble(html) {
  return `<div class="msg-row alex-row">
    <div class="alex-ava">${ALEX_ICON}</div>
    <div class="bubble alex-bubble" style="flex:1;max-width:72%">
      ${html}
      <span class="bubble-time">${now()}</span>
    </div>
  </div>`;
}

/* ── Address picker card ── */
function buildAddressCard(addresses) {
  if (!addresses) return '';
  const postcode = addresses.length > 0 ? (addresses[0]?.postcode || '') : '';
  const items = addresses.map(a => `
    <button class="addr-btn" onclick="selectAddress('${esc2(a.line1 + (a.city ? ', '+a.city : ''))}', '${esc2(postcode)}')">
      <span class="addr-num">${a.number}</span>
      <span class="addr-text">
        <strong>${esc(a.line1)}</strong>
        <span>${esc(a.city)}${postcode ? ', ' + esc(postcode) : ''}</span>
      </span>
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" width="14" height="14"><polyline points="9 18 15 12 9 6"/></svg>
    </button>`).join('');

  const notListed = `
    <div class="addr-manual" id="manualWrap-${esc2(postcode)}">
      <button class="addr-not-listed" onclick="showManualEntry('${esc2(postcode)}')">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="12" height="12"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
        ${addresses.length === 0 ? 'Enter your address manually' : "My address isn't listed — enter manually"}
      </button>
      <div class="manual-entry" id="manualEntry-${esc2(postcode)}" style="display:none">
        <input type="text" id="manualHouse-${esc2(postcode)}" placeholder="e.g. 14 Wibsey Road" class="manual-input"/>
        <button class="manual-submit" onclick="submitManualAddress('${esc2(postcode)}')">Use this address →</button>
      </div>
    </div>`;

  return `<div class="addr-card">
    <div class="addr-card-head">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
      ${addresses.length > 0 ? `Addresses for <strong>${esc(postcode)}</strong>` : `Enter address for <strong>${esc(postcode)}</strong>`}
    </div>
    ${addresses.length > 0 ? `<div class="addr-list">${items}</div>` : ''}
    ${notListed}
  </div>`;
}

function showManualEntry(postcode) {
  document.getElementById(`manualEntry-${postcode}`).style.display = 'flex';
  document.getElementById(`manualHouse-${postcode}`).focus();
}
function submitManualAddress(postcode) {
  const val = document.getElementById(`manualHouse-${postcode}`).value.trim();
  if (!val) return;
  selectAddress(val, postcode);
}

/* ── Bin dates card ── */
function buildBinDateCard(bin) {
  if (!bin) return '';
  const hasDates = bin.hasDates === true;
  const checkerUrl = bin.checkerUrl || 'https://www.bradford.gov.uk/recycling-and-waste/bin-collections/check-your-bin-collection-dates/';
  const greyOk  = hasDates && bin.greyBin  && !bin.greyBin.includes('Check');
  const greenOk = hasDates && bin.greenBin && !bin.greenBin.includes('Check');
  const brownOk = hasDates && bin.brownBin && !bin.brownBin.includes('Check') && !bin.brownBin.includes('See');

  // Build schedule table rows for each bin type
  function scheduleRows(dates, icon, label, ok) {
    const next = (dates && dates.length > 0) ? dates[0] : null;
    const rest = (dates && dates.length > 1) ? dates.slice(1) : [];
    return `<div class="bin-row">
      <div class="bin-icon">${icon}</div>
      <div class="bin-info">
        <span class="bin-label">${label}</span>
        <span class="bin-date ${ok ? 'has-date' : ''}">${next ? esc(next) : (ok ? '' : (label.includes('Grey') ? 'Every 2 weeks — general waste' : label.includes('Green') ? 'Every 2 weeks — recycling' : 'Weekly Apr–Nov · Every 2 weeks Dec–Mar'))}</span>
        ${rest.length > 0 ? `<span class="bin-upcoming">${rest.map(d => esc(d)).join(' &nbsp;·&nbsp; ')}</span>` : ''}
      </div>
    </div>`;
  }

  const greyDates  = (bin.greyBinDates  && bin.greyBinDates.length  > 0) ? bin.greyBinDates  : (greyOk  ? [bin.greyBin]  : []);
  const greenDates = (bin.greenBinDates && bin.greenBinDates.length > 0) ? bin.greenBinDates : (greenOk ? [bin.greenBin] : []);
  const brownDates = (bin.brownBinDates && bin.brownBinDates.length > 0) ? bin.brownBinDates : (brownOk ? [bin.brownBin] : []);

  const exactDatesBtn = !hasDates ? `
    <a href="${checkerUrl}" target="_blank" class="bin-exact-btn">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
      Get your exact collection dates
    </a>` : '';

  return `<div class="bin-card">
    <div class="bin-card-head">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>
      Bin collection — <span>${esc(bin.address || '')}</span>
    </div>
    <div class="bin-rows">
      ${scheduleRows(greyDates,  '🗑️', 'Grey Bin',  greyOk)}
      ${scheduleRows(greenDates, '♻️', 'Green Bin', greenOk)}
      ${scheduleRows(brownDates, '🌿', 'Brown Bin', brownOk)}
    </div>
    ${exactDatesBtn}
    <a href="${checkerUrl}" target="_blank" class="bin-link">${hasDates ? 'Check for updates on bradford.gov.uk →' : 'Open Bradford bin checker →'}</a>
  </div>`;
}

/* ── Library picker card — clickable bubble buttons ── */
function selectLibrary(name) {
  doSend(`Tell me about ${name}`);
}

/* ── Ofsted badge colour ── */
function ofstedClass(rating) {
  if (!rating) return '';
  const r = rating.toLowerCase();
  if (r.includes('outstanding'))         return 'ofsted-outstanding';
  if (r.includes('good'))                return 'ofsted-good';
  if (r.includes('requires'))            return 'ofsted-ri';
  if (r.includes('inadequate'))          return 'ofsted-inadequate';
  return 'ofsted-unknown';
}

/* ── School picker list ── */
function buildSchoolListCard(schools) {
  if (!schools || schools.length === 0) return '';

  const rows = schools.map(s => {
    const badge = s.ofstedRating
      ? `<span class="ofsted-badge ${ofstedClass(s.ofstedRating)}">${esc(s.ofstedRating)}</span>`
      : '';
    const dist = s.distance ? `<span class="sch-dist-pill">${esc(s.distance)}</span>` : '';
    const meta = [s.phase, s.type].filter(Boolean).map(esc).join(' · ');
    return `
      <button class="sch-btn" onclick="selectSchool('${esc2(s.name)}')">
        <span class="sch-num">${s.number}</span>
        <span class="sch-info">
          <span class="sch-name">${esc(s.name)}</span>
          <span class="sch-meta">${meta}${s.address ? ' · ' + esc(s.address) : ''}</span>
        </span>
        <span class="sch-right">${badge}${dist}</span>
        <svg class="sch-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" width="14" height="14"><polyline points="9 18 15 12 9 6"/></svg>
      </button>`;
  }).join('');

  return `
    <div class="sch-list-card">
      <div class="sch-card-head">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>
        Nearby Schools — tap one for full details
      </div>
      <div class="sch-list">${rows}</div>
    </div>`;
}

function selectSchool(name) {
  doSend(`Tell me about ${name}`);
}

/* ── School detail card ── */
function buildSchoolCard(s) {
  if (!s) return '';
  const badge = s.ofstedRating
    ? `<span class="ofsted-badge ${ofstedClass(s.ofstedRating)}">${esc(s.ofstedRating)}</span>`
    : '';
  const ofstedDateStr = s.ofstedDate ? ` (${esc(s.ofstedDate)})` : '';
  const phoneRow = s.phone
    ? `<div class="sch-detail-row"><span class="sch-dl">Phone</span><span class="sch-dv"><a href="tel:${esc(s.phone)}">${esc(s.phone)}</a></span></div>`
    : '';
  const siteRow = s.website
    ? `<div class="sch-detail-row"><span class="sch-dl">Website</span><span class="sch-dv"><a href="${esc(s.website)}" target="_blank" rel="noopener">Visit school website ↗</a></span></div>`
    : '';

  return `
    <div class="sch-card">
      <div class="sch-card-head">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>
        <span>${esc(s.name)}</span>
        ${badge}
      </div>
      <div class="sch-details">
        ${s.address  ? `<div class="sch-detail-row"><span class="sch-dl">Address</span><span class="sch-dv">${esc(s.address)}</span></div>` : ''}
        ${s.phase    ? `<div class="sch-detail-row"><span class="sch-dl">Phase</span><span class="sch-dv">${esc(s.phase)}</span></div>` : ''}
        ${s.type     ? `<div class="sch-detail-row"><span class="sch-dl">Type</span><span class="sch-dv">${esc(s.type)}</span></div>` : ''}
        ${s.ageRange ? `<div class="sch-detail-row"><span class="sch-dl">Ages</span><span class="sch-dv">${esc(s.ageRange)}</span></div>` : ''}
        ${s.pupils   ? `<div class="sch-detail-row"><span class="sch-dl">Pupils</span><span class="sch-dv">${esc(s.pupils)}</span></div>` : ''}
        <div class="sch-detail-row"><span class="sch-dl">Ofsted</span><span class="sch-dv">${esc(s.ofstedRating||'Not yet inspected')}${ofstedDateStr}</span></div>
        ${phoneRow}
        ${siteRow}
      </div>
      <div class="sch-card-actions">
        <a href="${esc(s.admissionsUrl||'https://www.bradford.gov.uk/education-and-skills/schools/school-admissions/')}" target="_blank" class="sch-action-btn sch-btn-primary">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
          Apply for a school place
        </a>
        <a href="${esc(s.ofstedUrl||'https://reports.ofsted.gov.uk/')}" target="_blank" class="sch-action-btn sch-btn-secondary">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
          Ofsted report
        </a>
      </div>
    </div>`;
}

function buildLibraryCard(libraries) {
  if (!libraries || libraries.length === 0) return '';

  const buttons = libraries.map(lib => `
    <button class="lib-btn" onclick="selectLibrary('${esc2(lib.name)}')">
      <span class="lib-num">${lib.number}</span>
      <span class="lib-info">
        <span class="lib-name">${esc(lib.name)}</span>
        <span class="lib-meta">${esc(lib.distance)} &nbsp;·&nbsp; ${esc(lib.address)}</span>
      </span>
      <svg class="lib-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" width="14" height="14"><polyline points="9 18 15 12 9 6"/></svg>
    </button>`).join('');

  return `
    <div class="lib-card">
      <div class="lib-card-head">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/></svg>
        Bradford Libraries — tap one for details &amp; facilities
      </div>
      <div class="lib-list">${buttons}</div>
    </div>`;
}

/* ── Council tax card ── */
function buildCouncilTaxCard(ct) {
  if (!ct) return '';

  const bandBadge = ct.band
    ? `<span class="ct-band-badge">Band ${esc(ct.band)}</span>`
    : '';

  const amountBlock = (ct.annualAmount || ct.band)
    ? `<div class="ct-amounts">
        <div class="ct-amount-row">
          <span class="ct-amount-label">Annual</span>
          <span class="ct-amount-val">${esc(ct.annualAmount || '—')}</span>
        </div>
        <div class="ct-amount-row">
          <span class="ct-amount-label">Monthly</span>
          <span class="ct-amount-val">${esc(ct.monthlyAmount || '—')}</span>
        </div>
      </div>`
    : '';

  let bandRows = '';
  if (ct.allBands && ct.allBands.length > 0) {
    const rows = ct.allBands.map(b => {
      const isActive = ct.band && b.band === ct.band;
      return `<tr class="${isActive ? 'ct-active-band' : ''}">
        <td>${esc(b.band)}</td>
        <td>${esc(b.annualAmount)}</td>
        <td>${esc(b.monthlyAmount)}</td>
      </tr>`;
    }).join('');
    bandRows = `
      <details class="ct-all-bands">
        <summary>All Bradford bands (${esc(ct.taxYear || '2025/26')})</summary>
        <div class="tbl-wrap">
          <table>
            <thead><tr><th>Band</th><th>Annual</th><th>Monthly</th></tr></thead>
            <tbody>${rows}</tbody>
          </table>
        </div>
      </details>`;
  }

  return `<div class="ct-card">
    <div class="ct-card-head">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14"><rect x="2" y="5" width="20" height="14" rx="2"/><path d="M2 10h20"/></svg>
      Council Tax — <span>${esc(ct.address || '')}</span>
      ${bandBadge}
    </div>
    ${amountBlock}
    ${bandRows}
    <div class="ct-links">
      <a href="${esc(ct.payUrl || 'https://www.bradford.gov.uk/council-tax/pay-your-council-tax/')}" target="_blank" class="ct-link-btn">Pay council tax →</a>
      <a href="${esc(ct.bandLookupUrl || 'https://www.tax.service.gov.uk/check-if-you-need-to-contact-voa')}" target="_blank" class="ct-link-ghost">Check your band (VOA) →</a>
    </div>
  </div>`;
}

/* ── Council tax property picker — scrollable address list with band/amount ── */
function buildCouncilTaxPropertyPicker(props) {
  if (!props || props.length === 0) return '';

  const postcode = props[0]?.postcode || '';

  const items = props.map(p => {
    const bandLabel = p.band ? `Band ${esc(p.band)}` : '–';
    const annualLabel = p.annualAmount || '–';
    return `
      <button class="ctp-btn" onclick="selectCouncilTaxProperty(this)"
        data-band="${esc(p.band)}"
        data-annual="${esc(p.annualAmount)}"
        data-monthly="${esc(p.monthlyAmount)}"
        data-address="${esc(p.address)}"
        data-postcode="${esc(p.postcode)}">
        <span class="ctp-num">${p.number}</span>
        <span class="ctp-info">
          <span class="ctp-addr">${esc(p.address)}</span>
          <span class="ctp-meta">${bandLabel} &nbsp;·&nbsp; ${annualLabel}/year</span>
        </span>
        <svg class="ctp-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" width="14" height="14"><polyline points="9 18 15 12 9 6"/></svg>
      </button>`;
  }).join('');

  return `
    <div class="ctp-card">
      <div class="ctp-head">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
        Properties at <strong>${esc(postcode)}</strong> — tap yours
      </div>
      <div class="ctp-list">${items}</div>
      <div class="ctp-foot" id="ctp-detail-${esc(postcode).replace(/ /g,'')}" style="display:none"></div>
    </div>`;
}

function selectCouncilTaxProperty(btn) {
  // Highlight selected row
  btn.closest('.ctp-list').querySelectorAll('.ctp-btn').forEach(b => b.classList.remove('ctp-selected'));
  btn.classList.add('ctp-selected');

  const band     = btn.dataset.band;
  const annual   = btn.dataset.annual;
  const monthly  = btn.dataset.monthly;
  const address  = btn.dataset.address;

  // API receives full data so the AI uses the correct band/amounts
  // Chat bubble shows only the address (clean and short)
  const apiMsg = `I live at ${address}. My council tax is Band ${band}, annual charge ${annual} (${monthly}/month). Give me 3 short replies: 1) confirm my band and exact amount, 2) how to pay, 3) discounts I might qualify for.`;
  doSend(apiMsg, `I live at ${address}`);
}

function renderSources(src) {
  if (!src || !src.length) return '';
  const items = [...new Set(src)].slice(0,3).map(u => {
    const lbl = u.replace(/https?:\/\/www\.bradford\.gov\.uk\/?/,'').replace(/\/$/,'') || 'bradford.gov.uk';
    return `<a href="${u}" target="_blank" class="src-tag">
      <svg viewBox="0 0 16 16" width="10" height="10" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M6 2H3a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h10a1 1 0 0 0 1-1V9"/>
        <polyline points="11 1 15 1 15 5"/><line x1="15" y1="1" x2="7" y2="9"/>
      </svg>${lbl}</a>`;
  }).join('');
  return `<div class="msg-sources">${items}</div>`;
}

/* ── Typing indicator — matches React TypingDots ── */
let dotsEl = null;
function showDots() {
  const box = document.getElementById('chat-msgs');
  dotsEl = document.createElement('div');
  dotsEl.className = 'msg-row alex-row';
  dotsEl.id = 'typing-el';
  dotsEl.setAttribute('aria-hidden', 'true');
  dotsEl.innerHTML = `
    <div class="alex-ava">${ALEX_ICON}</div>
    <div class="typing-wrap">
      <div class="typing-dot"></div>
      <div class="typing-dot"></div>
      <div class="typing-dot"></div>
      <span class="typing-label">Bradford Council is typing…</span>
    </div>`;
  box.appendChild(dotsEl);
  scrollEnd();
}
function hideDots() { dotsEl?.remove(); dotsEl = null; }

/* ── Markdown renderer (headings, lists, tables, bold, links) ── */
function renderMarkdown(raw) {
  const lines = raw.split('\n');
  let html = '', inOl = false, inUl = false, tableRows = [];

  const closeOl    = () => { if (inOl) { html += '</ol>'; inOl = false; } };
  const closeUl    = () => { if (inUl) { html += '</ul>'; inUl = false; } };
  const flushTable = () => {
    if (!tableRows.length) return;
    html += '<div class="tbl-wrap"><table>';
    let headDone = false;
    for (const row of tableRows) {
      if (/^\|[-| :]+\|$/.test(row.trim())) {
        if (!headDone) { html += '</thead><tbody>'; headDone = true; }
        continue;
      }
      const cells = row.split('|').slice(1, -1).map(c => c.trim());
      if (!headDone) { html += '<thead><tr>' + cells.map(c=>`<th>${inline(c)}</th>`).join('') + '</tr>'; }
      else           { html += '<tr>' + cells.map(c=>`<td>${inline(c)}</td>`).join('') + '</tr>'; }
    }
    if (!headDone) html += '</thead><tbody>';
    html += '</tbody></table></div>';
    tableRows = [];
  };

  for (const line of lines) {
    if (/^\|.+\|$/.test(line.trim())) { closeOl(); closeUl(); tableRows.push(line.trim()); continue; }
    flushTable();

    const olMatch = line.match(/^(\d+)\.\s+(.+)/);
    if (olMatch) {
      closeUl();
      if (!inOl) { html += '<ol>'; inOl = true; }
      html += `<li><span>${inline(olMatch[2])}</span></li>`;
      continue;
    }

    const ulMatch = line.match(/^[-•*]\s+(.+)/);
    if (ulMatch) {
      closeOl();
      if (!inUl) { html += '<ul>'; inUl = true; }
      html += `<li><span>${inline(ulMatch[1])}</span></li>`;
      continue;
    }

    // Blank lines inside a list: keep the list open (don't reset the counter)
    if (!line.trim() && (inOl || inUl)) continue;

    closeOl(); closeUl();
    if (/^[-*_]{3,}$/.test(line.trim())) { html += '<hr/>'; continue; }
    if (line.startsWith('#### ')){ html += `<h5>${inline(line.slice(5))}</h5>`; continue; }
    if (line.startsWith('### ')) { html += `<h4>${inline(line.slice(4))}</h4>`; continue; }
    if (line.startsWith('## '))  { html += `<h3>${inline(line.slice(3))}</h3>`; continue; }
    if (line.startsWith('# '))   { html += `<h3>${inline(line.slice(2))}</h3>`; continue; }
    if (!line.trim())            { html += '<br/>'; continue; }
    html += `<p>${inline(line)}</p>`;
  }
  flushTable(); closeOl(); closeUl();
  return html;
}

function inline(t) {
  return esc(t)
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g,     '<em>$1</em>')
    .replace(/`(.+?)`/g,       '<code>$1</code>')
    .replace(/\[([^\]]+)\]\((https?:\/\/[^\)]+)\)/g, '<a href="$2" target="_blank">$1</a>');
}

/* ── Helpers ── */
function scrollEnd() { const b=document.getElementById('chat-msgs'); setTimeout(()=>b.scrollTop=b.scrollHeight,60); }
function now()       { return new Date().toLocaleTimeString([],{hour:'2-digit',minute:'2-digit'}); }
function dateChip()  { return '<div class="date-chip">Today</div>'; }
function esc(t)      { return String(t).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); }
function esc2(t)     { return String(t).replace(/'/g,"\\'"); }

/* ══════════════════════════════════════════════════════
   SESSION HISTORY  (localStorage)
══════════════════════════════════════════════════════ */
const HISTORY_KEY = 'bca_history';

function getHistory() {
  try { return JSON.parse(localStorage.getItem(HISTORY_KEY) || '[]'); } catch { return []; }
}
function saveHistory(sessions) {
  try { localStorage.setItem(HISTORY_KEY, JSON.stringify(sessions)); } catch {}
}

function saveToCurrentSession(role, text) {
  const sessions = getHistory();
  let session = sessions.find(s => s.id === SID);
  if (!session) {
    session = { id: SID, startTime: Date.now(), preview: '', messageCount: 0, messages: [] };
    sessions.unshift(session);
  }
  const trimmed = text.length > 2000 ? text.slice(0, 2000) + '…' : text;
  session.messages.push({ role, text: trimmed, time: now() });
  session.messageCount = session.messages.length;
  if (role === 'user' && !session.preview)
    session.preview = text.length > 60 ? text.slice(0, 60) + '…' : text;
  if (session.messages.length > 60) session.messages = session.messages.slice(-60);
  saveHistory(sessions.slice(0, 25));
}

/* ══════════════════════════════════════════════════════
   PROFILE & PREFERENCES  (localStorage)
══════════════════════════════════════════════════════ */
const PROFILE_KEY = 'bca_profile';
const PREFS_KEY   = 'bca_prefs';
const DEFAULT_PREFS = { replyLength:'normal', textSize:'md', theme:'blue', bubbleStyle:'normal' };

function getProfile() {
  try { return JSON.parse(localStorage.getItem(PROFILE_KEY) || 'null'); } catch { return null; }
}
function saveProfile(p) {
  if (p) localStorage.setItem(PROFILE_KEY, JSON.stringify(p));
  else   localStorage.removeItem(PROFILE_KEY);
}
function getPrefs() {
  try { return { ...DEFAULT_PREFS, ...JSON.parse(localStorage.getItem(PREFS_KEY) || '{}') }; }
  catch { return { ...DEFAULT_PREFS }; }
}
function savePrefs(p) {
  localStorage.setItem(PREFS_KEY, JSON.stringify(p));
}

/* Build the API text with profile context (first message) + style hint (every message) */
function buildApiText(raw) {
  const parts = [];
  if (!sessionProfileInjected) {
    sessionProfileInjected = true;
    const p = getProfile();
    if (p && (p.name || p.postcode || p.context)) {
      const ctx = [];
      if (p.name)     ctx.push('name=' + p.name);
      if (p.postcode) ctx.push('postcode=' + p.postcode);
      if (p.context)  ctx.push(p.context);
      parts.push('[User: ' + ctx.join(', ') + ']');
    }
  }
  const prefs = getPrefs();
  if (prefs.replyLength === 'brief')    parts.push('[brief reply]');
  if (prefs.replyLength === 'detailed') parts.push('[detailed reply with full explanations]');
  return parts.length > 0 ? parts.join(' ') + ' ' + raw : raw;
}

/* Apply preference CSS classes to the chat panel */
function applyPrefs() {
  const panel = document.getElementById('chat-panel');
  if (!panel) return;
  const prefs = getPrefs();
  panel.classList.remove('chat-sm','chat-lg','chat-dark','chat-hc','chat-compact','chat-spacious');
  if (prefs.textSize    === 'sm')       panel.classList.add('chat-sm');
  if (prefs.textSize    === 'lg')       panel.classList.add('chat-lg');
  if (prefs.theme       === 'dark')     panel.classList.add('chat-dark');
  if (prefs.theme       === 'hc')       panel.classList.add('chat-hc');
  if (prefs.bubbleStyle === 'compact')  panel.classList.add('chat-compact');
  if (prefs.bubbleStyle === 'spacious') panel.classList.add('chat-spacious');
}

/* Brief toast notification */
function showToast(msg) {
  document.querySelector('.sp-toast')?.remove();
  const el = document.createElement('div');
  el.className = 'sp-toast';
  el.textContent = msg;
  document.body.appendChild(el);
  setTimeout(() => el.remove(), 2500);
}

/* ══════════════════════════════════════════════════════
   SETTINGS PANEL
══════════════════════════════════════════════════════ */
const BACK_ARROW = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" width="14" height="14"><polyline points="15 18 9 12 15 6"/></svg>`;
const CHAT_ICON  = `<svg viewBox="0 0 24 24" fill="none" stroke="#16a34a" stroke-width="1.8" width="20" height="20"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>`;
const TRASH_ICON = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2"/></svg>`;

function openSettings() {
  settingsOpen = true;
  _chipsDisplay = document.getElementById('chat-chips').style.display;
  document.getElementById('chat-msgs').style.display       = 'none';
  document.getElementById('chat-chips').style.display      = 'none';
  document.getElementById('chat-input-wrap').style.display = 'none';
  const sp = document.getElementById('settings-panel');
  sp.style.display = 'flex';
  document.querySelector('.ch-settings')?.classList.add('active');
  renderSettingsHome();
}

function closeSettings() {
  settingsOpen = false;
  document.getElementById('settings-panel').style.display  = 'none';
  document.getElementById('chat-msgs').style.display       = '';
  document.getElementById('chat-chips').style.display      = _chipsDisplay;
  document.getElementById('chat-input-wrap').style.display = '';
  document.querySelector('.ch-settings')?.classList.remove('active');
}

/* ── Settings home ── */
function renderSettingsHome() {
  const sp      = document.getElementById('settings-panel');
  const profile = getProfile();
  const prefs   = getPrefs();
  const nameLabel  = profile?.name ? esc(profile.name) : '<span style="color:#94a3b8">Not set</span>';
  const themeLabel = { blue:'Bradford Blue', dark:'Dark mode', hc:'High contrast' }[prefs.theme] || 'Bradford Blue';
  const replyLabel = { brief:'Brief replies', normal:'Normal replies', detailed:'Detailed replies' }[prefs.replyLength] || 'Normal replies';
  const histCount  = getHistory().length;

  sp.innerHTML = `
    <div class="sp-header">
      <button class="sp-back" onclick="closeSettings()">${BACK_ARROW} Back</button>
      <span class="sp-title">Settings</span>
      <span class="sp-spacer"></span>
    </div>
    <div class="sp-body">
      <p class="sp-section-title">Account</p>
      <div class="sp-nav-card" role="button" tabindex="0" onclick="renderProfileScreen()" onkeydown="if(event.key==='Enter'||event.key===' '){event.preventDefault();renderProfileScreen();}">
        <div class="sp-nav-icon" style="background:#dbeafe">
          <svg viewBox="0 0 24 24" fill="none" stroke="#0f4ca3" stroke-width="1.8" width="20" height="20"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/></svg>
        </div>
        <div class="sp-nav-info">
          <span class="sp-nav-title">Personal Details</span>
          <span class="sp-nav-sub">${nameLabel}</span>
        </div>
        <svg viewBox="0 0 24 24" fill="none" stroke="#c8daf0" stroke-width="2.5" width="14" height="14"><polyline points="9 18 15 12 9 6"/></svg>
      </div>

      <p class="sp-section-title">Preferences</p>
      <div class="sp-nav-card" role="button" tabindex="0" onclick="renderAppearanceScreen()" onkeydown="if(event.key==='Enter'||event.key===' '){event.preventDefault();renderAppearanceScreen();}">
        <div class="sp-nav-icon" style="background:#f3e8ff">
          <svg viewBox="0 0 24 24" fill="none" stroke="#7c3aed" stroke-width="1.8" width="20" height="20"><circle cx="12" cy="12" r="3"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41"/></svg>
        </div>
        <div class="sp-nav-info">
          <span class="sp-nav-title">Appearance & Reply Style</span>
          <span class="sp-nav-sub">${themeLabel} &nbsp;·&nbsp; ${replyLabel}</span>
        </div>
        <svg viewBox="0 0 24 24" fill="none" stroke="#c8daf0" stroke-width="2.5" width="14" height="14"><polyline points="9 18 15 12 9 6"/></svg>
      </div>

      <p class="sp-section-title">Data</p>
      <div class="sp-nav-card" role="button" tabindex="0" onclick="renderHistoryScreen()" onkeydown="if(event.key==='Enter'||event.key===' '){event.preventDefault();renderHistoryScreen();}">
        <div class="sp-nav-icon" style="background:#dcfce7">
          ${CHAT_ICON}
        </div>
        <div class="sp-nav-info">
          <span class="sp-nav-title">Chat History</span>
          <span class="sp-nav-sub">${histCount} conversation${histCount !== 1 ? 's' : ''} saved</span>
        </div>
        <svg viewBox="0 0 24 24" fill="none" stroke="#c8daf0" stroke-width="2.5" width="14" height="14"><polyline points="9 18 15 12 9 6"/></svg>
      </div>
    </div>`;
}

/* ── Personal Details screen ── */
function renderProfileScreen() {
  const p  = getProfile() || {};
  const sp = document.getElementById('settings-panel');
  sp.innerHTML = `
    <div class="sp-header">
      <button class="sp-back" onclick="renderSettingsHome()">${BACK_ARROW} Settings</button>
      <span class="sp-title">Personal Details</span>
      <span class="sp-spacer"></span>
    </div>
    <div class="sp-body">
      <p class="sp-form-hint">Stored only on this device. Alex uses these to personalise answers.</p>

      <div class="sp-field">
        <label class="sp-field-label">Your name</label>
        <input id="pf-name" class="sp-field-input" type="text" placeholder="e.g. Sarah" value="${esc(p.name || '')}"/>
        <span class="sp-field-hint">Alex will greet and address you by name</span>
      </div>

      <div class="sp-field">
        <label class="sp-field-label">Bradford postcode</label>
        <input id="pf-postcode" class="sp-field-input" type="text" placeholder="e.g. BD5 8LT" value="${esc(p.postcode || '')}"/>
        <span class="sp-field-hint">Pre-fills bin collection and council tax lookups</span>
      </div>

      <div class="sp-field">
        <label class="sp-field-label">Additional context <span style="font-weight:400;color:#94a3b8">(optional)</span></label>
        <textarea id="pf-context" class="sp-field-input sp-field-area" placeholder="e.g. I am a landlord, I have mobility needs, I have 3 children...">${esc(p.context || '')}</textarea>
        <span class="sp-field-hint">Helps Alex give more relevant, specific answers</span>
      </div>

      <button class="sp-save-btn" onclick="saveProfileFromForm()">Save Details</button>
      ${p.name || p.postcode || p.context
        ? `<button class="sp-clear-btn" onclick="clearProfile()">Clear all details</button>`
        : ''}
    </div>`;
}

function saveProfileFromForm() {
  const name     = document.getElementById('pf-name')?.value.trim();
  const postcode = document.getElementById('pf-postcode')?.value.trim().toUpperCase();
  const context  = document.getElementById('pf-context')?.value.trim();
  saveProfile({ name, postcode, context });
  sessionProfileInjected = false;
  showToast('Details saved!');
  renderSettingsHome();
}

function clearProfile() {
  if (confirm('Clear all personal details?')) {
    saveProfile(null);
    sessionProfileInjected = false;
    renderProfileScreen();
  }
}

/* ── Appearance & Reply Style screen ── */
function renderAppearanceScreen() {
  const prefs = getPrefs();
  const sp    = document.getElementById('settings-panel');

  function sel(group, val) {
    return 'sp-sel-btn' + (prefs[group] === val ? ' sp-sel-active' : '');
  }

  sp.innerHTML = `
    <div class="sp-header">
      <button class="sp-back" onclick="renderSettingsHome()">${BACK_ARROW} Settings</button>
      <span class="sp-title">Appearance</span>
      <span class="sp-spacer"></span>
    </div>
    <div class="sp-body">

      <div class="sp-pref-group">
        <p class="sp-pref-label">Reply length</p>
        <p class="sp-pref-sub">How much detail Alex includes in each answer</p>
        <div class="sp-sel-row">
          <button class="${sel('replyLength','brief')}"    onclick="setPref('replyLength','brief',this)">Brief</button>
          <button class="${sel('replyLength','normal')}"   onclick="setPref('replyLength','normal',this)">Normal</button>
          <button class="${sel('replyLength','detailed')}" onclick="setPref('replyLength','detailed',this)">Detailed</button>
        </div>
      </div>

      <div class="sp-pref-group">
        <p class="sp-pref-label">Text size</p>
        <div class="sp-sel-row">
          <button class="${sel('textSize','sm')}" onclick="setPref('textSize','sm',this)"><span style="font-size:.8rem">A</span>&nbsp;Small</button>
          <button class="${sel('textSize','md')}" onclick="setPref('textSize','md',this)"><span style="font-size:.92rem">A</span>&nbsp;Normal</button>
          <button class="${sel('textSize','lg')}" onclick="setPref('textSize','lg',this)"><span style="font-size:1.05rem">A</span>&nbsp;Large</button>
        </div>
      </div>

      <div class="sp-pref-group">
        <p class="sp-pref-label">Colour theme</p>
        <div class="sp-sel-row">
          <button class="${sel('theme','blue')}" onclick="setPref('theme','blue',this)">
            <span class="sp-theme-dot" style="background:#0f4ca3"></span> Bradford
          </button>
          <button class="${sel('theme','dark')}" onclick="setPref('theme','dark',this)">
            <span class="sp-theme-dot" style="background:#1e293b"></span> Dark
          </button>
          <button class="${sel('theme','hc')}" onclick="setPref('theme','hc',this)">
            <span class="sp-theme-dot" style="background:#000"></span> High Contrast
          </button>
        </div>
      </div>

      <div class="sp-pref-group">
        <p class="sp-pref-label">Bubble spacing</p>
        <div class="sp-sel-row">
          <button class="${sel('bubbleStyle','compact')}"  onclick="setPref('bubbleStyle','compact',this)">Compact</button>
          <button class="${sel('bubbleStyle','normal')}"   onclick="setPref('bubbleStyle','normal',this)">Normal</button>
          <button class="${sel('bubbleStyle','spacious')}" onclick="setPref('bubbleStyle','spacious',this)">Spacious</button>
        </div>
      </div>

    </div>`;
}

function setPref(key, value, btnEl) {
  const prefs = getPrefs();
  prefs[key] = value;
  savePrefs(prefs);
  applyPrefs();
  btnEl.closest('.sp-sel-row').querySelectorAll('.sp-sel-btn').forEach(b => b.classList.remove('sp-sel-active'));
  btnEl.classList.add('sp-sel-active');
}

/* ── Chat History screen ── */
function renderHistoryScreen() {
  const sessions = getHistory();
  const sp       = document.getElementById('settings-panel');

  const cards = sessions.length === 0
    ? `<div class="sp-empty">
         <div class="sp-empty-icon">💬</div>
         <p>No chat history yet.<br/>Start a conversation to see it here.</p>
       </div>`
    : sessions.map(s => {
        const isCurrent = s.id === SID;
        const d = new Date(s.startTime);
        const dateStr = d.toLocaleDateString('en-GB', {day:'2-digit', month:'short', year:'numeric'});
        const timeStr = d.toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'});
        return `<div class="session-card${isCurrent ? ' session-current' : ''}" onclick="openSessionDetail('${esc2(s.id)}')">
          <div class="session-icon">${CHAT_ICON}</div>
          <div class="session-info">
            <span class="session-date">${dateStr} · ${timeStr}
              ${isCurrent ? '<span class="session-now-badge">Active</span>' : ''}
            </span>
            <span class="session-preview">${esc(s.preview || 'New conversation')}</span>
            <span class="session-count">${s.messageCount} message${s.messageCount !== 1 ? 's' : ''}</span>
          </div>
          <button class="session-del" onclick="deleteSession(event,'${esc2(s.id)}')" title="Delete">${TRASH_ICON}</button>
        </div>`;
      }).join('');

  sp.innerHTML = `
    <div class="sp-header">
      <button class="sp-back" onclick="renderSettingsHome()">${BACK_ARROW} Settings</button>
      <span class="sp-title">Chat History</span>
      ${sessions.length > 0
        ? `<button class="sp-del-all" onclick="confirmDeleteAll()">Delete all</button>`
        : `<span class="sp-spacer"></span>`}
    </div>
    <div class="sp-body">
      ${sessions.length > 0 ? '<p class="sp-section-title">Saved conversations</p>' : ''}
      ${cards}
    </div>`;
}

function openSessionDetail(sessionId) {
  const session = getHistory().find(s => s.id === sessionId);
  if (!session) return;
  const d       = new Date(session.startTime);
  const dateStr = d.toLocaleDateString('en-GB', {day:'2-digit', month:'short', year:'numeric'});
  const timeStr = d.toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'});

  const msgs = session.messages.length === 0
    ? `<p style="text-align:center;color:#94a3b8;padding:24px;font-family:Inter,sans-serif;font-size:.84rem">No messages saved.</p>`
    : session.messages.map(m => {
        if (m.role === 'user') {
          return `<div class="hist-row hist-user-row">
            <div class="hist-bubble hist-user-bubble">${esc(m.text)}<span class="hist-time">${m.time}</span></div>
            <div class="hist-ava hist-user-ava">${USER_ICON}</div>
          </div>`;
        }
        return `<div class="hist-row hist-alex-row">
          <div class="hist-ava hist-alex-ava">${ALEX_ICON}</div>
          <div class="hist-bubble hist-alex-bubble">${renderMarkdown(m.text)}<span class="hist-time">${m.time}</span></div>
        </div>`;
      }).join('');

  const sp = document.getElementById('settings-panel');
  sp.innerHTML = `
    <div class="sp-header">
      <button class="sp-back" onclick="renderHistoryScreen()">${BACK_ARROW} History</button>
      <span class="sp-title-sub">${dateStr} · ${timeStr}</span>
      <button class="sp-del-all" onclick="deleteSession(null,'${esc2(sessionId)}',true)">Delete</button>
    </div>
    <div class="sp-body hist-scroll">
      ${msgs}
    </div>`;
}

function deleteSession(e, sessionId) {
  if (e) e.stopPropagation();
  saveHistory(getHistory().filter(s => s.id !== sessionId));
  renderHistoryScreen();
}

function confirmDeleteAll() {
  if (confirm('Delete all chat history? This cannot be undone.')) {
    saveHistory([]);
    renderHistoryScreen();
  }
}

/* Apply preferences on page load */
applyPrefs();
