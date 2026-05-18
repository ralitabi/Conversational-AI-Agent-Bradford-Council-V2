const API_BASE = (() => {
  const h = window.location.hostname;
  if (h === 'localhost' || h === '127.0.0.1') return 'http://localhost:5000';
  return '';  // Vercel proxy: /api/* → Railway (no CORS)
})();

const STORAGE_KEY = 'bca_admin_token_v2';

let token      = '';
let currentUser = null;
let currentPage = 1;
let searchTerm  = '';
let currentSid  = '';
let refreshTimer = null;
let searchTimer  = null;

// ── Sidebar (mobile) ─────────────────────────────────────────────────────────
function toggleSidebar() {
  const s = document.getElementById('sidebar');
  const b = document.getElementById('sidebar-backdrop');
  const open = s.classList.contains('mobile-open');
  s.classList.toggle('mobile-open', !open);
  b.classList.toggle('show', !open);
}
function closeSidebar() {
  document.getElementById('sidebar').classList.remove('mobile-open');
  document.getElementById('sidebar-backdrop').classList.remove('show');
}

// ── Boot ──────────────────────────────────────────────────────────────────────
(function init() {
  const saved = sessionStorage.getItem(STORAGE_KEY);
  if (saved) { token = saved; verifyToken(); }
  document.getElementById('login-pass').addEventListener('keydown', e => {
    if (e.key === 'Enter') doAdminLogin();
  });
  document.getElementById('login-user').addEventListener('keydown', e => {
    if (e.key === 'Enter') document.getElementById('login-pass').focus();
  });
})();

// ── Login ─────────────────────────────────────────────────────────────────────
async function doAdminLogin() {
  const username = document.getElementById('login-user').value.trim();
  const password = document.getElementById('login-pass').value;
  if (!username || !password) { showLoginErr('Please enter your username and password.'); return; }

  setSigninLoading(true);
  hideLoginErr();

  try {
    const r = await fetch(API_BASE + '/api/admin/auth', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    });

    if (r.status === 401) { showLoginErr('Incorrect username or password.'); setSigninLoading(false); return; }
    if (!r.ok) { showLoginErr('Server error. Please try again.'); setSigninLoading(false); return; }

    const data = await r.json();
    token = data.token;
    sessionStorage.setItem(STORAGE_KEY, token);
    currentUser = { name: data.name, username: data.username, role: data.role };
    setSigninLoading(false);
    showDashboard();
  } catch {
    showLoginErr('Cannot reach the API. Make sure the backend is running.');
    setSigninLoading(false);
  }
}

async function verifyToken() {
  try {
    const r = await apiFetch('/api/admin/me');
    if (!r.ok) { sessionStorage.removeItem(STORAGE_KEY); token = ''; return; }
    const d = await r.json();
    currentUser = { name: d.name, username: d.username, role: d.role };
    showDashboard();
  } catch {
    sessionStorage.removeItem(STORAGE_KEY); token = '';
  }
}

async function doAdminLogout() {
  try { await apiFetch('/api/admin/logout', { method: 'POST' }); } catch {}
  sessionStorage.removeItem(STORAGE_KEY);
  clearInterval(refreshTimer);
  token = ''; currentUser = null;
  document.getElementById('dashboard').style.display = 'none';
  document.getElementById('login-screen').style.display = '';
  document.getElementById('login-pass').value = '';
  document.body.classList.add('login-mode');
}

// ── UI helpers ────────────────────────────────────────────────────────────────
function showLoginErr(msg) {
  const el = document.getElementById('login-err');
  el.textContent = msg;
  el.classList.add('show');
}
function hideLoginErr() {
  document.getElementById('login-err').classList.remove('show');
}
function setSigninLoading(on) {
  const btn = document.getElementById('signin-btn');
  btn.disabled = on;
  btn.querySelector('.btn-text').style.display  = on ? 'none' : '';
  btn.querySelector('.btn-spinner').style.display = on ? '' : 'none';
}
function togglePw() {
  const inp = document.getElementById('login-pass');
  const icon = document.getElementById('eye-icon');
  const show = inp.type === 'password';
  inp.type = show ? 'text' : 'password';
  icon.innerHTML = show
    ? `<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/>`
    : `<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>`;
}

function showDashboard() {
  document.getElementById('login-screen').style.display = 'none';
  document.getElementById('dashboard').style.display = '';
  document.body.classList.remove('login-mode');

  if (currentUser) {
    document.getElementById('user-name').textContent   = currentUser.name;
    document.getElementById('user-role').textContent   = currentUser.role;
    document.getElementById('user-avatar').textContent = currentUser.name.charAt(0).toUpperCase();

    // Show Staff Activity nav only for superadmin
    if (currentUser.role === 'superadmin') {
      document.querySelectorAll('.superadmin-only').forEach(el => el.style.display = '');
    }
  }

  loadMyProfile();
  refreshAll();
  refreshTimer = setInterval(refreshAll, 30000);
}

