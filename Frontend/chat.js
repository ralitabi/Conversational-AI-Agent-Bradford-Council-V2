const API        = (window.BRADFORD_API || 'http://localhost:5000') + '/api/chat';
const STREAM_API = (window.BRADFORD_API || 'http://localhost:5000') + '/api/chat/stream';
const SID  = 'bca-' + Math.random().toString(36).slice(2,10);
let busy                  = false;
let chatEnabled           = true;
let settingsOpen          = false;
let _chipsDisplay         = '';
let sessionProfileInjected = false;

/* Bradford Council crest — council logo as agent avatar */
const ALEX_ICON = `<img src="https://www.bradford.gov.uk/css/2025/images/crest.svg" alt="Bradford Council" style="width:100%;height:100%;object-fit:cover;object-position:left center;display:block;" />`;
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
        'Need to speak with a specific department?',
        'If you\'d like to be connected to a Bradford Council department or officer, just let me know and I\'ll arrange it for you.'
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

  _lastUserMsg = text;  // track for trigger detection

  // ── Live support mode: route to contact API instead of AI ──
  if (_contactMode && contactSessionId && !contactClosed) {
    if (!text) return;
    el.value = ''; el.style.height = 'auto';
    document.getElementById('chat-chips').style.display = 'none';
    addUserMsg(text);
    try {
      await fetch(`${CONTACT_API}/${contactSessionId}/message`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content: text })
      });
    } catch {}
    el.focus();
    return;
  }

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
      structured?.schools, structured?.schoolDetails, structured?.properties,
      structured?.sportsCentres, structured?.sportsCentreDetails);

    // Offer human handoff if user asked or AI suggests it
    if (!_contactMode && !_handoffPending) {
      if (_wantsHuman(text) || _aiSuggestsHuman(fullText)) {
        setTimeout(() => _showHandoffCard(), 600);
      }
    }

  } catch (err) {
    hideDots();
    addAlexMsg("Sorry, I'm having trouble connecting. Please call Bradford Council on **01274 431000**.", [], null, null, null, null, null, null, null, null, null, null);
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

function finaliseStreamBubble(div, text, addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails, properties, sportsCentres, sportsCentreDetails) {
  const cursor = div.querySelector('.stream-cursor');
  if (cursor) cursor.remove();
  const timeEl = div.querySelector('.bubble-time');
  if (timeEl) { timeEl.textContent = now(); timeEl.style.display = ''; }
  saveToCurrentSession('alex', text);

  const parts = splitIntoMessages(text);
  const firstBubble = div.querySelector('.bubble.alex-bubble');
  if (firstBubble) firstBubble.querySelector('.stream-text').innerHTML = renderMarkdown(parts[0] ?? text);

  if (parts.length <= 1) {
    const wrapper = div.querySelector('.bubble.alex-bubble').parentElement;
    let extras = '';
    if (addresses && addresses.length > 0)       extras += buildAddressCard(addresses);
    if (binDates)                                 extras += buildBinDateCard(binDates);
    if (libraries && libraries.length > 0)        extras += buildLibraryCard(libraries);
    if (ctProperties && ctProperties.length > 0) extras += buildCouncilTaxPropertyPicker(ctProperties);
    else if (councilTax)                          extras += buildCouncilTaxCard(councilTax);
    if (schools && schools.length > 0)            extras += buildSchoolListCard(schools);
    if (schoolDetails)                            extras += buildSchoolCard(schoolDetails);
    if (properties)                               extras += buildPropertyGrid(properties);
    if (sportsCentres && sportsCentres.length > 0) extras += buildSportsCentreListCard(sportsCentres);
    if (sportsCentreDetails)                      extras += buildSportsCentreDetailCard(sportsCentreDetails);
    if (extras) wrapper.insertAdjacentHTML('beforeend', extras);
  }

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
          isLast ? schools : null, isLast ? schoolDetails : null, isLast ? properties : null,
          isLast ? sportsCentres : null, isLast ? sportsCentreDetails : null);
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
    // Also split when a new ## heading starts — each ## section = its own bubble
    const shouldCut  = (isHeading && cur.length > 40) || (!isListPara && cur.length > 320 && cur.trim());
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
function addAlexMsg(text, sources, addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails, properties, sportsCentres, sportsCentreDetails) {
  const parts = splitIntoMessages(text);
  const hasCards = addresses || binDates || libraries || councilTax || ctProperties || schools || schoolDetails || properties || sportsCentres || sportsCentreDetails;
  if (parts.length <= 1 || hasCards) {
    appendAlexBubble(text, sources, addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails, properties, sportsCentres, sportsCentreDetails);
    return;
  }
  appendAlexBubble(parts[0], [], null, null, null, null, null, null, null, null, null, null);
  let delay = Math.min(1600, parts[0].length * 20 + 600);
  for (let i = 1; i < parts.length; i++) {
    const idx = i, isLast = idx === parts.length - 1;
    setTimeout(showInterimDots, Math.max(0, delay - 650));
    setTimeout(() => {
      hideInterimDots();
      appendAlexBubble(parts[idx],
        isLast ? sources : [], isLast ? addresses : null, isLast ? binDates : null,
        isLast ? libraries : null, isLast ? councilTax : null, isLast ? ctProperties : null,
        isLast ? schools : null, isLast ? schoolDetails : null, isLast ? properties : null,
        isLast ? sportsCentres : null, isLast ? sportsCentreDetails : null);
    }, delay);
    delay += Math.min(1600, parts[idx].length * 20 + 600);
  }
}

/* Render one Alex bubble — time inside bubble (matches React) */
function appendAlexBubble(text, sources, addresses, binDates, libraries, councilTax, ctProperties, schools, schoolDetails, properties, sportsCentres, sportsCentreDetails) {
  const div = document.createElement('div');
  div.className = 'msg-row alex-row';

  let extras = '';
  if (addresses && addresses.length > 0)        extras += buildAddressCard(addresses);
  if (binDates)                                  extras += buildBinDateCard(binDates);
  if (libraries && libraries.length > 0)         extras += buildLibraryCard(libraries);
  if (ctProperties && ctProperties.length > 0)  extras += buildCouncilTaxPropertyPicker(ctProperties);
  else if (councilTax)                           extras += buildCouncilTaxCard(councilTax);
  if (schools && schools.length > 0)             extras += buildSchoolListCard(schools);
  if (schoolDetails)                             extras += buildSchoolCard(schoolDetails);
  if (properties)                                extras += buildPropertyGrid(properties);
  if (sportsCentres && sportsCentres.length > 0) extras += buildSportsCentreListCard(sportsCentres);
  if (sportsCentreDetails)                       extras += buildSportsCentreDetailCard(sportsCentreDetails);

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

/* ── Tip bubble — styled differently from intro bubbles ── */
function introTip(text) {
  return `<div class="msg-row alex-row">
    <div class="alex-ava">${ALEX_ICON}</div>
    <div class="intro-tip-bubble">
      <span class="intro-tip-icon">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="13" height="13"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.69 12a19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 3.61 1h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L8.09 8.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92z"/></svg>
      </span>
      ${text}
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

  // Build schedule rows — use actual dates when available, otherwise backend schedule text
  function scheduleRows(dates, label, ok) {
    const next = (dates && dates.length > 0) ? dates[0] : null;
    const rest = (dates && dates.length > 1) ? dates.slice(1) : [];
    return `<div class="bin-row">
      <div class="bin-type-badge bin-badge-${label.toLowerCase().replace(' bin','').trim()}">${label}</div>
      <div class="bin-info">
        <span class="bin-date ${ok ? 'has-date' : 'bin-schedule'}">${next ? esc(next) : ''}</span>
        ${rest.length > 0 ? `<span class="bin-upcoming">${rest.map(d => esc(d)).join(' &nbsp;·&nbsp; ')}</span>` : ''}
      </div>
    </div>`;
  }

  // Use backend schedule text even when hasDates=false (it has accurate schedule descriptions)
  const greyDates  = (bin.greyBinDates  && bin.greyBinDates.length  > 0) ? bin.greyBinDates
                   : bin.greyBin  ? [bin.greyBin]  : ['Every 2 weeks'];
  const greenDates = (bin.greenBinDates && bin.greenBinDates.length > 0) ? bin.greenBinDates
                   : bin.greenBin ? [bin.greenBin] : ['Every 2 weeks (alternating with grey)'];
  const brownDates = (bin.brownBinDates && bin.brownBinDates.length > 0) ? bin.brownBinDates
                   : bin.brownBin ? [bin.brownBin] : ['Weekly Apr–Nov · Fortnightly Dec–Mar'];

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
      ${scheduleRows(greyDates,  'Grey Bin',  greyOk)}
      ${scheduleRows(greenDates, 'Green Bin', greenOk)}
      ${scheduleRows(brownDates, 'Brown Bin', brownOk)}
    </div>
    ${!hasDates ? `<div class="bin-note">Exact next collection dates require Bradford's online form — tap below to check.</div>` : ''}
    ${exactDatesBtn}
    <div class="bin-footer-links">
      <a href="${checkerUrl}" target="_blank" class="bin-link">${hasDates ? 'Check for updates →' : 'Open bin checker →'}</a>
      <a href="https://www.bradford.gov.uk/recycling-and-waste/bin-collections/report-a-missed-bin-collection/" target="_blank" class="bin-link">Report missed collection →</a>
    </div>
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

  const ofstedDateStr = s.ofstedDate ? ` · ${esc(s.ofstedDate)}` : '';
  const admUrl   = s.admissionsUrl || 'https://www.bradford.gov.uk/education-and-skills/school-admissions/apply-for-a-place-at-one-of-bradford-districts-schools/';
  const ofstUrl  = s.ofstedUrl     || 'https://reports.ofsted.gov.uk/';
  const transpUrl= s.transportUrl  || 'https://www.bradford.gov.uk/education-and-skills/travel-assistance/assistance-with-travel-to-home-school-and-college/';
  const mealsUrl = s.freeMealsUrl  || 'https://www.bradford.gov.uk/education-and-skills/school-meals/paying-for-school-meals/';
  const termUrl  = s.termDatesUrl  || 'https://www.bradford.gov.uk/education-and-skills/school-holidays-and-term-dates/school-holidays-and-term-dates/';

  // Ofsted colour
  const oKey = !s.ofstedRating ? 'default'
    : s.ofstedRating.toLowerCase().includes('outstanding') ? 'outstanding'
    : s.ofstedRating.toLowerCase().includes('good')        ? 'good'
    : s.ofstedRating.toLowerCase().includes('requires')    ? 'requires'
    : s.ofstedRating.toLowerCase().includes('inadequate')  ? 'inadequate' : 'default';
  const ofstedColors = {
    outstanding:{ bg:'#dcfce7',color:'#14532d',border:'#86efac'},
    good:       { bg:'#dbeafe',color:'#1e3a8a',border:'#93c5fd'},
    requires:   { bg:'#fef9c3',color:'#713f12',border:'#fde047'},
    inadequate: { bg:'#fee2e2',color:'#7f1d1d',border:'#fca5a5'},
    default:    { bg:'#f1f5f9',color:'#475569',border:'#cbd5e1'}
  };
  const oc = ofstedColors[oKey];
  const typeColor = s.type && s.type.toLowerCase().includes('academy')
    ? {bg:'#ede9fe',color:'#4c1d95',border:'#c4b5fd'}
    : {bg:'#e0f2fe',color:'#0c4a6e',border:'#7dd3fc'};

  // ── Facilities ──
  const facs = s.facilities || [];
  // Strip emojis from facility labels
  const stripEmoji = t => t.replace(/[\u{1F300}-\u{1FFFF}\u{2600}-\u{26FF}\u{2700}-\u{27BF}\u{FE00}-\u{FE0F}]/gu,'').replace(/^\s+/,'');
  const facsHtml = facs.length > 0 ? `
    <div class="sch-facs-section">
      <p class="sch-section-label">Facilities &amp; Clubs</p>
      <div class="sch-facs-grid">${facs.map(f => `<span class="sch-fac-chip">${esc(stripEmoji(f))}</span>`).join('')}</div>
    </div>` : '';

  // ── Term dates calendar (collapsed by default) ──
  const periods  = s.termPeriods || [];
  const upcoming = periods.filter(p => !p.past);
  const past     = periods.filter(p => p.past);
  const typeConf = {
    term:     {dot:'#7dd3fc',bg:'#f0f9ff',border:'#bae6fd',color:'#0369a1'},
    halfterm: {dot:'#fde047',bg:'#fefce8',border:'#fde68a',color:'#854d0e'},
    christmas:{dot:'#fca5a5',bg:'#fef2f2',border:'#fca5a5',color:'#991b1b'},
    easter:   {dot:'#86efac',bg:'#f0fdf4',border:'#86efac',color:'#166534'},
    summer:   {dot:'#fdba74',bg:'#fff7ed',border:'#fdba74',color:'#c2410c'}
  };
  const renderPeriods = (list, faded) => list.map(p => {
    const c = typeConf[p.type] || typeConf.halfterm;
    return `<div class="td-chip" style="background:${c.bg};border-color:${c.border};color:${c.color};${faded?'opacity:.4;':''}">
      <span class="td-dot" style="background:${c.dot}"></span>
      <div class="td-info"><span class="td-label">${esc(p.label)}</span><span class="td-dates">${esc(p.dates)}</span></div>
    </div>`;
  }).join('');

  const termHtml = periods.length > 0 ? `
    <details class="sch-term-details">
      <summary class="sch-term-summary">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="13" height="13"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>
        Term Dates &amp; Holidays
        <span class="sch-term-year">${esc(s.academicYear||'2025/26 & 2026/27')}</span>
        ${s.isAcademy ? '<span class="sch-academy-note">Academy — may vary</span>' : ''}
        <span class="sch-term-arrow">▾</span>
      </summary>
      <div class="sch-term-body">
        ${upcoming.length > 0 ? `<div class="td-grid">${renderPeriods(upcoming,false)}</div>` : ''}
        ${past.length > 0 ? `
          <details class="td-past-wrap">
            <summary>Show past dates (${past.length})</summary>
            <div class="td-grid">${renderPeriods(past,true)}</div>
          </details>` : ''}
        <a href="${esc(termUrl)}" target="_blank" class="td-link">
          View full calendar on ${s.isAcademy ? 'school website' : 'bradford.gov.uk'} →
        </a>
      </div>
    </details>` : '';

  return `
    <div class="sch-card">

      <div class="sch-card-hero">
        <div class="sch-hero-icon">
          <svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.8" width="22" height="22"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>
        </div>
        <div class="sch-hero-info">
          <h3 class="sch-hero-name">${esc(s.name)}</h3>
          <div class="sch-hero-tags">
            ${s.phase ? `<span class="sch-tag" style="background:#e0f2fe;color:#0c4a6e;border-color:#7dd3fc">${esc(s.phase)}</span>` : ''}
            ${s.type  ? `<span class="sch-tag" style="background:${typeColor.bg};color:${typeColor.color};border-color:${typeColor.border}">${esc(s.type)}</span>` : ''}
            ${s.ofstedRating ? `<span class="sch-tag" style="background:${oc.bg};color:${oc.color};border-color:${oc.border}">Ofsted: ${esc(s.ofstedRating)}${ofstedDateStr}</span>` : `<span class="sch-tag" style="background:#f1f5f9;color:#64748b;border-color:#cbd5e1">Ofsted: Not yet rated</span>`}
          </div>
        </div>
      </div>

      <div class="sch-info-grid">
        ${s.address     ? `<div class="sch-info-row"><span class="sch-info-icon">📍</span><div><span class="sch-info-label">Address</span><span class="sch-info-val">${esc(s.address)}</span></div></div>` : ''}
        ${s.headteacher ? `<div class="sch-info-row"><div class="sch-info-icon-svg"><svg viewBox="0 0 24 24" fill="none" stroke="#64748b" stroke-width="2" width="14" height="14"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7s8 3 8 7"/></svg></div><div><span class="sch-info-label">Headteacher</span><span class="sch-info-val">${esc(s.headteacher)}</span></div></div>` : ''}
        ${s.ageRange    ? `<div class="sch-info-row"><div class="sch-info-icon-svg"><svg viewBox="0 0 24 24" fill="none" stroke="#64748b" stroke-width="2" width="14" height="14"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg></div><div><span class="sch-info-label">Age Range</span><span class="sch-info-val sch-age-highlight">${esc(s.ageRange)}</span></div></div>` : ''}
        ${s.phone       ? `<div class="sch-info-row"><div class="sch-info-icon-svg"><svg viewBox="0 0 24 24" fill="none" stroke="#64748b" stroke-width="2" width="14" height="14"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.69 13a19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 3.6 2h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92z"/></svg></div><div><span class="sch-info-label">Phone</span><span class="sch-info-val"><a href="tel:${esc(s.phone)}">${esc(s.phone)}</a></span></div></div>` : ''}
        ${s.website     ? `<div class="sch-info-row"><div class="sch-info-icon-svg"><svg viewBox="0 0 24 24" fill="none" stroke="#64748b" stroke-width="2" width="14" height="14"><circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/></svg></div><div><span class="sch-info-label">Website</span><span class="sch-info-val"><a href="${esc(s.website)}" target="_blank" rel="noopener">${esc(s.website.replace(/^https?:\/\//,''))}</a></span></div></div>` : ''}
        ${s.pupils      ? `<div class="sch-info-row"><div class="sch-info-icon-svg"><svg viewBox="0 0 24 24" fill="none" stroke="#64748b" stroke-width="2" width="14" height="14"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg></div><div><span class="sch-info-label">Number on Roll</span><span class="sch-info-val">${esc(s.pupils)}</span></div></div>` : ''}
      </div>

      ${facsHtml}
      ${termHtml}

      <div class="sch-action-row">
        <a href="${esc(admUrl)}" target="_blank" class="sch-act-primary">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" width="14" height="14"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>
          Apply for a school place
        </a>
        <a href="${esc(ofstUrl)}" target="_blank" class="sch-act-ghost">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="13" height="13"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
          Ofsted report
        </a>
      </div>

      <div class="sch-quick-links">
        <a href="${esc(mealsUrl)}" target="_blank" class="sch-ql">Free school meals</a>
        <a href="${esc(transpUrl)}" target="_blank" class="sch-ql">School transport</a>
        <a href="https://www.bradford.gov.uk/education-and-skills/school-support-services/school-support-services/" target="_blank" class="sch-ql">SEND support</a>
        <a href="https://www.bradford.gov.uk/education-and-skills/school-admissions/about-school-admissions/" target="_blank" class="sch-ql">Admissions guide</a>
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

/* ── Sports centre type icon ── */
function scTypeIcon(type) {
  if (type === 'pool')        return `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.8" width="20" height="20"><path d="M2 12h2a2 2 0 0 1 2 2 2 2 0 0 0 2 2 2 2 0 0 0 2-2 2 2 0 0 1 2-2 2 2 0 0 1 2 2 2 2 0 0 0 2 2 2 2 0 0 0 2-2 2 2 0 0 1 2-2h2"/><path d="M2 7h2a2 2 0 0 1 2 2 2 2 0 0 0 2 2 2 2 0 0 0 2-2 2 2 0 0 1 2-2 2 2 0 0 1 2 2 2 2 0 0 0 2 2 2 2 0 0 0 2-2 2 2 0 0 1 2-2h2"/><path d="M8 4l4-2 4 2"/></svg>`;
  if (type === 'gym')         return `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.8" width="20" height="20"><path d="M6.5 6.5h11M6.5 17.5h11M6 12h12M4 8V6a2 2 0 0 1 2-2h0a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h0a2 2 0 0 1-2-2v-2M20 8V6a2 2 0 0 0-2-2h0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h0a2 2 0 0 0 2-2v-2"/></svg>`;
  if (type === 'sports-hall') return `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.8" width="20" height="20"><circle cx="12" cy="12" r="10"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10"/><path d="M2 12h20"/></svg>`;
  return `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="1.8" width="20" height="20"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 9h18M9 21V9"/></svg>`;
}

function scTypeGradient(type) {
  if (type === 'pool')        return 'linear-gradient(135deg,#0369a1,#0ea5e9)';
  if (type === 'gym')         return 'linear-gradient(135deg,#7c3aed,#a78bfa)';
  if (type === 'sports-hall') return 'linear-gradient(135deg,#b45309,#f59e0b)';
  return 'linear-gradient(135deg,#065f46,#34d399)';
}

function scTypeLabel(type) {
  if (type === 'pool')        return 'Swimming Pool';
  if (type === 'gym')         return 'Gym & Fitness';
  if (type === 'sports-hall') return 'Sports Hall';
  return 'Multi-Sport';
}

/* ── Sports centre list card grid ── */
function buildSportsCentreListCard(centres) {
  if (!centres || centres.length === 0) return '';

  const cards = centres.map(c => {
    const grad = scTypeGradient(c.type);
    const icon = scTypeIcon(c.type);
    const label = scTypeLabel(c.type);
    const facs = (c.facilities || []).slice(0,4).map(f => `<span class="sc-fac-pill">${esc(f)}</span>`).join('');
    return `
      <div class="sc-card" onclick="selectSportsCentre('${esc2(c.name)}')">
        <div class="sc-card-header" style="background:${grad}">
          <div class="sc-card-icon">${icon}</div>
          <div class="sc-card-header-info">
            <span class="sc-type-label">${label}</span>
            <span class="sc-dist-badge">${esc(c.distance)}</span>
          </div>
        </div>
        <div class="sc-card-body">
          <h4 class="sc-card-name">${esc(c.name)}</h4>
          <p class="sc-card-address">
            <svg viewBox="0 0 24 24" fill="none" stroke="#64748b" stroke-width="2" width="12" height="12"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
            ${esc(c.address)}
          </p>
          ${facs ? `<div class="sc-fac-pills">${facs}</div>` : ''}
          ${c.phone ? `<p class="sc-card-phone">
            <svg viewBox="0 0 24 24" fill="none" stroke="#64748b" stroke-width="2" width="12" height="12"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.69 13a19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 3.6 2h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92z"/></svg>
            <a href="tel:${esc(c.phone)}" onclick="event.stopPropagation()">${esc(c.phone)}</a>
          </p>` : ''}
        </div>
        <button class="sc-card-btn">View full details →</button>
      </div>`;
  }).join('');

  return `
    <div class="sc-list-wrap">
      <div class="sc-list-head">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><path d="M2 12h2a2 2 0 0 1 2 2 2 2 0 0 0 2 2 2 2 0 0 0 2-2 2 2 0 0 1 2-2 2 2 0 0 1 2 2 2 2 0 0 0 2 2 2 2 0 0 0 2-2 2 2 0 0 1 2-2h2"/></svg>
        Bradford Sports Centres &amp; Pools — tap a card for full details
      </div>
      <div class="sc-grid">${cards}</div>
    </div>`;
}

function selectSportsCentre(name) {
  doSend(`Tell me about ${name}`);
}

/* ── Sports centre detail card ── */
function buildSportsCentreDetailCard(c) {
  if (!c) return '';
  const grad  = scTypeGradient(c.type);
  const icon  = scTypeIcon(c.type);
  const label = scTypeLabel(c.type);

  const facs = (c.facilities || []).map(f => `<span class="sc-detail-fac">${esc(f)}</span>`).join('');

  const hoursLines = (c.openingHours || '').split('·').map(s => s.trim()).filter(Boolean);
  const hoursHtml = hoursLines.length > 1
    ? hoursLines.map(l => `<div class="sc-hours-row">${esc(l)}</div>`).join('')
    : `<div class="sc-hours-row">${esc(c.openingHours || 'Check website for current hours')}</div>`;

  return `
    <div class="sc-detail-card">

      <div class="sc-detail-hero" style="background:${grad}">
        <div class="sc-detail-hero-icon">${icon}</div>
        <div class="sc-detail-hero-info">
          <span class="sc-detail-type-label">${label}</span>
          <h3 class="sc-detail-name">${esc(c.name)}</h3>
        </div>
      </div>

      <div class="sc-detail-body">

        <div class="sc-detail-contacts">
          ${c.address ? `
          <div class="sc-detail-row">
            <svg viewBox="0 0 24 24" fill="none" stroke="#0369a1" stroke-width="2" width="16" height="16"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
            <div>
              <span class="sc-detail-label">Address</span>
              <span class="sc-detail-val">${esc(c.address)}</span>
            </div>
          </div>` : ''}
          ${c.phone ? `
          <div class="sc-detail-row">
            <svg viewBox="0 0 24 24" fill="none" stroke="#0369a1" stroke-width="2" width="16" height="16"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.69 13a19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 3.6 2h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 22 16.92z"/></svg>
            <div>
              <span class="sc-detail-label">Phone</span>
              <span class="sc-detail-val"><a href="tel:${esc(c.phone)}">${esc(c.phone)}</a></span>
            </div>
          </div>` : ''}
          ${c.email ? `
          <div class="sc-detail-row">
            <svg viewBox="0 0 24 24" fill="none" stroke="#0369a1" stroke-width="2" width="16" height="16"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><polyline points="22,6 12,13 2,6"/></svg>
            <div>
              <span class="sc-detail-label">Email</span>
              <span class="sc-detail-val"><a href="mailto:${esc(c.email)}">${esc(c.email)}</a></span>
            </div>
          </div>` : ''}
        </div>

        ${c.openingHours ? `
        <div class="sc-detail-section">
          <div class="sc-detail-section-head">
            <svg viewBox="0 0 24 24" fill="none" stroke="#0369a1" stroke-width="2" width="14" height="14"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
            Opening Hours
          </div>
          <div class="sc-hours-grid">${hoursHtml}</div>
        </div>` : ''}

        ${facs ? `
        <div class="sc-detail-section">
          <div class="sc-detail-section-head">
            <svg viewBox="0 0 24 24" fill="none" stroke="#0369a1" stroke-width="2" width="14" height="14"><polyline points="9 11 12 14 22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg>
            Facilities
          </div>
          <div class="sc-detail-facs">${facs}</div>
        </div>` : ''}

        ${c.pageUrl ? `
        <a href="${esc(c.pageUrl)}" target="_blank" class="sc-detail-link-btn">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="14" height="14"><circle cx="12" cy="12" r="10"/><line x1="2" y1="12" x2="22" y2="12"/><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"/></svg>
          View on Bradford Council website
        </a>` : ''}

      </div>
    </div>`;
}

/* ── Bradford Homes property grid ── */
function buildPropertyGrid(result) {
  if (!result || !result.items || result.items.length === 0) return '';

  const cards = result.items.map(p => {
    const img = p.imageUrl
      ? `<div class="prop-img" style="background-image:url('${esc(p.imageUrl)}')"></div>`
      : `<div class="prop-img prop-img-placeholder">
           <svg viewBox="0 0 24 24" fill="none" stroke="#c5cde0" stroke-width="1.2" width="40" height="40">
             <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>
             <polyline points="9 22 9 12 15 12 15 22"/>
           </svg>
           <span class="prop-no-photo">No photo</span>
         </div>`;

    const bedroomBadge = p.bedrooms
      ? `<span class="prop-badge prop-badge-bed">🛏 ${esc(p.bedrooms)}</span>` : '';
    const distBadge = p.distance && p.distance !== 'unknown'
      ? `<span class="prop-badge prop-badge-dist">📍 ${esc(p.distance)}</span>` : '';

    const features = (p.features || [])
      .filter(f => f && f !== 'Property Shop')
      .slice(0, 3)
      .map(f => `<span class="prop-feature-tag">${esc(f)}</span>`).join('');

    const viewBtn = p.detailUrl
      ? `<a href="${esc(p.detailUrl)}" target="_blank" rel="noopener" class="prop-view-btn">View Property →</a>` : '';

    return `<div class="prop-card">
      ${img}
      <div class="prop-body">
        ${bedroomBadge || distBadge ? `<div class="prop-badges">${bedroomBadge}${distBadge}</div>` : ''}
        <div class="prop-title">${esc(p.title || p.address)}</div>
        <div class="prop-address">${esc(p.address)}</div>
        ${p.rent     ? `<div class="prop-rent">${esc(p.rent)}</div>` : ''}
        ${p.landlord ? `<div class="prop-landlord">${esc(p.landlord.trim())}</div>` : ''}
        ${features   ? `<div class="prop-features">${features}</div>` : ''}
        ${viewBtn}
      </div>
    </div>`;
  }).join('');

  const searchUrl = result.searchUrl || 'https://www.bradfordhomes.org.uk/PropertySearch/Results?AdvertTypes=21&Location.Name=Bradford&SearchRadius=10&SortOrder=0';
  const total     = result.totalFound || result.items.length;

  return `<div class="prop-grid-wrap">
    <div class="prop-grid-head">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>
      Available properties near <strong>${esc(result.location || 'Bradford')}</strong>
      <span class="prop-total">${total} found</span>
    </div>
    <div class="prop-grid">${cards}</div>
    <div class="prop-grid-footer">
      <a href="${esc(searchUrl)}" target="_blank" rel="noopener" class="prop-search-more">
        See all ${total} results on Bradford Homes →
      </a>
    </div>
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
  return autoLink(html);
}

function inline(t) {
  return esc(t)
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g,     '<em>$1</em>')
    .replace(/`(.+?)`/g,       '<code>$1</code>')
    .replace(/\[([^\]]+)\]\((https?:\/\/[^\)]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>');
}

/* Auto-link phones, emails, bare URLs that aren't already inside <a> tags */
function autoLink(html) {
  // Split on existing <a> tags — odd-indexed parts are already linked, skip them
  const parts = html.split(/(<a\b[^>]*>[\s\S]*?<\/a>)/);
  return parts.map((chunk, i) => {
    if (i % 2 === 1) return chunk;
    return chunk
      // UK landline / mobile: 01274 439450 · 07123 456789
      .replace(/(0\d{4}\s\d{6,7})/g, p =>
        `<a href="tel:${p.replace(/\s+/g,'')}" class="auto-tel">${p}</a>`)
      // Email addresses
      .replace(/([a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,})/g, e =>
        `<a href="mailto:${e}" class="auto-email">${e}</a>`)
      // Bare https URLs not already linked (not followed by ) which means inside markdown)
      .replace(/\bhttps?:\/\/[^\s<>"&\)]+/g, u =>
        `<a href="${u}" target="_blank" rel="noopener" class="auto-url">${u}</a>`);
  }).join('');
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
   CONTACT COUNCIL STAFF — AGENTIC FLOW
══════════════════════════════════════════════════════ */
const CONTACT_API = (() => {
  const h = window.location.hostname;
  return (h === 'localhost' || h === '127.0.0.1')
    ? 'http://localhost:5000/api/contact'
    : '/api/contact';
})();

const CONTACT_STORE_KEY = 'bca_contact_session_v1';

let contactSessionId  = null;
let contactPollTimer  = null;
let contactLastMsgId  = 0;
let contactClosed     = false;
let contactAttach     = null;
let contactUnreadCount = 0;
let _shownMsgIds      = new Set();
let _cachedMessages   = [];   // persisted across close/open

let _contactMode    = false;   // true when live support is active
let _handoffPending = false;   // true when confirmation card is showing
let _lastUserMsg    = '';      // last user message text

const _HUMAN_PATTERNS = [
  // Explicit staff / person requests
  /\b(speak|talk|chat)\s+(to|with)\s+(a\s+)?(real\s+)?(person|someone|human|staff|officer|agent)\b/i,
  /\b(want|need|like)\s+(a\s+)?(real\s+)?(person|human|agent|officer)\b/i,
  /\bcontact\s+staff\b/i,
  /\bhuman\s+(help|support|agent|assistance)\b/i,
  /\blive\s+(support|chat|agent)\b/i,
  /\bconnect\s+me\b/i,
  /\bescalate\b/i,
  /\breal\s+person\b/i,
  /\bcouncil\s+officer\b/i,
  /\btransfer\s+me\b/i,
  /\bspeak\s+with\s+someone\b/i,
  // Department / team chat requests
  /\b(?:wanna|want\s+to|need\s+to|like\s+to|can\s+i|could\s+i)\s+(?:have\s+(?:a\s+)?)?(?:chat|talk|speak)\s+with\b/i,
  /\bchat\s+with\s+(?:the\s+)?(?:council|[\w]+(?:\s+[\w]+){0,3})\s+(?:department|team|office|section|helpline)\b/i,
  /\bspeak\s+(?:directly\s+)?(?:to|with)\s+(?:the\s+)?(?:[\w]+(?:\s+[\w]+){0,3})\s+(?:department|team|office)\b/i,
  /\btalk\s+(?:directly\s+)?(?:to|with)\s+(?:the\s+)?(?:[\w]+(?:\s+[\w]+){0,3})\s+(?:department|team|office)\b/i,
  /\bget\s+(?:in\s+touch|connected|through)\s+(?:to|with)\b/i,
  /\bput\s+me\s+(?:in\s+touch|through)\b/i,
  /\bsomebody\s+(?:from|at)\b/i,
];
const _AI_ESCALATION = [
  /\ba council officer\b/i,
  /\bcontact bradford council directly\b/i,
  /\bspeak(?:ing)?\s+with.*(officer|staff)\b/i,
  /\b01274\b/,
  /\badvise you to contact\b/i,
  /\bi can'?t connect you\b/i,
  /\bcannot connect you\b/i,
  /\byou(?:'ll)? need to contact\b/i,
  /\breach out (?:to|directly)\b/i,
];
function _wantsHuman(t)    { return _HUMAN_PATTERNS.some(p => p.test(t)); }
function _aiSuggestsHuman(t){ return _AI_ESCALATION.some(p => p.test(t)); }

const _adminAvatarCache = {};
const _ADMIN_AVA_COLS   = ['#005192','#7c3aed','#16a34a','#F26C11','#0891b2','#dc2626','#4f46e5'];
function _adminAvaColor(n) { let h=0; for(const c of n) h=(h*31+c.charCodeAt(0))%_ADMIN_AVA_COLS.length; return _ADMIN_AVA_COLS[h]; }
async function _getAdminAvatar(name) {
  if (name in _adminAvatarCache) return _adminAvatarCache[name];
  _adminAvatarCache[name] = null;
  try {
    const r = await fetch(`${CONTACT_API}/staff-avatar?name=${encodeURIComponent(name)}`);
    if (r.ok) { const d = await r.json(); _adminAvatarCache[name] = d.avatarDataUrl || null; }
  } catch {}
  return _adminAvatarCache[name];
}

// ── Contact session persistence helpers ───────────────────────────────────
function _saveContactSession(extra = {}) {
  if (!contactSessionId) return;
  const stored = {
    sessionId:    contactSessionId,
    name:         convData.name  || '',
    topic:        convData.topic || '',
    lastMsgId:    contactLastMsgId,
    closed:       contactClosed,
    savedAt:      Date.now(),
    messages:     _cachedMessages.slice(-150),  // keep last 150 messages
    contactMode:  _contactMode,
    staffName:    _currentStaffName || '',
    ...extra
  };
  localStorage.setItem(CONTACT_STORE_KEY, JSON.stringify(stored));
}

function _loadContactSession() {
  try {
    const raw = localStorage.getItem(CONTACT_STORE_KEY);
    if (!raw) return null;
    const s = JSON.parse(raw);
    // Expire after 7 days
    if (Date.now() - s.savedAt > 7 * 86400000) { localStorage.removeItem(CONTACT_STORE_KEY); return null; }
    return s;
  } catch { return null; }
}

function _clearContactSession() {
  localStorage.removeItem(CONTACT_STORE_KEY);
}

// ── Notification helpers ───────────────────────────────────────────────────
function _requestNotificationPermission() {
  if ('Notification' in window && Notification.permission === 'default') {
    Notification.requestPermission();
  }
}

function _showContactNotification(senderName, content) {
  // Red dot on button always
  _setContactUnreadDot(true);

  // Browser notification only when tab is hidden
  if (!document.hidden) return;
  if (!('Notification' in window) || Notification.permission !== 'granted') return;

  const text = content.startsWith('[IMG]') ? '📎 Sent an image' : content.slice(0, 80);
  const n = new Notification('Bradford Council Staff', {
    body: `${senderName}: ${text}`,
    icon: 'https://www.bradford.gov.uk/css/2025/images/crest.svg',
    tag:  'bca-contact',
    requireInteraction: false
  });
  n.onclick = () => { window.focus(); openContact(); n.close(); };
}

function _setContactUnreadDot(show) {
  const dot = document.getElementById('contact-unread-dot');
  if (dot) dot.style.display = show ? '' : 'none';
  // Also update page title
  if (show) {
    if (!document.title.startsWith('● ')) document.title = '● ' + document.title;
  } else {
    document.title = document.title.replace(/^● /, '');
  }
}

// ── Auto-restore on page load ──────────────────────────────────────────────
(function _restoreContactOnLoad() {
  const s = _loadContactSession();
  if (!s) return;
  contactSessionId  = s.sessionId;
  contactLastMsgId  = s.lastMsgId || 0;
  contactClosed     = s.closed    || false;
  convData.name     = s.name;
  convData.topic    = s.topic;
  _setContactUnreadDot(true);

  if (s.contactMode && !s.closed) {
    // Restore live support in main chat on page reload
    _cachedMessages = s.messages || [];
    _shownMsgIds    = new Set();
    _contactMode    = true;
    // Render cached messages into main chat once DOM is ready
    setTimeout(() => {
      _cachedMessages.forEach(m => {
        _shownMsgIds.add(m.id);
        if (m.sender === 'admin') _addStaffMessage(m.senderName, m.content, m.timestamp);
        else if (m.sender === 'citizen') addUserMsg(m.content);
      });
      contactLastMsgId = _cachedMessages.length
        ? Math.max(..._cachedMessages.map(m => m.id)) : 0;
      _setLiveMode(s.sessionId, s.staffName || '');
      if (contactPollTimer) clearInterval(contactPollTimer);
      contactPollTimer = setInterval(_pollHandoffMessages, 2500);
      _pollHandoffMessages();
    }, 300);
  } else {
    contactPollTimer = setInterval(_backgroundContactPoll, 5000);
  }
})();

async function _backgroundContactPoll() {
  if (!contactSessionId) return;
  try {
    const r = await fetch(`${CONTACT_API}/${contactSessionId}/messages?after=${contactLastMsgId}`);
    if (!r.ok) return;
    const d = await r.json();

    if (d.messages.length > 0) {
      const adminMsgs = d.messages.filter(m => m.sender === 'admin');
      if (adminMsgs.length > 0) {
        const last = adminMsgs[adminMsgs.length - 1];
        _showContactNotification(last.senderName, last.content);
      }
      d.messages.forEach(m => {
        contactLastMsgId = Math.max(contactLastMsgId, m.id);
        if (!_cachedMessages.find(c => c.id === m.id))
          _cachedMessages.push({ id: m.id, sender: m.sender, senderName: m.senderName, content: m.content, timestamp: m.timestamp });
      });
      _saveContactSession();
    }

    contactClosed = d.status === 'closed';
  } catch {}
}

// Agentic conversation state
let convState = 'idle';
let convData  = {};

const CONTACT_TOPICS = [
  'Council Tax', 'Housing', 'Benefits & Support', 'Bins & Recycling',
  'Planning', 'Schools & Education', 'Health & Social Care',
  'Transport & Roads', 'Business', 'Other'
];

function openContact() {
  // Legacy: now just shows handoff card inline
  if (!_handoffPending && !_contactMode) _showHandoffCard();
}

function closeContact() {
  // No-op: contact is now part of main chat; keep background poll if session active
  if (contactPollTimer) clearInterval(contactPollTimer);
  if (contactSessionId && !contactClosed)
    contactPollTimer = setInterval(_backgroundContactPoll, 5000);
}

// ── Bot message helpers ────────────────────────────────────────────────────
function _convBotMsg(html) {
  const msgs = document.getElementById('contact-conv-msgs');
  const t = document.getElementById('conv-typing-dot');
  if (t) t.remove();
  const div = document.createElement('div');
  div.className = 'conv-bot-row';
  div.innerHTML = `
    <div class="conv-bot-ava"><img src="https://www.bradford.gov.uk/css/2025/images/crest.svg" alt="BC"/></div>
    <div class="conv-bot-bubble">${html}</div>`;
  msgs.appendChild(div);
  msgs.scrollTop = msgs.scrollHeight;
}

function _convTyping() {
  const msgs = document.getElementById('contact-conv-msgs');
  const d = document.createElement('div');
  d.className = 'conv-bot-row'; d.id = 'conv-typing-dot';
  d.innerHTML = `<div class="conv-bot-ava"><img src="https://www.bradford.gov.uk/css/2025/images/crest.svg" alt="BC"/></div><div class="conv-bot-bubble conv-typing"><span></span><span></span><span></span></div>`;
  msgs.appendChild(d);
  msgs.scrollTop = msgs.scrollHeight;
}

function _convUserMsg(text) {
  const msgs = document.getElementById('contact-conv-msgs');
  const div = document.createElement('div');
  div.className = 'conv-user-row';
  div.innerHTML = `<div class="conv-user-bubble">${esc(text)}</div>`;
  msgs.appendChild(div);
  msgs.scrollTop = msgs.scrollHeight;
}

function _showTopicChips() {
  const row = document.getElementById('contact-chip-row');
  row.innerHTML = CONTACT_TOPICS.map(t =>
    `<button class="conv-chip" onclick="handleConvTopic(this,'${t}')">${t}</button>`
  ).join('');
  row.style.display = 'flex';
  document.getElementById('contact-conv-input').disabled = true;
}

// ── User sends in conversation ─────────────────────────────────────────────
function convContactSend() {
  const inp = document.getElementById('contact-conv-input');
  const val = inp.value.trim();
  if (!val || inp.disabled) return;
  inp.value = ''; inp.style.height = 'auto';
  _processConvInput(val);
}

function convContactKey(e) {
  if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); convContactSend(); }
}

function _processConvInput(val) {
  const inp = document.getElementById('contact-conv-input');
  switch (convState) {
    case 'name':
      convData.name = val;
      _convUserMsg(val);
      inp.disabled = true;
      convState = 'topic';
      _convTyping();
      setTimeout(() => {
        _convBotMsg(`Thanks, <strong>${esc(val)}</strong>! What is your enquiry about?`);
        _showTopicChips();
      }, 700);
      break;

    case 'message':
      convData.message = val;
      _convUserMsg(val);
      inp.disabled = true;
      if (convData.email) {
        _submitConvContact();
      } else {
        convState = 'email';
        _convTyping();
        setTimeout(() => {
          _convBotMsg("What's your <strong>email address</strong>? Staff will use it only to follow up on your query — it is kept private and never shared publicly. <span style='color:#94a3b8;font-size:.8em'>(type <em>skip</em> to continue without)</span>");
          inp.disabled = false; inp.placeholder = 'your@email.com or skip'; inp.focus();
        }, 700);
      }
      break;

    case 'email': {
      const isSkip = val.toLowerCase() === 'skip' || !val.includes('@');
      const email  = isSkip ? '' : val.trim();
      convData.email = email;
      if (email) {
        // Auto-save email to profile so it's never asked again
        const existing = getProfile() || {};
        saveProfile({ ...existing, email });
        _convUserMsg('✓ Email saved');
      } else {
        _convUserMsg('(no email)');
      }
      inp.disabled = true;
      _submitConvContact();
      break;
    }
  }
}

function handleConvTopic(btn, topic) {
  document.getElementById('contact-chip-row').style.display = 'none';
  convData.topic = topic;
  _convUserMsg(topic);
  convState = 'message';
  _convTyping();
  const inp = document.getElementById('contact-conv-input');
  setTimeout(() => {
    _convBotMsg(`Got it — <strong>${esc(topic)}</strong>.<br>Please describe your question or issue:`);
    inp.disabled = false; inp.placeholder = 'Describe your issue…'; inp.focus();
  }, 700);
}

async function _submitConvContact() {
  _convTyping();
  await new Promise(r => setTimeout(r, 700));
  _convBotMsg("Connecting you with a Bradford Council staff member now...");
  try {
    const r = await fetch(CONTACT_API + '/start', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name:    convData.name,
        email:   convData.email || '',
        phone:   '',
        subject: convData.topic,
        message: convData.message
      })
    });
    if (!r.ok) throw new Error();
    const d = await r.json();
    contactSessionId = d.sessionId;
    contactLastMsgId = 0;
    contactClosed    = false;
    _saveContactSession();
    _requestNotificationPermission();
    setTimeout(() => startContactChat(d.sessionId, convData.name, convData.topic, convData.message), 800);
  } catch {
    const t = document.getElementById('conv-typing-dot');
    if (t) t.remove();
    _convBotMsg("Sorry, there was a problem reaching staff. Please try again.");
    convState = 'message';
    const inp = document.getElementById('contact-conv-input');
    inp.disabled = false; inp.focus();
  }
}

function _setContactRef(sessionId) {
  const refEl = document.getElementById('contact-ref');
  const ref2  = document.getElementById('contact-ref2');
  const wrap  = document.getElementById('cch-ref-wrap');
  if (refEl) refEl.textContent = sessionId;
  if (ref2)  ref2.textContent  = sessionId;
  if (wrap)  wrap.style.display = sessionId ? '' : 'none';
}

function startContactChat(sessionId, name, subject, initMessage) {
  // Clear any existing poll (background or foreground)
  if (contactPollTimer) { clearInterval(contactPollTimer); contactPollTimer = null; }

  document.getElementById('contact-conv-view').style.display = 'none';
  document.getElementById('contact-chat-view').style.display = 'flex';
  _setContactRef(sessionId);
  _cachedMessages = [];
  _saveContactSession();

  // Show waiting banner + the citizen's own message immediately
  const msgsEl = document.getElementById('contact-msgs');
  msgsEl.innerHTML = '';
  const waiting = document.createElement('div');
  waiting.className = 'contact-waiting';
  waiting.innerHTML = `
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" width="32" height="32"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>
    <div><strong>Message received.</strong></div>
    <div>A council staff member will reply shortly.</div>
    <div style="font-size:.72rem;margin-top:6px">Ref: <strong>${sessionId}</strong></div>`;
  msgsEl.appendChild(waiting);

  contactLastMsgId = 0;
  _shownMsgIds = new Set();

  // Run immediately then every 2.5s
  pollContactMessages();
  contactPollTimer = setInterval(pollContactMessages, 2500);
}

async function pollContactMessages() {
  if (!contactSessionId) return;
  try {
    const r = await fetch(`${CONTACT_API}/${contactSessionId}/messages?after=${contactLastMsgId}`);
    if (!r.ok) return;
    const d = await r.json();

    d.messages.forEach(m => {
      if (_shownMsgIds.has(m.id)) { contactLastMsgId = Math.max(contactLastMsgId, m.id); return; }
      _shownMsgIds.add(m.id);
      _cachedMessages.push({ id: m.id, sender: m.sender, senderName: m.senderName, content: m.content, timestamp: m.timestamp });
      appendContactMsg(m.sender, m.senderName, m.content, m.timestamp);
      contactLastMsgId = Math.max(contactLastMsgId, m.id);
      if (m.sender === 'admin') {
        const waiting = document.querySelector('.contact-waiting');
        if (waiting) waiting.remove();
        // Update header staff label on first admin reply
        const lbl = document.getElementById('cch-staff-label');
        if (lbl && lbl.textContent === 'Staff Support Chat') lbl.textContent = `${m.senderName} is helping you`;
        if (_contactMode) {
          const lbl2 = document.getElementById('cch-staff-label');
          // won't exist in new design, ignore
        }
        _currentStaffName = m.senderName;
        if (document.hidden) _showContactNotification(m.senderName, m.content);
      }
    });
    if (d.messages.length > 0) _saveContactSession();

    // Update status badge
    const badge = document.getElementById('contact-status-label');
    if (badge) {
      const labels = { waiting: 'Waiting', active: 'Active', closed: 'Closed' };
      badge.textContent = labels[d.status] || d.status;
      badge.className = 'contact-status-badge ' + d.status;
    }

    if (d.status === 'closed' && !contactClosed) {
      contactClosed = true;
      clearInterval(contactPollTimer);
      contactPollTimer = null;
      appendContactSystemMsg('This session has been closed by staff. Thank you for contacting Bradford Council.');
      document.getElementById('contact-input').disabled = true;
      document.querySelector('.contact-send-btn').disabled = true;
      _saveContactSession({ closed: true });
    }
  } catch {}
}

function _renderMsgContent(text) {
  if (!text) return '';
  // Feedback request card
  const fbMatch = text.match(/\[FEEDBACK_REQUEST:([^:]+):([^\]]+)\]/);
  if (fbMatch) {
    const adminUsername = fbMatch[1];
    const adminName     = fbMatch[2];
    const cardId = 'fb_' + Math.random().toString(36).slice(2, 8);
    setTimeout(() => _initFeedbackCard(cardId, adminUsername, adminName), 50);
    return `<div class="fb-card" id="${cardId}">
      <div class="fb-card-title">
        <svg viewBox="0 0 24 24" fill="currentColor" width="16" height="16"><path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/></svg>
        Rate Your Experience
      </div>
      <div class="fb-card-sub">How was your support from <strong>${escapeHtml(adminName)}</strong> today?</div>
      <div class="fb-stars" id="${cardId}_stars">
        ${[1,2,3,4,5].map(i => `<button class="fb-star" data-v="${i}" onclick="_fbPickStar('${cardId}',${i})">★</button>`).join('')}
      </div>
      <textarea class="fb-comment" id="${cardId}_comment" placeholder="Leave a comment (optional)…" rows="2"></textarea>
      <button class="fb-submit" id="${cardId}_btn" onclick="_fbSubmit('${cardId}','${escapeHtml(adminUsername)}')">Submit Feedback</button>
      <div class="fb-error" id="${cardId}_err" style="display:none"></div>
    </div>`;
  }
  if (text.includes('[POSTCODE_REQUEST]')) {
    const cardId = 'pc_' + Math.random().toString(36).slice(2, 8);
    return `<div class="postcode-card" id="${cardId}">
      <div class="postcode-card-title">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/></svg>
        Find Your Address
      </div>
      <div class="postcode-card-sub">Enter your postcode to select your address</div>
      <div class="postcode-card-row">
        <input class="postcode-card-inp" id="${cardId}_inp" placeholder="e.g. BD1 1NZ" maxlength="10" autocomplete="postal-code"
          oninput="this.value=this.value.toUpperCase()"
          onkeydown="if(event.key==='Enter'){event.preventDefault();_postcodeSearch('${cardId}')}">
        <button class="postcode-card-btn" id="${cardId}_btn" onclick="_postcodeSearch('${cardId}')">Find</button>
      </div>
      <div id="${cardId}_results"></div>
      <div class="postcode-card-err" id="${cardId}_err" style="display:none"></div>
    </div>`;
  }

  const parts = text.split(/(\[IMG\][\s\S]*?\[\/IMG\])/);
  return parts.map(p => {
    if (p.startsWith('[IMG]') && p.endsWith('[/IMG]')) {
      const src = p.slice(5, -6);
      return `<img src="${src}" style="max-width:200px;max-height:160px;border-radius:8px;margin-top:6px;display:block;cursor:pointer" onclick="window.open(this.src,'_blank')">`;
    }
    return escapeHtml(p).replace(/\n/g, '<br>');
  }).join('');
}

let _fbSelected = {};
function _initFeedbackCard(id) { _fbSelected[id] = 0; }
function _fbPickStar(id, val) {
  _fbSelected[id] = val;
  document.querySelectorAll(`#${id}_stars .fb-star`).forEach((btn, i) => {
    btn.classList.toggle('active', i < val);
  });
}
async function _fbSubmit(id, adminUsername) {
  const stars   = _fbSelected[id] || 0;
  const comment = document.getElementById(`${id}_comment`)?.value.trim() || '';
  const errEl   = document.getElementById(`${id}_err`);
  const btn     = document.getElementById(`${id}_btn`);
  if (stars === 0) { errEl.textContent = 'Please select a star rating.'; errEl.style.display = ''; return; }
  errEl.style.display = 'none';
  btn.disabled = true; btn.textContent = 'Submitting…';
  try {
    const r = await fetch(`${CONTACT_API}/${contactSessionId}/feedback`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ stars, comment, adminUsername })
    });
    if (!r.ok) throw new Error();
    // Replace card with thank-you
    const card = document.getElementById(id);
    if (card) {
      card.innerHTML = `
        <div class="fb-done">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="24" height="24"><polyline points="20 6 9 17 4 12"/></svg>
          <div>
            <strong>Thank you for your feedback!</strong>
            <div style="font-size:.8rem;margin-top:3px">${stars} star${stars !== 1 ? 's' : ''} — ${comment || 'No comment'}</div>
          </div>
        </div>`;
    }
  } catch {
    errEl.textContent = 'Failed to submit. Please try again.'; errEl.style.display = '';
    btn.disabled = false; btn.textContent = 'Submit Feedback';
  }
}

