using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using X_ChemicalStorage.BioBank.Services;

namespace X_ChemicalStorage.Controllers
{
    /// صفحة اختبار وإدارة مؤقتة لنظام الـ IoT (أزرار بدل Console وSQL).
    /// تتفعّل بس لو في appsettings: "IoT": { "TestConsole": true }. تتقفل قبل الإنتاج.
    [Authorize]
    public class IoTConsoleController : Controller
    {
        private readonly IConfiguration _cfg;
        private readonly IFreezerIoTService _iot;

        public IoTConsoleController(IConfiguration cfg, IFreezerIoTService iot)
        {
            _cfg = cfg;
            _iot = iot;
        }

        private bool Enabled => string.Equals(_cfg["IoT:TestConsole"], "true", StringComparison.OrdinalIgnoreCase);

        [HasPermission(Permissions.IoT.View)]
        [HttpGet("biobank/iot-console")]
        public IActionResult Index()
            => Enabled ? Content(Html, "text/html; charset=utf-8") : NotFound();

        /// كل الإنذارات المفتوحة (كل الفريزرات)
        [HasPermission(Permissions.IoT.View)]
        [HttpGet("biobank/api/iot/alarms")]
        public async Task<IActionResult> OpenAlarms()
            => Enabled ? Ok(await _iot.GetActiveAlarmsAsync(null)) : NotFound();