// ── API helper ────────────────────────────────────────────────────────────────
function apiFetch(path, opts = {}) {
  return fetch(API_BASE + path, {
    ...opts,
    headers: {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer ' + token,
      ...(opts.headers || {})
    }
  });
}

// ── Navigation ────────────────────────────────────────────────────────────────
function showView(name) {
  ['overview', 'sessions', 'analytics', 'staff', 'support'].forEach(v => {
    const el = document.getElementById('view-' + v);
    const nav = document.getElementById('nav-' + v);
    if (el)  el.style.display  = v === name ? '' : 'none';
    if (nav) nav.classList.toggle('active', v === name);
  });
  const titles = { overview: 'Overview', sessions: 'Conversations', analytics: 'Analytics', staff: 'Staff Activity', support: 'Live Support' };
  document.getElementById('topbar-title').textContent = titles[name] || name;
  closeSidebar();
  if (name === 'sessions')  loadSessions(1);
  if (name === 'analytics') loadAnalytics();
  if (name === 'staff')     loadStaff();
  if (name === 'support')   loadSupport();
}

// ── Refresh ───────────────────────────────────────────────────────────────────
function refreshAll() {
  loadStats();
  loadOverviewSessions();
  refreshSupportBadge();
  document.getElementById('last-refresh').textContent =
    'Updated ' + new Date().toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
}

// ── Stats ─────────────────────────────────────────────────────────────────────
async function loadStats() {
  try {
    const r = await apiFetch('/api/admin/stats');
    if (!r.ok) return;
    const d = await r.json();
    set('s-totalSessions', d.totalSessions.toLocaleString());
    set('s-totalMessages', d.totalMessages.toLocaleString());
    set('s-sessionsToday', d.sessionsToday.toLocaleString());
    set('s-msgsToday',     d.msgsToday.toLocaleString());
    set('s-msgsWeek',      d.msgsThisWeek.toLocaleString());
    set('s-avgMsgs',       d.avgMsgsPerSession);
  } catch {}
}

// ── Overview recent sessions ──────────────────────────────────────────────────
async function loadOverviewSessions() {
  const wrap = document.getElementById('overview-sessions');
  try {
    const r = await apiFetch('/api/admin/sessions?page=1');
    if (!r.ok) { wrap.innerHTML = err('Failed to load.'); return; }
    const d = await r.json();
    wrap.innerHTML = renderTable(d.sessions, false);
  } catch { wrap.innerHTML = err('Error loading sessions.'); }
}

// ── Sessions list ─────────────────────────────────────────────────────────────
function searchSessions(val) {
  searchTerm = val;
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => loadSessions(1), 350);
}

async function loadSessions(page) {
  currentPage = page;
  const wrap = document.getElementById('sessions-table-wrap');
  wrap.innerHTML = '<div class="loading-row">Loading…</div>';
  try {
    const qs = new URLSearchParams({ page, ...(searchTerm ? { search: searchTerm } : {}) });
    const r  = await apiFetch('/api/admin/sessions?' + qs);
    if (!r.ok) { wrap.innerHTML = err('Failed to load.'); return; }
    const d = await r.json();
    wrap.innerHTML = renderTable(d.sessions, true);
    renderPagination(d.page, d.totalPages);
  } catch { wrap.innerHTML = err('Error loading sessions.'); }
}

function renderTable(sessions, showDel) {
  if (!sessions.length) return '<div class="loading-row">No conversations found.</div>';
  return `<table>
    <thead><tr>
      <th>Session ID</th><th>First Message</th><th>Questions</th>
      <th>Started</th><th>Last Active</th><th></th>
    </tr></thead>
    <tbody>${sessions.map(s => `
      <tr onclick="openDetail('${esc(s.sessionId)}')">
        <td class="td-sid">${esc(s.sessionId)}</td>
        <td class="td-preview">${esc((s.preview || '').slice(0, 80))}</td>
        <td class="td-count">${s.userMessages}</td>
        <td class="td-time">${fmtDate(s.firstSeen)}</td>
        <td class="td-time">${fmtDate(s.lastSeen)}</td>
        <td class="td-del">${showDel ? `
          <button class="del-btn" onclick="event.stopPropagation();delSession('${esc(s.sessionId)}')" title="Delete">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="13" height="13"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6M14 11v6"/><path d="M9 6V4h6v2"/></svg>
          </button>` : ''}</td>
      </tr>`).join('')}
    </tbody>
  </table>`;
}