async function _postcodeSearch(cardId) {
  const inp     = document.getElementById(`${cardId}_inp`);
  const btn     = document.getElementById(`${cardId}_btn`);
  const results = document.getElementById(`${cardId}_results`);
  const errEl   = document.getElementById(`${cardId}_err`);
  if (!inp) return;
  const postcode = inp.value.trim().toUpperCase();
  if (!postcode) { errEl.textContent = 'Please enter a postcode.'; errEl.style.display = ''; return; }
  errEl.style.display = 'none';
  btn.disabled = true; btn.textContent = '…';
  results.innerHTML = '<div style="font-size:.78rem;color:#64748b;padding:8px 0 4px">Searching…</div>';
  try {
    const r = await fetch(`${CONTACT_API}/address?postcode=${encodeURIComponent(postcode)}`);
    const d = r.ok ? await r.json() : null;
    const addrs = d?.addresses;
    if (!addrs || !addrs.length) {
      results.innerHTML = '';
      errEl.textContent = `No addresses found for ${postcode}. Please check and try again.`;
      errEl.style.display = '';
      btn.disabled = false; btn.textContent = 'Find';
      return;
    }
    results.innerHTML = `<div class="pc-addr-list">${
      addrs.map((a, i) => `<button class="pc-addr-item" onclick="_selectAddress('${cardId}',this.dataset.v)" data-v="${escapeHtml(`${a.line1}, ${a.city}, ${a.postcode}`)}">
        <span class="pc-addr-num">${i+1}</span>
        <span>${escapeHtml(a.line1)}, ${escapeHtml(a.city)}, ${escapeHtml(a.postcode)}</span>
      </button>`).join('')
    }</div>`;
    btn.disabled = false; btn.textContent = 'Find';
  } catch {
    results.innerHTML = '';
    errEl.textContent = 'Could not reach address service. Please try again.';
    errEl.style.display = '';
    btn.disabled = false; btn.textContent = 'Find';
  }
}

