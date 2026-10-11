// GET /j/:code: what a person without the app (or with a link preview) sees. Self-contained: inline CSS, no scripts, no fonts.
// Everything interpolated is HTML-escaped; the host's name comes from a client and is untrusted.

const SPORT_LABEL = { tennis: 'Tennis', golf: 'Golf' };

export function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}

const CSS = `
:root{--navy:#10243D;--lime:#D7F044;--cream:#FFF9EE}
*{box-sizing:border-box}
body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:24px 16px;
background:var(--navy);color:var(--cream);font:17px/1.4 -apple-system,BlinkMacSystemFont,"Helvetica Neue",Arial,sans-serif}
main{width:100%;max-width:420px;text-align:center}
h1{font-size:26px;line-height:1.2;margin:0 0 8px}
p{margin:0 0 20px;opacity:.85}
.code{font:700 44px/1.1 ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;letter-spacing:.18em;margin:8px 0 28px;
color:var(--lime);padding-left:.18em}
a.btn{display:block;padding:16px;border-radius:14px;text-decoration:none;font-weight:700;margin:0 0 12px}
a.open{background:var(--lime);color:var(--navy)}
a.install{border:2px solid var(--lime);color:var(--lime)}
small{display:block;margin-top:20px;opacity:.6}
`;

function page(title, body) {
  return `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="robots" content="noindex"><title>${escapeHtml(title)}</title><style>${CSS}</style></head>
<body><main>${body}</main></body></html>`;
}

function installButton(installUrl) {
  // Only http(s) links; anything else (javascript:, data:) is dropped.
  if (!installUrl || !/^https?:\/\//i.test(installUrl)) return '';
  return `<a class="btn install" href="${escapeHtml(installUrl)}">Get the game</a>`;
}

/** lobby = { code, sport, hostName } for a live lobby, or null. `code` must already be validated by the caller. */
export function renderLanding({ code, lobby, installUrl }) {
  if (!lobby) {
    return page('Invite ended', `<h1>This invite has ended</h1><p>The game already started or the host left. Ask your friend to send a new one.</p>${installButton(installUrl)}`);
  }
  const sport = SPORT_LABEL[lobby.sport] ?? 'a game';
  const host = escapeHtml(lobby.hostName);
  const safeCode = escapeHtml(code);
  return page(`${lobby.hostName} invited you`,
    `<h1>${host} invited you to play ${escapeHtml(sport)}</h1>
<p>Open the game and enter this code:</p>
<div class="code">${safeCode}</div>
<a class="btn open" href="motionclub://j/${encodeURIComponent(code)}">Open in the game</a>
${installButton(installUrl)}
<small>Don't have it yet? Install it, then open this link again.</small>`);
}