function renderPagination(page, total) {
  const wrap = document.getElementById('sessions-pagination');
  if (total <= 1) { wrap.innerHTML = ''; return; }
  let html = '';
  if (page > 1) html += btn('← Prev', `loadSessions(${page - 1})`);
  for (let i = Math.max(1, page - 2); i <= Math.min(total, page + 2); i++)
    html += `<button class="page-btn${i === page ? ' active' : ''}" onclick="loadSessions(${i})">${i}</button>`;
  if (page < total) html += btn('Next →', `loadSessions(${page + 1})`);
  wrap.innerHTML = html;
}
function btn(label, fn) { return `<button class="page-btn" onclick="${fn}">${label}</button>`; }

// ── Detail panel ──────────────────────────────────────────────────────────────
async function openDetail(sessionId) {
  currentSid = sessionId;
  set('detail-sid', sessionId);
  document.getElementById('detail-messages').innerHTML = '<div class="loading-row">Loading…</div>';
  document.getElementById('detail-backdrop').classList.add('show');
  document.getElementById('detail-panel').classList.add('show');
  try {
    const r = await apiFetch('/api/admin/sessions/' + encodeURIComponent(sessionId));
    if (!r.ok) { document.getElementById('detail-messages').innerHTML = err('Not found.'); return; }
    const d = await r.json();
    document.getElementById('detail-messages').innerHTML = d.turns.map(t => `
      <div class="msg-wrap">
        <div class="msg-bubble ${t.role}">${esc(t.content)}</div>
        <div class="msg-meta ${t.role === 'user' ? 'right' : ''}">${fmtDateTime(t.timestamp)}</div>
      </div>`).join('');
  } catch { document.getElementById('detail-messages').innerHTML = err('Error loading.'); }
}

function closeDetail() {
  document.getElementById('detail-backdrop').classList.remove('show');
  document.getElementById('detail-panel').classList.remove('show');
  currentSid = '';
}

// ── Delete ────────────────────────────────────────────────────────────────────
async function delSession(sessionId) {
  if (!confirm('Delete this conversation? This cannot be undone.')) return;
  await apiFetch('/api/admin/sessions/' + encodeURIComponent(sessionId), { method: 'DELETE' });
  if (currentSid === sessionId) closeDetail();
  loadSessions(currentPage);
  loadOverviewSessions();
  loadStats();
}
function deleteCurrentSession() { if (currentSid) delSession(currentSid); }

async function confirmClearAll() {
  if (!confirm('Delete ALL conversations? This cannot be undone.')) return;
  await apiFetch('/api/admin/sessions', { method: 'DELETE' });
  loadSessions(1);
  loadOverviewSessions();
  loadStats();
}

// ── Profile ───────────────────────────────────────────────────────────────────
let myProfile = null;
let pendingAvatarBase64 = null;
let pendingAvatarMime   = null;

async function loadMyProfile() {
  try {
    const r = await apiFetch('/api/admin/profile');
    if (!r.ok) return;
    myProfile = await r.json();
    updateSidebarAvatar(myProfile);
  } catch {}
}

function updateSidebarAvatar(profile) {
  const el = document.getElementById('user-avatar');
  if (!el) return;
  if (profile?.avatarDataUrl) {
    el.innerHTML = `<img src="${profile.avatarDataUrl}" style="width:100%;height:100%;object-fit:cover;border-radius:50%"/>`;
    el.style.background = 'transparent';
  } else {
    el.innerHTML = (profile?.displayName || currentUser?.name || '?').charAt(0).toUpperCase();
    el.style.background = '';
  }
  if (profile?.displayName) set('user-name', profile.displayName);
}

function openProfile() {
  if (!myProfile) { loadMyProfile().then(() => _openProfilePanel()); return; }
  _openProfilePanel();
}

function _openProfilePanel() {
  const p = myProfile;
  set('profile-username', '@' + (currentUser?.username || ''));
  document.getElementById('profile-display-name').value = p?.displayName || currentUser?.name || '';
  document.getElementById('profile-role-display').value  = currentUser?.role || '';
  document.getElementById('profile-bio').value           = p?.bio || '';

  const avatarEl = document.getElementById('profile-avatar-display');
  if (p?.avatarDataUrl) {
    avatarEl.innerHTML = `<img src="${p.avatarDataUrl}" style="width:100%;height:100%;object-fit:cover"/>`;
    avatarEl.style.background = 'transparent';
    document.getElementById('remove-avatar-btn').style.display = '';
  } else {
    avatarEl.innerHTML = (p?.displayName || currentUser?.name || '?').charAt(0).toUpperCase();
    avatarEl.style.background = '';
    document.getElementById('remove-avatar-btn').style.display = 'none';
  }

  pendingAvatarBase64 = null;
  pendingAvatarMime   = null;
  hideProfileMsg();
  document.getElementById('profile-backdrop').classList.add('show');
  document.getElementById('profile-panel').classList.add('show');
}