async function _selectAddress(cardId, addressText) {
  const card = document.getElementById(cardId);
  if (card) {
    card.innerHTML = `<div class="postcode-card-done">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" width="18" height="18"><polyline points="20 6 9 17 4 12"/></svg>
      <div><strong>Address confirmed</strong><br><span style="font-size:.8rem;color:#15803d">${escapeHtml(addressText)}</span></div>
    </div>`;
  }
  if (contactSessionId && !contactClosed) {
    try {
      await fetch(`${CONTACT_API}/${contactSessionId}/message`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ content: `My address is: ${addressText}` })
      });
      await pollContactMessages();
    } catch {}
  }
}

let _currentStaffName = '';

function _setLiveMode(sessionId, staffName) {
  _contactMode = true;
  const nameEl = document.getElementById('ch-name-id');
  if (nameEl) nameEl.innerHTML = `Council Staff <span class="ch-live-badge">LIVE</span>`;
  const sub = document.querySelector('.ch-subtitle');
  if (sub) sub.textContent = `Ref: ${sessionId} · A staff member is helping you`;
  const inp = document.getElementById('chat-input');
  if (inp) inp.placeholder = 'Message council staff…';
}

function _endLiveMode() {
  _contactMode    = false;
  _handoffPending = false;
  const nameEl = document.getElementById('ch-name-id');
  if (nameEl) nameEl.textContent = 'Bradford Council Assistant';
  const sub = document.querySelector('.ch-subtitle');
  if (sub) sub.textContent = 'Live guidance & service support';
  const inp = document.getElementById('chat-input');
  if (inp) inp.placeholder = 'Ask me anything about Bradford Council…';
}