        private const string Html = """"
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>BioBank IoT Console (TEMP)</title>
<style>
  :root { --bg:#f4f6f8; --card:#fff; --line:#dde3ea; --txt:#1f2933; --mut:#6b7785; --blue:#1f6feb; }
  * { box-sizing:border-box; }
  body { margin:0; font:14px/1.45 system-ui,Segoe UI,Arial,sans-serif; background:var(--bg); color:var(--txt); }
  header { background:#12263f; color:#fff; padding:12px 20px; display:flex; gap:16px; align-items:center; flex-wrap:wrap; }
  header h1 { font-size:17px; margin:0; font-weight:600; }
  header .tag { background:#d9480f; padding:2px 8px; border-radius:10px; font-size:11px; }
  header label { margin-left:auto; font-size:13px; }
  main { padding:16px 20px; display:grid; gap:16px; max-width:1500px; margin:auto; }
  .card { background:var(--card); border:1px solid var(--line); border-radius:8px; padding:14px 16px; }
  .card h2 { font-size:15px; margin:0 0 10px; }
  .row { display:flex; gap:8px; flex-wrap:wrap; align-items:center; }
  .counts { display:flex; gap:10px; flex-wrap:wrap; }
  .cnt { min-width:110px; padding:10px 12px; border-radius:8px; color:#fff; }
  .cnt b { display:block; font-size:22px; }
  table { width:100%; border-collapse:collapse; }
  th, td { text-align:left; padding:6px 8px; border-bottom:1px solid var(--line); vertical-align:top; }
  th { color:var(--mut); font-weight:600; font-size:12px; white-space:nowrap; }
  tr.sel { background:#e8f0fe; }
  button { cursor:pointer; border:1px solid #b9c3cf; background:#fff; border-radius:6px; padding:5px 10px; font:inherit; }
  button:hover { background:#eef2f7; }
  button.pri { background:var(--blue); border-color:var(--blue); color:#fff; }
  button.warn { background:#d9480f; border-color:#d9480f; color:#fff; }
  button.sm { padding:2px 8px; font-size:12px; }
  input, select { font:inherit; padding:5px 7px; border:1px solid #b9c3cf; border-radius:6px; }
  input[type=number] { width:90px; }
  .st { display:inline-block; padding:1px 8px; border-radius:10px; color:#fff; font-size:12px; white-space:nowrap; }
  .Normal{background:#2f9e44} .HighTemp{background:#e03131} .LowTemp{background:#1971c2}
  .Abnormal{background:#f08c00} .Offline{background:#868e96} .Commissioning{background:#7048e8}
  .mut { color:var(--mut); } .ok { color:#2f9e44; } .bad { color:#e03131; }
  #log { height:150px; overflow:auto; background:#0f1720; color:#cfe3ff; font:12px/1.5 Consolas,monospace; padding:8px; border-radius:6px; white-space:pre-wrap; }
  .grid2 { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
  @media (max-width:1000px){ .grid2{grid-template-columns:1fr;} }
  .banner { background:#fff3bf; border:1px solid #ffd43b; padding:8px 12px; border-radius:6px; }
  .field { display:flex; flex-direction:column; gap:3px; font-size:12px; color:var(--mut); }
  .scroll { max-height:300px; overflow:auto; }
</style>
</head>
<body>
<header>
  <h1>BioBank IoT Console</h1><span class="tag">TEMP - for testing only</span>
  <label><input type="checkbox" id="auto" checked> auto-refresh (10s)</label>
  <button id="refreshAll">Refresh now</button>
</header>
<main>

  <section class="card">
    <h2>Freezers <span class="mut" id="dashTime"></span></h2>
    <div class="counts" id="counts"></div>
    <div class="scroll" style="margin-top:10px"><table>
      <thead><tr><th>Freezer</th><th>Type</th><th>State</th><th>Temp</th><th>Limits</th><th>Last data</th><th>Unacked</th><th>Collector</th><th></th></tr></thead>
      <tbody id="freezers"></tbody>
    </table></div>
  </section>

  <section class="card">
    <h2>Scenario simulator <span class="mut" id="selName">(select a freezer above)</span></h2>
    <div class="row" id="scenarioBtns">
      <button data-k="normal">Normal temp</button>
      <button data-k="low">Low temp</button>
      <button data-k="high">High temp</button>
      <button data-k="door-open">Door open</button>
      <button data-k="door-closed">Door closed</button>
      <button data-k="power-fail">Power fail</button>
      <button data-k="power-ok">Power OK</button>
      <span class="mut">(each button sends ONE reading)</span>
    </div>
    <div class="row" style="margin-top:10px">
      <b>Keep sending:</b>
      <select id="simKind">
        <option value="normal">Normal temp</option><option value="low">Low temp</option><option value="high">High temp</option>
        <option value="door-open">Door open</option><option value="power-fail">Power fail</option>
      </select>
      every <input type="number" id="simSec" value="20" min="5"> seconds
      <button class="pri" id="simStart">Start</button>
      <button class="warn" id="simStop">Stop (= freezer goes silent, tests Offline)</button>
    </div>
    <div id="simBanner" class="banner" style="display:none;margin-top:10px"></div>
    <p class="mut" style="margin:8px 0 0">Alarms need the condition to last (default 10 min; use "Test timings" in Settings below to make it 1 min). Keep this tab open while the simulator runs.</p>
  </section>

  <div class="grid2">
    <section class="card">
      <h2>Open alarms</h2>
      <div class="scroll"><table>
        <thead><tr><th>#</th><th>Freezer</th><th>Type</th><th>Lvl</th><th>Since</th><th>State</th><th></th></tr></thead>
        <tbody id="alarms"></tbody>
      </table></div>
    </section>

    <section class="card">
      <h2>Notifications log (latest 30)</h2>
      <div class="scroll"><table>
        <thead><tr><th>Time</th><th>Kind</th><th>Freezer</th><th>To</th><th>Via</th><th>Status</th></tr></thead>
        <tbody id="notifs"></tbody>
      </table></div>
    </section>
  </div>

  <div class="grid2">
    <section class="card">
      <h2>Alarm contacts</h2>
      <div class="scroll"><table>
        <thead><tr><th>Name</th><th>Tier</th><th>Phone</th><th>Email</th><th>Telegram</th><th>Freezer</th><th>Active</th><th></th></tr></thead>
        <tbody id="contacts"></tbody>
      </table></div>
      <h2 style="margin-top:14px">Add contact</h2>
      <div class="row">
        <div class="field">Name<input id="cName"></div>
        <div class="field">Phone (01012345678)<input id="cPhone"></div>
        <div class="field">Email<input id="cEmail"></div>
        <div class="field">Telegram chat id<input id="cTg"></div>
        <div class="field">Tier<select id="cTier"><option value="1">1 (first)</option><option value="2">2 (escalation)</option></select></div>
        <div class="field">Freezer (empty = all)<input id="cFz" placeholder="e.g. FZ150-01"></div>
        <button class="pri" id="cAdd" style="align-self:flex-end">Add</button>
      </div>
    </section>

    <section class="card">
      <h2>Settings <span class="mut" id="setName">(select a freezer)</span></h2>
      <div class="row">
        <div class="field">High limit &deg;C<input type="number" step="0.1" id="sHigh"></div>
        <div class="field">Low limit &deg;C<input type="number" step="0.1" id="sLow"></div>
        <div class="field">Temp delay (min)<input type="number" id="sDelay"></div>
        <div class="field">Door open (min)<input type="number" id="sDoor"></div>
        <div class="field">Offline after (min)<input type="number" id="sOff"></div>
        <div class="field">Escalate after (min)<input type="number" id="sEsc"></div>
        <div class="field">Commissioning<select id="sComm"><option value="true">ON (no alarms)</option><option value="false">OFF (alarms active)</option></select></div>
      </div>
      <div class="row" style="margin-top:10px">
        <button class="pri" id="sSave">Save</button>
        <button id="pTest">Test timings (1/1/30/2)</button>
        <button id="pNormal">Normal timings (10/5/2/10)</button>
        <span id="sMsg"></span>
      </div>
      <h2 style="margin-top:14px">Change history</h2>
      <div class="scroll" style="max-height:160px"><table>
        <thead><tr><th>When</th><th>Who</th><th>Field</th><th>From</th><th>To</th></tr></thead>
        <tbody id="audit"></tbody>
      </table></div>
    </section>
  </div>

  <section class="card">
    <h2>Log</h2>
    <div id="log"></div>
  </section>
</main>

<script>
'use strict';
const $ = s => document.querySelector(s);
const state = { freezers: [], sel: null, sim: null };

// ---------- helpers ----------
function esc(v) { return String(v == null ? '' : v).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c])); }
function toDate(s) { if (!s) return null; return new Date(/(Z|[+-]\d\d:\d\d)$/.test(s) ? s : s + 'Z'); }
function fmt(s) { const d = toDate(s); return d ? d.toLocaleString('en-GB', { hour12: false }) : ''; }
function ago(s) {
  const d = toDate(s); if (!d) return 'never';
  const sec = Math.max(0, Math.round((Date.now() - d.getTime()) / 1000));
  if (sec < 90) return sec + 's ago'; if (sec < 5400) return Math.round(sec / 60) + ' min ago';
  if (sec < 172800) return Math.round(sec / 3600) + ' h ago'; return Math.round(sec / 86400) + ' d ago';
}
function log(msg, cls) {
  const el = $('#log'); const t = new Date().toLocaleTimeString('en-GB');
  el.textContent += '[' + t + '] ' + msg + '\n'; el.scrollTop = el.scrollHeight;
}
async function api(method, url, body) {
  const r = await fetch(url, { method, credentials: 'same-origin',
    headers: body ? { 'Content-Type': 'application/json' } : {}, body: body ? JSON.stringify(body) : undefined });
  if (r.redirected && /login/i.test(r.url)) throw new Error('Not logged in - open the app and sign in first.');
  let data = null; try { data = await r.json(); } catch (e) { }
  if (!r.ok) throw new Error((data && data.message) || ('HTTP ' + r.status));
  return data;
}
function getSel() { return state.freezers.find(f => f.freezerId === state.sel); }

// ---------- freezers ----------
async function loadDashboard() {
  try {
    const d = await api('GET', '/biobank/api/iot/dashboard?days=7');
    state.freezers = d.devices || [];
    const c = d.counts;
    const items = [['All', c.all, '#495057'], ['Normal', c.normal, '#2f9e44'], ['High', c.highTemp, '#e03131'], ['Low', c.lowTemp, '#1971c2'],
      ['Abnormal', c.abnormal, '#f08c00'], ['Offline', c.offline, '#868e96'], ['Commissioning', c.commissioning, '#7048e8']];
    $('#counts').innerHTML = items.map(i => '<div class="cnt" style="background:' + i[2] + '">' + i[0] + '<b>' + i[1] + '</b></div>').join('');
    $('#dashTime').textContent = ' - updated ' + new Date().toLocaleTimeString('en-GB');
    renderFreezers();
  } catch (e) { log('Dashboard: ' + e.message); $('#dashTime').textContent = ' - ERROR: ' + e.message; }
}
function renderFreezers() {
  $('#freezers').innerHTML = state.freezers.map(f =>
    '<tr class="' + (f.freezerId === state.sel ? 'sel' : '') + '">' +
    '<td><b>' + esc(f.freezerId) + '</b></td><td>' + esc(f.type) + '</td>' +
    '<td><span class="st ' + esc(f.state) + '">' + esc(f.state) + '</span></td>' +
    '<td>' + (f.temperature == null ? '-' : Number(f.temperature).toFixed(1) + ' &deg;C') + '</td>' +
    '<td class="mut">' + esc(f.lowTempLimit) + ' ~ ' + esc(f.highTempLimit) + '</td>' +
    '<td title="' + esc(fmt(f.lastReadingAt)) + '">' + esc(ago(f.lastReadingAt)) + '</td>' +
    '<td>' + (f.unacknowledgedAlarms ? '<b class="bad">' + f.unacknowledgedAlarms + '</b>' : '0') + '</td>' +
    '<td class="mut">' + esc(f.collectorCode || '-') + '</td>' +
    '<td><button class="sm pri" data-sel="' + esc(f.freezerId) + '">Select</button></td></tr>').join('')
    || '<tr><td colspan="9" class="mut">No monitored freezers (link a collector first).</td></tr>';
}
async function selectFreezer(id) {
  state.sel = id; renderFreezers();
  $('#selName').textContent = '- ' + id; $('#setName').textContent = '- ' + id;
  await loadSettings();
}

// ---------- scenarios ----------
function scenario(f, kind) {
  const mid = Number(((f.highTempLimit + f.lowTempLimit) / 2).toFixed(1));
  const p = { temp: mid, door: 'Closed', power: 'OK' };
  if (kind === 'low') p.temp = Number((f.lowTempLimit - 10).toFixed(1));
  if (kind === 'high') p.temp = Number((f.highTempLimit + 10).toFixed(1));
  if (kind === 'door-open') p.door = 'Open';
  if (kind === 'power-fail') p.power = 'Fail';
  return p;
}
async function sendReading(kind, quiet) {
  const f = getSel(); if (!f) { alert('Select a freezer first.'); return false; }
  if (!f.collectorCode) { alert('This freezer has no linked collector.'); return false; }
  const p = scenario(f, kind);
  const url = '/biobank/api/iot/test-ingest?collector=' + encodeURIComponent(f.collectorCode) +
    '&temp=' + p.temp + '&door=' + p.door + '&power=' + p.power;
  try {
    const r = await api('GET', url);
    if (!quiet) log('sent ' + kind + ' to ' + f.freezerId + ' (' + p.temp + 'C door=' + p.door + ' power=' + p.power + ') -> ' + (r && r.result || 'ok'));
    return true;
  } catch (e) { log('SEND FAILED: ' + e.message); return false; }
}
function simBanner() {
  const b = $('#simBanner');
  if (!state.sim) { b.style.display = 'none'; return; }
  b.style.display = 'block';
  b.textContent = 'Simulator running: "' + state.sim.kind + '" on ' + state.sim.fz + ' every ' + state.sim.sec + 's since ' + state.sim.since + ' (sent: ' + state.sim.count + ')';
}
function stopSim(silent) {
  if (!state.sim) return;
  clearInterval(state.sim.timer); const s = state.sim; state.sim = null; simBanner();
  if (!silent) log('simulator stopped (' + s.fz + ', ' + s.count + ' readings sent). The freezer is now silent.');
}
async function startSim() {
  const f = getSel(); if (!f) { alert('Select a freezer first.'); return; }
  stopSim(true);
  const kind = $('#simKind').value; const sec = Math.max(5, parseInt($('#simSec').value, 10) || 20);
  const sim = { kind, sec, fz: f.freezerId, count: 0, since: new Date().toLocaleTimeString('en-GB') };
  const tick = async () => { if (state.sim !== sim) return; if (await sendReading(kind, true)) { sim.count++; simBanner(); } };
  state.sim = sim; sim.timer = setInterval(tick, sec * 1000); simBanner();
  log('simulator started: ' + kind + ' on ' + f.freezerId + ' every ' + sec + 's');
  await tick();
}

// ---------- alarms ----------
async function loadAlarms() {
  try {
    const a = await api('GET', '/biobank/api/iot/alarms');
    $('#alarms').innerHTML = (a || []).map(x => {
      const state_ = x.isCleared ? '<span class="ok">cleared</span>' + (x.acknowledgedAt ? '' : ' <span class="bad">(not acked)</span>')
                                 : (x.acknowledgedAt ? '<span class="mut">acked, still active</span>' : '<b class="bad">ACTIVE</b>');
      const btn = x.acknowledgedAt ? '' : '<button class="sm" data-ack="' + x.id + '">Acknowledge</button>';
      return '<tr><td>' + x.id + '</td><td>' + esc(x.freezerId) + '</td><td>' + esc(x.alarmType) + '</td><td>' + x.level + '</td>' +
        '<td title="' + esc(fmt(x.triggeredAt)) + '">' + esc(ago(x.triggeredAt)) + '</td><td>' + state_ + '</td><td>' + btn + '</td></tr>';
    }).join('') || '<tr><td colspan="7" class="mut">No open alarms.</td></tr>';
  } catch (e) { log('Alarms: ' + e.message); }
}
async function ack(id) {
  const note = prompt('Reason / note for acknowledging alarm #' + id + ':', '');
  if (note === null) return;
  try { await api('POST', '/biobank/api/alarms/' + id + '/acknowledge', { note }); log('alarm #' + id + ' acknowledged'); loadAlarms(); loadDashboard(); }
  catch (e) { log('ACK FAILED: ' + e.message); alert(e.message); }
}

// ---------- notifications ----------
async function loadNotifs() {
  try {
    const n = await api('GET', '/biobank/api/notifications?take=30');
    $('#notifs').innerHTML = (n || []).map(x =>
      '<tr><td title="' + esc(fmt(x.createdAt)) + '">' + esc(ago(x.createdAt)) + '</td><td>' + esc(x.kind) + '</td><td>' + esc(x.freezerId) + '</td>' +
      '<td>' + esc(x.contactName) + '</td><td>' + esc(x.channel) + '</td>' +
      '<td><span class="' + (x.status === 'Sent' ? 'ok' : (x.status === 'Pending' ? 'mut' : 'bad')) + '">' + esc(x.status) + '</span>' +
      (x.error ? '<div class="mut" style="font-size:11px">' + esc(x.error) + '</div>' : '') + '</td></tr>').join('')
      || '<tr><td colspan="6" class="mut">No notifications yet.</td></tr>';
  } catch (e) { log('Notifications: ' + e.message); }
}

// ---------- contacts ----------
let contactsCache = [];
async function loadContacts() {
  try {
    contactsCache = await api('GET', '/biobank/api/alarm-contacts') || [];
    $('#contacts').innerHTML = contactsCache.map(c =>
      '<tr><td>' + esc(c.name) + '</td><td>' + c.tier + '</td><td>' + esc(c.phone || '') + '</td><td>' + esc(c.email || '') + '</td>' +
      '<td>' + esc(c.telegramChatId || '') + '</td><td>' + esc(c.freezerId || 'all') + '</td>' +
      '<td>' + (c.isActive ? '<span class="ok">yes</span>' : '<span class="bad">no</span>') + '</td>' +
      '<td><button class="sm" data-ctest="' + c.id + '">Test</button> <button class="sm" data-ctoggle="' + c.id + '">' + (c.isActive ? 'Disable' : 'Enable') + '</button> ' +
      '<button class="sm" data-cdel="' + c.id + '">Delete</button></td></tr>').join('')
      || '<tr><td colspan="8" class="mut">No contacts. Add one below - without contacts nobody gets alarms.</td></tr>';
  } catch (e) { log('Contacts: ' + e.message); }
}
async function addContact() {
  const body = { name: $('#cName').value.trim(), phone: $('#cPhone').value.trim() || null, email: $('#cEmail').value.trim() || null,
    telegramChatId: $('#cTg').value.trim() || null, tier: parseInt($('#cTier').value, 10), freezerId: $('#cFz').value.trim() || null, isActive: true };
  try { await api('POST', '/biobank/api/alarm-contacts', body); log('contact added: ' + body.name);
    ['#cName', '#cPhone', '#cEmail', '#cTg', '#cFz'].forEach(s => $(s).value = ''); loadContacts(); }
  catch (e) { log('ADD CONTACT FAILED: ' + e.message); alert(e.message); }
}
async function testContact(id) {
  log('sending test message to contact #' + id + ' ...');
  try { const r = await api('POST', '/biobank/api/alarm-contacts/' + id + '/test');
    (r || []).forEach(x => log('  test via ' + x.channel + ' -> ' + x.status + (x.error ? ' (' + x.error + ')' : ''))); loadNotifs(); }
  catch (e) { log('TEST FAILED: ' + e.message); alert(e.message); }
}
async function toggleContact(id) {
  const c = contactsCache.find(x => x.id === id); if (!c) return;
  try { await api('PUT', '/biobank/api/alarm-contacts/' + id, { name: c.name, phone: c.phone, email: c.email, telegramChatId: c.telegramChatId, tier: c.tier, freezerId: c.freezerId, isActive: !c.isActive });
    loadContacts(); } catch (e) { log('UPDATE FAILED: ' + e.message); alert(e.message); }
}
async function delContact(id) {
  if (!confirm('Delete this contact?')) return;
  try { await api('DELETE', '/biobank/api/alarm-contacts/' + id); loadContacts(); } catch (e) { log('DELETE FAILED: ' + e.message); }
}

// ---------- settings ----------
async function loadSettings(keepMsg) {
  if (!state.sel) return;
  if (!keepMsg) $('#sMsg').textContent = '';
  try {
    const s = await api('GET', '/biobank/api/freezers/' + encodeURIComponent(state.sel) + '/settings');
    $('#sHigh').value = s.highTempLimit; $('#sLow').value = s.lowTempLimit; $('#sDelay').value = s.tempAlarmDelayMinutes;
    $('#sDoor').value = s.doorOpenMinutes; $('#sOff').value = s.offlineAfterMinutes; $('#sEsc').value = s.escalationMinutes;
    $('#sComm').value = String(s.isCommissioning);
    const au = await api('GET', '/biobank/api/freezers/' + encodeURIComponent(state.sel) + '/settings/audit?take=15');
    $('#audit').innerHTML = (au || []).map(a => '<tr><td>' + esc(fmt(a.changedAt)) + '</td><td>' + esc(a.changedBy) + '</td><td>' + esc(a.field) + '</td><td>' + esc(a.oldValue) + '</td><td>' + esc(a.newValue) + '</td></tr>').join('')
      || '<tr><td colspan="5" class="mut">No changes recorded.</td></tr>';
  } catch (e) { $('#sMsg').innerHTML = '<span class="bad">' + esc(e.message) + '</span>'; }
}
async function saveSettings() {
  if (!state.sel) { alert('Select a freezer first.'); return; }
  const body = { highTempLimit: parseFloat($('#sHigh').value), lowTempLimit: parseFloat($('#sLow').value),
    tempAlarmDelayMinutes: parseInt($('#sDelay').value, 10), doorOpenMinutes: parseInt($('#sDoor').value, 10),
    offlineAfterMinutes: parseInt($('#sOff').value, 10), escalationMinutes: parseInt($('#sEsc').value, 10),
    isCommissioning: $('#sComm').value === 'true' };
  try { await api('PUT', '/biobank/api/freezers/' + encodeURIComponent(state.sel) + '/settings', body);
    $('#sMsg').innerHTML = '<span class="ok">Saved.</span>'; log('settings saved for ' + state.sel); loadSettings(true); loadDashboard(); }
  catch (e) { $('#sMsg').innerHTML = '<span class="bad">' + esc(e.message) + '</span>'; }
}
function preset(delay, door, off, esc_) { $('#sDelay').value = delay; $('#sDoor').value = door; $('#sOff').value = off; $('#sEsc').value = esc_; }

// ---------- wiring ----------
document.addEventListener('click', e => {
  const t = e.target.closest('button'); if (!t) return;
  if (t.dataset.sel) selectFreezer(t.dataset.sel);
  else if (t.dataset.ack) ack(parseInt(t.dataset.ack, 10));
  else if (t.dataset.ctest) testContact(parseInt(t.dataset.ctest, 10));
  else if (t.dataset.ctoggle) toggleContact(parseInt(t.dataset.ctoggle, 10));
  else if (t.dataset.cdel) delContact(parseInt(t.dataset.cdel, 10));
  else if (t.closest('#scenarioBtns') && t.dataset.k) sendReading(t.dataset.k).then(() => setTimeout(refreshLive, 1500));
});
$('#simStart').onclick = startSim; $('#simStop').onclick = () => stopSim(false);
$('#cAdd').onclick = addContact; $('#sSave').onclick = saveSettings;
$('#pTest').onclick = () => preset(1, 1, 30, 2); $('#pNormal').onclick = () => preset(10, 5, 2, 10);
$('#refreshAll').onclick = () => { refreshLive(); loadContacts(); loadSettings(); };

function refreshLive() { loadDashboard(); loadAlarms(); loadNotifs(); }
setInterval(() => { if ($('#auto').checked) refreshLive(); }, 10000);
refreshLive(); loadContacts();
log('console ready.');
</script>
</body>
</html>
"""";
    }
}