function closeProfile() {
  document.getElementById('profile-backdrop').classList.remove('show');
  document.getElementById('profile-panel').classList.remove('show');
}

function onAvatarSelected(e) {
  const file = e.target.files[0];
  if (!file) return;
  if (file.size > 800_000) { showProfileMsg('Image too large. Please use an image under 600 KB.', false); return; }

  const reader = new FileReader();
  reader.onload = ev => {
    const dataUrl = ev.target.result;
    const [header, base64] = dataUrl.split(',');
    const mime = header.match(/data:([^;]+)/)?.[1] || 'image/jpeg';
    pendingAvatarBase64 = base64;
    pendingAvatarMime   = mime;

    const avatarEl = document.getElementById('profile-avatar-display');
    avatarEl.innerHTML = `<img src="${dataUrl}" style="width:100%;height:100%;object-fit:cover"/>`;
    avatarEl.style.background = 'transparent';
    document.getElementById('remove-avatar-btn').style.display = '';
  };
  reader.readAsDataURL(file);
  e.target.value = '';
}

async function removeAvatar() {
  pendingAvatarBase64 = null;
  pendingAvatarMime   = null;
  const avatarEl = document.getElementById('profile-avatar-display');
  avatarEl.innerHTML = (myProfile?.displayName || currentUser?.name || '?').charAt(0).toUpperCase();
  avatarEl.style.background = '';
  document.getElementById('remove-avatar-btn').style.display = 'none';

  try {
    await apiFetch('/api/admin/profile/avatar', { method: 'DELETE' });
    if (myProfile) myProfile.avatarDataUrl = null;
    updateSidebarAvatar(myProfile);
  } catch {}
}

async function saveProfile() {
  const btn = document.getElementById('save-profile-btn');
  btn.disabled = true;
  btn.textContent = 'Saving…';
  hideProfileMsg();

  try {
    // Upload avatar first if one was selected
    if (pendingAvatarBase64) {
      const ar = await apiFetch('/api/admin/profile/avatar', {
        method: 'POST',
        body: JSON.stringify({ base64: pendingAvatarBase64, mimeType: pendingAvatarMime })
      });
      if (!ar.ok) { showProfileMsg('Failed to upload photo.', false); btn.disabled = false; btn.textContent = 'Save Changes'; return; }
      const ad = await ar.json();
      if (myProfile) myProfile.avatarDataUrl = ad.avatarDataUrl;
    }

    // Save name + bio
    const pr = await apiFetch('/api/admin/profile', {
      method: 'PUT',
      body: JSON.stringify({
        displayName: document.getElementById('profile-display-name').value.trim(),
        bio:         document.getElementById('profile-bio').value.trim()
      })
    });
    if (!pr.ok) { showProfileMsg('Failed to save profile.', false); btn.disabled = false; btn.textContent = 'Save Changes'; return; }
    const pd = await pr.json();
    if (myProfile) { myProfile.displayName = pd.displayName; myProfile.bio = pd.bio; }
    updateSidebarAvatar(myProfile);
    showProfileMsg('Profile saved successfully!', true);
    pendingAvatarBase64 = null;
    pendingAvatarMime   = null;
  } catch { showProfileMsg('An error occurred. Please try again.', false); }

  btn.disabled = false;
  btn.textContent = 'Save Changes';
}

function showProfileMsg(msg, ok) {
  const el = document.getElementById('profile-msg');
  el.textContent = msg;
  el.className = ok ? 'profile-msg-ok' : 'profile-msg-err';
  el.style.display = 'block';
}
function hideProfileMsg() { document.getElementById('profile-msg').style.display = 'none'; }

// ── Staff Activity (superadmin only) ─────────────────────────────────────────
async function loadStaff() {
  document.getElementById('staff-cards').innerHTML = '<div class="loading-row">Loading…</div>';
  document.getElementById('activity-feed').innerHTML = '<tr><td colspan="5" class="loading-row">Loading…</td></tr>';
  try {
    const [staffRes, profilesRes] = await Promise.all([
      apiFetch('/api/admin/staff'),
      apiFetch('/api/admin/profiles')
    ]);
    if (staffRes.status === 403) { document.getElementById('staff-cards').innerHTML = err('Access denied.'); return; }
    if (!staffRes.ok) { document.getElementById('staff-cards').innerHTML = err('Failed to load.'); return; }
    const d        = await staffRes.json();
    const profiles = profilesRes.ok ? await profilesRes.json() : [];
    renderStaffCards(d.staff, profiles);
    renderActivityFeed(d.recentActivity);
  } catch { document.getElementById('staff-cards').innerHTML = err('Error loading staff data.'); }
}