function _showHandoffCard() {
  if (_handoffPending || _contactMode) return;
  _handoffPending = true;
  const msgs = document.getElementById('chat-msgs');
  const div  = document.createElement('div');
  div.className = 'msg-row alex-row';
  div.id = 'handoff-card';
  div.innerHTML = `
    <div class="alex-ava">${ALEX_ICON}</div>
    <div style="display:flex;flex-direction:column;gap:5px;flex:1;min-width:0">
      <div class="bubble alex-bubble handoff-bubble" id="handoff-bubble">
        <div class="handoff-title">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
          Connect to a Council Officer?
        </div>
        <p class="handoff-sub">Would you like this matter handled by a Bradford Council officer who can help directly?</p>
        <div class="handoff-btns">
          <button class="handoff-yes" id="hd-yes-btn">Yes, connect me</button>
          <button class="handoff-no"  id="hd-no-btn">No, continue chat</button>
        </div>
      </div>
      <span class="bubble-time">${now()}</span>
    </div>`;
  msgs.appendChild(div);

  const bubbleEl = div.querySelector('.handoff-bubble');

  div.querySelector('#hd-yes-btn').addEventListener('click', () => {
    _handoffPending = false;
    if (bubbleEl) bubbleEl.innerHTML = `
      <div style="display:flex;align-items:center;gap:8px;font-size:.83rem;color:#15803d">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" width="15" height="15"><polyline points="20 6 9 17 4 12"/></svg>
        Confirmed — connecting you with a council officer now.
      </div>`;
    // Send as a regular chat message so the AI collects their details naturally
    doSend('Yes, I confirm that I want to contact Bradford Council and share my details so I can speak with a council officer.');
  });

  div.querySelector('#hd-no-btn').addEventListener('click', () => _declineHandoff(bubbleEl));
  scrollEnd();
}

