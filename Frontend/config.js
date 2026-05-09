// ── Bradford Council AI — Frontend Config ──────────────────────────────────
// Auto-detects environment:
//   file:// or localhost  → local API on port 5000
//   Netlify (https://)    → Railway production API
(function () {
  var loc = window.location;
  if (loc.protocol === 'file:' || loc.hostname === 'localhost' || loc.hostname === '127.0.0.1') {
    window.BRADFORD_API = 'http://localhost:5000';
  } else {
    window.BRADFORD_API = 'https://bradford-council-api-production.up.railway.app';
  }
})();