const AVATAR_COLOURS = ['#005192','#0891b2','#16a34a','#7c3aed','#F26C11','#dc2626','#0f766e'];

function renderStaffCards(staff, profiles = []) {
  const wrap = document.getElementById('staff-cards');
  if (!staff.length) { wrap.innerHTML = '<div class="loading-row">No staff activity recorded yet.</div>'; return; }
  wrap.innerHTML = staff.map((s, i) => {
    const colour    = AVATAR_COLOURS[i % AVATAR_COLOURS.length];
    const initial   = (s.name || s.username).charAt(0).toUpperCase();
    const roleClass = s.username === 'admin' ? 'role-superadmin' : 'role-admin';
    const roleLbl   = s.username === 'admin' ? 'Super Admin' : 'Admin';
    const profile   = profiles.find(p => p.username === s.username);
    const bio       = profile?.bio ? `<div class="staff-bio">${esc(profile.bio)}</div>` : '';
    const lastLogin = s.lastLogin ? fmtDateTime(s.lastLogin) : 'Never';
    const lastActive= fmtDateTime(s.lastActive);
    const avatarHtml = profile?.avatarDataUrl
      ? `<img src="${profile.avatarDataUrl}" style="width:100%;height:100%;object-fit:cover;border-radius:50%"/>`
      : esc(initial);
    const avatarBg = profile?.avatarDataUrl ? 'transparent' : colour;

    return `
    <div class="staff-card">
      <div class="staff-card-header">
        <div class="staff-avatar" style="background:${avatarBg}">${avatarHtml}</div>
        <div>
          <div class="staff-name">${esc(s.name)}</div>
          <div class="staff-username">@${esc(s.username)}</div>
          <div class="staff-last">Last active: ${lastActive}</div>
        </div>
      </div>
      <div class="staff-metrics">
        <div class="staff-metric highlight">
          <div class="staff-metric-val">${s.totalLogins}</div>
          <div class="staff-metric-lbl">Logins</div>
        </div>
        <div class="staff-metric ${s.failedLogins > 0 ? 'danger' : ''}">
          <div class="staff-metric-val">${s.failedLogins}</div>
          <div class="staff-metric-lbl">Failed Logins</div>
        </div>
        <div class="staff-metric">
          <div class="staff-metric-val">${s.sessionsViewed}</div>
          <div class="staff-metric-lbl">Sessions Viewed</div>
        </div>
        <div class="staff-metric ${s.sessionsDeleted > 0 ? 'danger' : ''}">
          <div class="staff-metric-val">${s.sessionsDeleted + s.deleteAlls}</div>
          <div class="staff-metric-lbl">Deletions</div>
        </div>
      </div>
      ${bio}
      <div class="staff-divider"></div>
      <div class="staff-bottom">
        <span class="staff-role-badge ${roleClass}">${roleLbl}</span>
        <span class="staff-total">${s.totalActions} total actions &nbsp;·&nbsp; Last login: ${lastLogin}</span>
      </div>
    </div>`;
  }).join('');
}

const ACTION_LABELS = {
  login:              'Signed In',
  logout:             'Signed Out',
  login_failed:       'Failed Login',
  view_session:       'Viewed Conversation',
  view_conversations: 'Browsed Conversations',
  view_analytics:     'Viewed Analytics',
  delete_session:     'Deleted Conversation',
  delete_all:         'Cleared All Data',
};

function renderActivityFeed(activity) {
  const tbody = document.getElementById('activity-feed');
  if (!activity.length) { tbody.innerHTML = '<tr><td colspan="5" class="loading-row">No activity recorded yet.</td></tr>'; return; }
  tbody.innerHTML = activity.map(a => {
    const label    = ACTION_LABELS[a.action] || a.action;
    const chipCls  = 'action-chip action-' + (a.action || 'unknown');
    const detail   = a.detail ? `<span style="font-size:.75rem;color:var(--muted)">${esc(a.detail)}</span>` : '—';
    return `<tr>
      <td><strong>${esc(a.name)}</strong><br><span style="font-size:.74rem;color:var(--muted)">@${esc(a.username)}</span></td>
      <td><span class="${chipCls}">${esc(label)}</span></td>
      <td>${detail}</td>
      <td class="td-ip">${esc(a.ipAddress || '—')}</td>
      <td class="td-time">${fmtDateTime(a.timestamp)}</td>
    </tr>`;
  }).join('');
}

// ── Analytics ─────────────────────────────────────────────────────────────────
let analyticsDays = 30;
let chartTrend = null, chartHours = null, chartDow = null;

function setPeriod(days, btn) {
  analyticsDays = days;
  document.querySelectorAll('.period-btn').forEach(b => b.classList.remove('active'));
  btn.classList.add('active');
  loadAnalytics();
}