function _declineHandoff(bubble) {
  _handoffPending = false;
  if (!bubble) bubble = document.getElementById('handoff-bubble');
  if (bubble) bubble.innerHTML = `<span style="font-size:.83rem;color:#64748b">No problem — I'll continue helping you here.</span>`;
}

function _confirmHandoff(bubble) {
  if (!bubble) bubble = document.getElementById('handoff-bubble');
  if (!bubble) return;
  const profile = getProfile() || {};

  // Build options HTML separately to avoid any template-literal issues
  const topicOptions = CONTACT_TOPICS
    .map(t => `<option value="${t.replace(/"/g,'&quot;')}">${t}</option>`)
    .join('');

  bubble.innerHTML = `
    <div class="handoff-title">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="15" height="15"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
      Just a few details
    </div>
    <div class="handoff-form">
      <div class="hf-label">Your name *</div>
      <input id="hf-name"  class="handoff-inp" placeholder="e.g. John Smith" value="${(profile.name  || '').replace(/"/g,'&quot;')}">
      <div class="hf-label">Which department? *</div>
      <select id="hf-topic" class="handoff-inp">
        <option value="">Select a department…</option>
        ${topicOptions}
      </select>
      <div class="hf-label">Email address *</div>
      <input id="hf-email" class="handoff-inp" placeholder="your@email.com" type="email" value="${(profile.email || '').replace(/"/g,'&quot;')}">
      <div class="hf-label">Phone number <span style="font-weight:400;opacity:.6">(optional)</span></div>
      <input id="hf-phone" class="handoff-inp" placeholder="07xxx xxxxxx" type="tel">
      <div id="hf-err" class="handoff-err" style="display:none"></div>
      <button class="handoff-yes" id="hf-connect-btn">Connect Me Now</button>
    </div>`;

  // Wire events directly on the bubble's elements
  bubble.querySelector('#hf-connect-btn').addEventListener('click', _submitHandoff);
  bubble.querySelectorAll('.handoff-inp').forEach(el =>
    el.addEventListener('keydown', e => { if (e.key === 'Enter') _submitHandoff(); })
  );

  // Scroll the card into view
  const card = document.getElementById('handoff-card');
  if (card) card.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  setTimeout(() => document.getElementById('hf-name')?.focus(), 200);
}

async function _submitHandoff() {
  const name  = document.getElementById('hf-name')?.value.trim();
  const email = document.getElementById('hf-email')?.value.trim();
  const phone = document.getElementById('hf-phone')?.value.trim();
  const topic = document.getElementById('hf-topic')?.value || 'Other';
  const errEl = document.getElementById('hf-err');
  if (!name)              { errEl.textContent = 'Please enter your full name.';        errEl.style.display = ''; return; }
  if (!email?.includes('@')) { errEl.textContent = 'Please enter a valid email address.'; errEl.style.display = ''; return; }
  errEl.style.display = 'none';

  const btn = document.getElementById('hf-connect-btn');
  if (btn) { btn.disabled = true; btn.textContent = 'Connecting…'; }

  // Save email to profile
  const existing = getProfile() || {};
  saveProfile({ ...existing, name, email });

  // Build chat context for admin
  const ctxLines = [];
  document.querySelectorAll('#chat-msgs .msg-row').forEach(row => {
    const isUser = row.classList.contains('user-row');
    const b = row.querySelector('.bubble');
    if (!b || row.id === 'handoff-card') return;
    const txt = b.textContent.trim().slice(0, 300);
    if (txt) ctxLines.push((isUser ? 'Citizen: ' : 'AI: ') + txt);
  });
  const context = ctxLines.slice(-12).join('\n');

  try {
    const r = await fetch(`${CONTACT_API}/start`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name, email,
        phone:   phone || null,
        subject: topic,
        message: context ? `[AI conversation context]\n${context}\n[End context]` : `Contact request from ${name}`
      })
    });
    if (!r.ok) throw new Error();
    const d = await r.json();

    contactSessionId = d.sessionId;
    contactLastMsgId = 0;
    contactClosed    = false;
    _cachedMessages  = [];
    _shownMsgIds     = new Set();
    _currentStaffName = '';
    convData.name = name; convData.email = email; convData.topic = topic;
    _saveContactSession({ contactMode: true });

    const bubble = document.getElementById('handoff-bubble');
    if (bubble) {
      bubble.innerHTML = `
        <div class="handoff-connected">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" width="20" height="20"><polyline points="20 6 9 17 4 12"/></svg>
          <div>
            <strong>You're connected!</strong>
            <div style="font-size:.77rem;margin-top:3px;opacity:.75">A council officer will reply shortly in this chat.<br>Reference: <strong>${d.sessionId}</strong></div>
          </div>
        </div>`;
      bubble.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    _setLiveMode(d.sessionId, '');
    if (contactPollTimer) clearInterval(contactPollTimer);
    contactPollTimer = setInterval(_pollHandoffMessages, 2500);
    _pollHandoffMessages();
  } catch {
    errEl.textContent = 'Connection failed. Please try again.';
    errEl.style.display = '';
    if (btn) { btn.disabled = false; btn.textContent = 'Connect Now'; }
    _handoffPending = false;
  }
}