async function loadAnalytics() {
  try {
    const r = await apiFetch('/api/admin/analytics?days=' + analyticsDays);
    if (!r.ok) return;
    const d = await r.json();
    renderEngagement(d.engagement);
    renderTrendChart(d.trends);
    renderTopics(d.topicCounts);
    renderHoursChart(d.peakHours);
    renderDowChart(d.dayOfWeek);
  } catch {}
}

function renderEngagement(e) {
  set('an-total',   e.total.toLocaleString());
  set('an-engaged', e.engaged.toLocaleString());
  set('an-single',  e.singleQuery.toLocaleString());
  set('an-rate',    e.engagedPct + '%');
}

function renderTrendChart(trends) {
  const labels = trends.map(t => {
    const d = new Date(t.date);
    return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short' });
  });
  const msgs  = trends.map(t => t.messages);
  const sess  = trends.map(t => t.sessions);
  const total = msgs.reduce((a, b) => a + b, 0);
  set('trend-sub', total.toLocaleString() + ' messages in selected period');

  if (chartTrend) chartTrend.destroy();
  chartTrend = new Chart(document.getElementById('chart-trend'), {
    type: 'line',
    data: {
      labels,
      datasets: [
        {
          label: 'Messages', data: msgs, borderColor: '#005192', backgroundColor: 'rgba(0,81,146,.08)',
          borderWidth: 2, tension: 0.35, fill: true, pointRadius: 2, pointHoverRadius: 5
        },
        {
          label: 'Sessions', data: sess, borderColor: '#F26C11', backgroundColor: 'transparent',
          borderWidth: 2, tension: 0.35, fill: false, pointRadius: 2, pointHoverRadius: 5,
          borderDash: [4, 3]
        }
      ]
    },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: { legend: { position: 'top', labels: { boxWidth: 12, font: { size: 11 } } } },
      scales: {
        x: { grid: { display: false }, ticks: { font: { size: 10 }, maxTicksLimit: 10 } },
        y: { beginAtZero: true, grid: { color: '#f1f5f9' }, ticks: { font: { size: 10 }, precision: 0 } }
      }
    }
  });
}

function renderTopics(topics) {
  const wrap = document.getElementById('topics-list');
  if (!topics.length) { wrap.innerHTML = '<div class="loading-row">No data yet.</div>'; return; }
  const max = topics[0].count;
  const colours = ['#005192','#0891b2','#16a34a','#7c3aed','#F26C11','#dc2626','#0f766e','#9333ea','#ca8a04','#be185d'];
  wrap.innerHTML = topics.slice(0, 10).map((t, i) => `
    <div class="topic-row">
      <div class="topic-meta">
        <span class="topic-name">${esc(t.topic)}</span>
        <span class="topic-count">${t.count}</span>
      </div>
      <div class="topic-bar-bg">
        <div class="topic-bar-fill" style="width:${Math.round(t.count/max*100)}%;background:${colours[i % colours.length]}"></div>
      </div>
    </div>`).join('');
}

function renderHoursChart(hours) {
  const labels = hours.map(h => h.label);
  const counts = hours.map(h => h.count);
  const max    = Math.max(...counts);
  const bgs    = counts.map(c => c === max ? '#005192' : 'rgba(0,81,146,.25)');

  if (chartHours) chartHours.destroy();
  chartHours = new Chart(document.getElementById('chart-hours'), {
    type: 'bar',
    data: { labels, datasets: [{ label: 'Questions', data: counts, backgroundColor: bgs, borderRadius: 3 }] },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        x: { grid: { display: false }, ticks: { font: { size: 9 }, maxRotation: 0 } },
        y: { beginAtZero: true, grid: { color: '#f1f5f9' }, ticks: { font: { size: 10 }, precision: 0 } }
      }
    }
  });
}

function renderDowChart(dow) {
  const max = Math.max(...dow.map(d => d.count));
  const bgs = dow.map(d => d.count === max ? '#F26C11' : 'rgba(242,108,17,.3)');

  if (chartDow) chartDow.destroy();
  chartDow = new Chart(document.getElementById('chart-dow'), {
    type: 'bar',
    data: {
      labels: dow.map(d => d.day),
      datasets: [{ label: 'Questions', data: dow.map(d => d.count), backgroundColor: bgs, borderRadius: 3 }]
    },
    options: {
      responsive: true, maintainAspectRatio: false,
      plugins: { legend: { display: false } },
      scales: {
        x: { grid: { display: false }, ticks: { font: { size: 11 } } },
        y: { beginAtZero: true, grid: { color: '#f1f5f9' }, ticks: { font: { size: 10 }, precision: 0 } }
      }
    }
  });
}

// ── Utils ─────────────────────────────────────────────────────────────────────
function esc(s) {
  return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
function set(id, val) { const el = document.getElementById(id); if (el) el.textContent = val; }
function err(msg) { return `<div class="loading-row">${msg}</div>`; }
function fmtDate(iso) {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}
function fmtDateTime(iso) {
  if (!iso) return '';
  return new Date(iso).toLocaleString('en-GB', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
}

// ══════════════════════════════════════════════════════════════════════════════
// LIVE SUPPORT
// ══════════════════════════════════════════════════════════════════════════════
let supportFilter     = 'all';
let supportSessions   = [];
let activeSupportId   = null;
let supportMsgLast    = 0;
let supportPollTimer  = null;
let supportListTimer  = null;

async function loadSupport() {
  stopSupportPolling();
  activeSupportId = null;
  document.getElementById('support-chat-empty').style.display  = '';
  document.getElementById('support-chat-active').style.display = 'none';
  await loadSupportSessions();
  supportListTimer = setInterval(loadSupportSessions, 8000);
}

async function loadSupportSessions() {
  const wrap = document.getElementById('support-sessions-list');
  try {
    const qs = supportFilter !== 'all' ? '?status=' + supportFilter : '';
    const r  = await apiFetch('/api/contact/admin/sessions' + qs);
    if (!r.ok) { wrap.innerHTML = '<div class="support-empty-list">Failed to load sessions.</div>'; return; }
    supportSessions = await r.json();
    renderSupportSessions();
    refreshSupportBadge();
  } catch {
    wrap.innerHTML = '<div class="support-empty-list">Error loading sessions.</div>';
  }
}

function renderSupportSessions() {
  const wrap = document.getElementById('support-sessions-list');
  if (!supportSessions.length) {
    wrap.innerHTML = '<div class="support-empty-list">No sessions found.</div>';
    return;
  }
  wrap.innerHTML = supportSessions.map(s => {
    const selected = s.id === activeSupportId ? ' selected' : '';
    const unreadEl = s.unread > 0 ? `<span class="ss-unread">${s.unread}</span>` : '';
    const preview  = esc(s.preview || '');
    const when     = fmtDateTime(s.lastAt || s.updatedAt);
    return `
      <div class="support-session-item${selected}" onclick="openSupportChat('${esc(s.id)}')">
        <div class="ss-header">
          <span class="ss-name">${esc(s.name)}</span>
          <span class="ss-time">${when}</span>
        </div>
        <div class="ss-subject">${esc(s.subject)}</div>
        <div class="ss-preview">${preview}</div>
        <div class="ss-footer">
          <span class="support-status-badge status-${s.status}">${s.status}</span>
          ${unreadEl}
        </div>
      </div>`;
  }).join('');
}

async function refreshSupportBadge() {
  try {
    const r = await apiFetch('/api/contact/admin/sessions');
    if (!r.ok) return;
    const sessions = await r.json();
    const total = sessions.reduce((sum, s) => sum + (s.unread || 0), 0);
    const badge = document.getElementById('support-badge');
    if (badge) {
      badge.textContent = total > 0 ? total : '0';
      badge.style.background = total > 0 ? 'var(--orange)' : '';
      badge.style.color      = total > 0 ? '#fff' : '';
    }
  } catch {}
}

function setSupportFilter(status, btn) {
  supportFilter = status;
  document.querySelectorAll('.support-filter-btn').forEach(b => b.classList.remove('active'));
  if (btn) btn.classList.add('active');
  loadSupportSessions();
}

async function openSupportChat(sessionId) {
  activeSupportId = sessionId;
  supportMsgLast  = 0;
  renderSupportSessions();

  document.getElementById('support-chat-empty').style.display  = 'none';
  document.getElementById('support-chat-active').style.display = '';
  document.getElementById('support-msgs').innerHTML = '<div class="sup-system">Loading messages…</div>';

  stopSupportPolling();
  await pollSupportMessages();
  supportPollTimer = setInterval(pollSupportMessages, 2500);
}

async function pollSupportMessages() {
  if (!activeSupportId) return;
  try {
    const r = await apiFetch(`/api/contact/admin/${activeSupportId}/messages?after=${supportMsgLast}`);
    if (!r.ok) return;
    const d = await r.json();

    if (supportMsgLast === 0) {
      document.getElementById('support-msgs').innerHTML = '';
      const session = d.session;
      if (session) {
        document.getElementById('support-chat-name').textContent    = session.name;
        document.getElementById('support-chat-subject').textContent = session.subject;
        const metaParts = [];
        if (session.email) metaParts.push(session.email);
        if (session.phone) metaParts.push(session.phone);
        document.getElementById('support-chat-meta').textContent = metaParts.join(' · ');
      }
      updateSupportSessionStatus(d.status);
    }

    d.messages.forEach(m => {
      appendSupportMsg(m);
      supportMsgLast = Math.max(supportMsgLast, m.id);
    });

    updateSupportSessionStatus(d.status);

    const msgs = document.getElementById('support-msgs');
    msgs.scrollTop = msgs.scrollHeight;

    if (d.messages.length > 0) {
      loadSupportSessions();
    }
  } catch {}
}

function renderMsgContent(text) {
  if (!text) return '';
  const parts = text.split(/(\[IMG\][\s\S]*?\[\/IMG\])/);
  return parts.map(p => {
    if (p.startsWith('[IMG]') && p.endsWith('[/IMG]')) {
      const src = p.slice(5, -6);
      return `<img src="${src}" style="max-width:200px;max-height:160px;border-radius:8px;margin-top:6px;display:block;cursor:pointer" onclick="window.open(this.src,'_blank')">`;
    }
    return esc(p).replace(/\n/g, '<br>');
  }).join('');
}

function appendSupportMsg(m) {
  const msgs = document.getElementById('support-msgs');
  const div  = document.createElement('div');
  div.className = `sup-msg ${m.sender}`;
  div.innerHTML = `
    <div class="sup-bubble">${renderMsgContent(m.content)}</div>
    <div class="sup-meta">${esc(m.senderName)} · ${fmtDateTime(m.timestamp)}</div>`;
  msgs.appendChild(div);
}

function updateSupportSessionStatus(status) {
  const closeBtn  = document.getElementById('support-close-btn');
  const reopenBtn = document.getElementById('support-reopen-btn');
  const input     = document.getElementById('support-input');
  const sendBtn   = document.getElementById('support-send-btn');
  const closedBar = document.getElementById('support-closed-bar');

  const badge = document.getElementById('support-current-badge');
  if (badge) {
    badge.className = `support-status-badge status-${status}`;
    badge.textContent = status;
  }

  if (status === 'closed') {
    if (closeBtn)  closeBtn.style.display  = 'none';
    if (reopenBtn) reopenBtn.style.display = '';
    if (input)  { input.disabled = true; input.placeholder = 'Session closed'; }
    if (sendBtn)   sendBtn.disabled = true;
    if (closedBar) closedBar.style.display = '';
  } else {
    if (closeBtn)  closeBtn.style.display  = '';
    if (reopenBtn) reopenBtn.style.display = 'none';
    if (input)  { input.disabled = false; input.placeholder = 'Type a reply…'; }
    if (sendBtn)   sendBtn.disabled = false;
    if (closedBar) closedBar.style.display = 'none';
  }
}

async function sendSupportMessage() {
  if (!activeSupportId) return;
  const input = document.getElementById('support-input');
  const content = input.value.trim();
  if (!content) return;

  const sendBtn = document.getElementById('support-send-btn');
  sendBtn.disabled = true;
  input.value = '';
  supportResize(input);

  try {
    const r = await apiFetch(`/api/contact/admin/${activeSupportId}/message`, {
      method: 'POST',
      body: JSON.stringify({ content })
    });
    if (!r.ok) {
      input.value = content;
      supportResize(input);
    } else {
      await pollSupportMessages();
    }
  } catch {
    input.value = content;
    supportResize(input);
  }
  sendBtn.disabled = false;
}

async function closeContactSession() {
  if (!activeSupportId) return;
  try {
    await apiFetch(`/api/contact/admin/${activeSupportId}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status: 'closed' })
    });
    const msgs = document.getElementById('support-msgs');
    const sys  = document.createElement('div');
    sys.className = 'sup-system';
    sys.textContent = 'Session closed by admin';
    msgs.appendChild(sys);
    msgs.scrollTop = msgs.scrollHeight;
    updateSupportSessionStatus('closed');
    loadSupportSessions();
  } catch {}
}

async function reopenContactSession() {
  if (!activeSupportId) return;
  try {
    await apiFetch(`/api/contact/admin/${activeSupportId}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status: 'active' })
    });
    const msgs = document.getElementById('support-msgs');
    const sys  = document.createElement('div');
    sys.className = 'sup-system';
    sys.textContent = 'Session reopened by admin';
    msgs.appendChild(sys);
    msgs.scrollTop = msgs.scrollHeight;
    updateSupportSessionStatus('active');
    loadSupportSessions();
  } catch {}
}

function stopSupportPolling() {
  clearInterval(supportPollTimer);
  clearInterval(supportListTimer);
  supportPollTimer = null;
  supportListTimer = null;
}

function supportKey(e) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    sendSupportMessage();
  }
}

function supportResize(el) {
  el.style.height = 'auto';
  el.style.height = Math.min(el.scrollHeight, 120) + 'px';
}