async function _pollHandoffMessages() {
  if (!contactSessionId || !_contactMode) return;
  try {
    const r = await fetch(`${CONTACT_API}/${contactSessionId}/messages?after=${contactLastMsgId}`);
    if (!r.ok) return;
    const d = await r.json();

    d.messages.forEach(m => {
      if (_shownMsgIds.has(m.id)) { contactLastMsgId = Math.max(contactLastMsgId, m.id); return; }
      _shownMsgIds.add(m.id);
      _cachedMessages.push({ id: m.id, sender: m.sender, senderName: m.senderName, content: m.content, timestamp: m.timestamp });
      contactLastMsgId = Math.max(contactLastMsgId, m.id);

      if (m.sender === 'admin') {
        _currentStaffName = m.senderName;
        _addStaffMessage(m.senderName, m.content, m.timestamp);
        // Update live badge subtitle with staff name
        const sub = document.querySelector('.ch-subtitle');
        if (sub) sub.textContent = `Ref: ${contactSessionId} · ${m.senderName} is helping you`;
        if (document.hidden) _showContactNotification(m.senderName, m.content);
      }
    });

    if (d.messages.length > 0) _saveContactSession({ contactMode: true, staffName: _currentStaffName });

    if (d.status === 'closed' && !contactClosed) {
      contactClosed = true;
      clearInterval(contactPollTimer); contactPollTimer = null;
      _endLiveMode();
      // Add system message in main chat
      const msgs = document.getElementById('chat-msgs');
      const sys  = document.createElement('div');
      sys.style.cssText = 'text-align:center;padding:10px;font-size:.76rem;color:#94a3b8;font-style:italic';
      sys.textContent = 'This live support session has been closed by staff. You can continue chatting with the AI assistant.';
      msgs.appendChild(sys);
      scrollEnd();
      _saveContactSession({ contactMode: false, closed: true });
    }
  } catch {}
}

function _addStaffMessage(staffName, content, timestamp) {
  const msgs  = document.getElementById('chat-msgs');
  if (!msgs) return;
  const time  = timestamp
    ? new Date(timestamp).toLocaleTimeString('en-GB', { hour:'2-digit', minute:'2-digit' })
    : now();
  const avaId = 'sav_' + Math.random().toString(36).slice(2, 8);
  const color = _adminAvaColor(staffName);
  const div   = document.createElement('div');
  div.className = 'msg-row alex-row staff-row';
  div.innerHTML = `
    <div class="staff-ava" id="${avaId}" style="background:${color}">${staffName.charAt(0).toUpperCase()}</div>
    <div style="display:flex;flex-direction:column;gap:3px;flex:1;min-width:0">
      <div class="staff-name-lbl">${escapeHtml(staffName)} <span class="staff-badge">Staff</span></div>
      <div class="bubble staff-bubble">${_renderMsgContent(content)}</div>
      <span class="bubble-time">${time}</span>
    </div>`;
  msgs.appendChild(div);
  scrollEnd();
  _getAdminAvatar(staffName).then(url => {
    if (!url) return;
    const el = document.getElementById(avaId);
    if (el) { el.innerHTML = `<img src="${url}" style="width:100%;height:100%;object-fit:cover;border-radius:50%">`; el.style.background = 'transparent'; }
  });
}

function attachContactImage(input) {
  const file = input.files[0];
  if (!file) return;
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = e => resolve(e.target.result);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  }).then(dataUrl => {
    contactAttach = dataUrl;
    const prev = document.getElementById('contact-attach-preview');
    if (prev) {
      prev.innerHTML = `<img src="${dataUrl}" style="max-height:56px;max-width:70px;border-radius:6px;border:1px solid #e2e8f0"><button onclick="clearContactAttach()" style="background:none;border:none;cursor:pointer;color:#64748b;font-size:1.2rem;line-height:1;padding:0 4px" title="Remove">&times;</button>`;
      prev.style.display = 'flex';
    }
  });
  input.value = '';
}

function clearContactAttach() {
  contactAttach = null;
  const prev = document.getElementById('contact-attach-preview');
  if (prev) { prev.innerHTML = ''; prev.style.display = 'none'; }
}

function appendContactMsg(sender, senderName, content, timestamp) {
  // In live-support mode, admin messages go into the main AI chat
  if (_contactMode && sender === 'admin') {
    _addStaffMessage(senderName, content, timestamp);
    return;
  }
  // In live-support mode, skip re-rendering own citizen messages (already shown optimistically)
  if (_contactMode && sender === 'citizen') return;

  const msgsEl = document.getElementById('contact-msgs');
  const time   = timestamp ? new Date(timestamp).toLocaleTimeString('en-GB', { hour:'2-digit', minute:'2-digit' }) : new Date().toLocaleTimeString('en-GB', { hour:'2-digit', minute:'2-digit' });
  const div    = document.createElement('div');

  // Special cards (feedback / postcode) render full-width outside any bubble
  if (content && (content.includes('[FEEDBACK_REQUEST:') || content.includes('[POSTCODE_REQUEST]'))) {
    div.className = 'contact-msg-feedback-wrap';
    div.innerHTML = _renderMsgContent(content);
    msgsEl.appendChild(div);
    msgsEl.scrollTop = msgsEl.scrollHeight;
    return;
  }

  if (sender === 'admin') {
    const avaId  = 'cav_' + Math.random().toString(36).slice(2, 8);
    const color  = _adminAvaColor(senderName);
    const letter = senderName.charAt(0).toUpperCase();
    div.className = 'contact-msg admin';
    div.innerHTML = `
      <div class="contact-admin-row">
        <div class="contact-admin-ava" id="${avaId}" style="background:${color}">${letter}</div>
        <div class="contact-admin-content">
          <div class="contact-admin-name">${escapeHtml(senderName)}</div>
          <div class="contact-bubble">${_renderMsgContent(content)}</div>
          <div class="contact-msg-meta">${time}</div>
        </div>
      </div>`;
    _getAdminAvatar(senderName).then(url => {
      if (!url) return;
      const el = document.getElementById(avaId);
      if (el) { el.innerHTML = `<img src="${url}" style="width:100%;height:100%;object-fit:cover;border-radius:50%">`; el.style.background = 'transparent'; }
    });
  } else if (sender === 'system') {
    div.style.cssText = 'text-align:center;padding:6px 12px;font-size:.72rem;color:#94a3b8;font-style:italic;align-self:center';
    div.textContent = content;
  } else {
    div.className = 'contact-msg citizen';
    div.innerHTML = `<div class="contact-bubble">${_renderMsgContent(content)}</div><div class="contact-msg-meta">${time}</div>`;
  }

  msgsEl.appendChild(div);
  msgsEl.scrollTop = msgsEl.scrollHeight;
}

function appendContactSystemMsg(text) {
  const msgsEl = document.getElementById('contact-msgs');
  const div = document.createElement('div');
  div.style.cssText = 'text-align:center;padding:10px;font-size:.76rem;color:#94a3b8;font-style:italic';
  div.textContent = text;
  msgsEl.appendChild(div);
  msgsEl.scrollTop = msgsEl.scrollHeight;
}

async function sendContactMessage() {
  const inp = document.getElementById('contact-input');
  const text = inp.value.trim();
  if ((!text && !contactAttach) || !contactSessionId || contactClosed) return;

  inp.value = ''; inp.style.height = 'auto';
  let content = text;
  if (contactAttach) {
    content = (text ? text + '\n' : '') + `[IMG]${contactAttach}[/IMG]`;
    clearContactAttach();
  }

  try {
    await fetch(`${CONTACT_API}/${contactSessionId}/message`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ content })
    });
    await pollContactMessages();
  } catch {}
}

function contactKey(e) {
  if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); sendContactMessage(); }
}
function contactResize(el) {
  el.style.height = 'auto';
  el.style.height = Math.min(el.scrollHeight, 100) + 'px';
}
function escapeHtml(s) {
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/\n/g,'<br>');
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
  const photoEl = p.photo
    ? `<img src="${p.photo}" style="width:64px;height:64px;border-radius:50%;object-fit:cover;border:2px solid #005192">`
    : `<div style="width:64px;height:64px;border-radius:50%;background:#dbeafe;display:flex;align-items:center;justify-content:center;font-size:1.6rem;font-weight:700;color:#005192">${(p.name||'?').charAt(0).toUpperCase()}</div>`;

  sp.innerHTML = `
    <div class="sp-header">
      <button class="sp-back" onclick="renderSettingsHome()">${BACK_ARROW} Settings</button>
      <span class="sp-title">Personal Details</span>
      <span class="sp-spacer"></span>
    </div>
    <div class="sp-body">
      <p class="sp-form-hint">Stored only on this device. Used to personalise answers and pre-fill contact forms.</p>

      <div style="display:flex;align-items:center;gap:14px;margin-bottom:20px">
        <label for="pf-photo" style="cursor:pointer" title="Upload profile photo">
          ${photoEl}
        </label>
        <div>
          <div style="font-size:.8rem;font-weight:600;color:#1e293b">Profile Photo</div>
          <label for="pf-photo" style="font-size:.75rem;color:#005192;cursor:pointer;text-decoration:underline">Upload photo</label>
          ${p.photo ? `<span style="font-size:.75rem;color:#94a3b8"> · </span><button onclick="removeProfilePhoto()" style="font-size:.75rem;color:#dc2626;background:none;border:none;cursor:pointer;text-decoration:underline;padding:0">Remove</button>` : ''}
        </div>
        <input type="file" id="pf-photo" accept="image/*" style="display:none" onchange="onProfilePhotoSelected(this)">
      </div>

      <div class="sp-field">
        <label class="sp-field-label">Your name</label>
        <input id="pf-name" class="sp-field-input" type="text" placeholder="e.g. Sarah" value="${esc(p.name || '')}"/>
        <span class="sp-field-hint">Alex will greet and address you by name</span>
      </div>

      <div class="sp-field">
        <label class="sp-field-label">Email address</label>
        <input id="pf-email" class="sp-field-input" type="email" placeholder="your@email.com" value="${esc(p.email || '')}"/>
        <span class="sp-field-hint">Auto-filled when contacting staff — skip the typing</span>
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
      ${p.name || p.postcode || p.context || p.email
        ? `<button class="sp-clear-btn" onclick="clearProfile()">Clear all details</button>`
        : ''}
    </div>`;
}

function saveProfileFromForm() {
  const existing = getProfile() || {};
  const name     = document.getElementById('pf-name')?.value.trim();
  const email    = document.getElementById('pf-email')?.value.trim();
  const postcode = document.getElementById('pf-postcode')?.value.trim().toUpperCase();
  const context  = document.getElementById('pf-context')?.value.trim();
  saveProfile({ ...existing, name, email, postcode, context });
  sessionProfileInjected = false;
  showToast('Details saved!');
  renderSettingsHome();
}

function onProfilePhotoSelected(input) {
  const file = input.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = e => {
    const existing = getProfile() || {};
    saveProfile({ ...existing, photo: e.target.result });
    showToast('Photo saved!');
    renderProfileScreen();
  };
  reader.readAsDataURL(file);
  input.value = '';
}

function removeProfilePhoto() {
  const existing = getProfile() || {};
  delete existing.photo;
  saveProfile(existing);
  showToast('Photo removed');
  renderProfileScreen();
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
