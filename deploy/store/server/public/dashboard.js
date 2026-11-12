// The author dashboard (store.lapinbim.com/dashboard/): who is signed in, their submissions with their review state and
// downloads, and the form that submits an extension or a new version (create → upload the file → screenshots → send).
// No framework; every text from the server is set as text, never as HTML.
(function () {
  var $ = function (s, el) { return (el || document).querySelector(s); };
  var form = $('[data-form]');
  var progress = $('[data-progress]'), errorOut = $('[data-error]');

  function call(method, path, body, headers) {
    var init = { method: method, credentials: 'same-origin', headers: headers || {} };
    if (body !== undefined) {
      if (body instanceof Blob) init.body = body;
      else { init.body = JSON.stringify(body); init.headers['content-type'] = 'application/json'; }
    }
    return fetch(path, init).then(function (r) {
      return r.json().catch(function () { return {}; }).then(function (j) {
        if (!r.ok) { var e = new Error(j.error || ('The store answered ' + r.status + '.')); e.status = r.status; throw e; }
        return j;
      });
    });
  }

  var STATES = {
    draft: 'Draft: no file yet', ready: 'Not sent', open: 'In review', merged: 'Accepted: listed at the next publication',
    closed: 'Declined (see its pull request)', withdrawn: 'Withdrawn',
  };
  var CHECKS = { passing: 'checks pass', failing: 'checks fail: see its pull request', running: 'checks running' };

  function cell(tr, text, cls) { var td = document.createElement('td'); if (cls) td.className = cls; if (text instanceof Node) td.appendChild(text); else td.textContent = text; tr.appendChild(td); return td; }
  function link(href, text) { var a = document.createElement('a'); a.href = href; a.textContent = text; a.rel = 'noopener'; return a; }

  function render(data) {
    var rows = data.submissions || [];
    var table = $('[data-submissions]'), tbody = $('tbody', table);
    tbody.textContent = '';
    $('[data-none]').hidden = rows.length > 0;
    table.hidden = rows.length === 0;
    rows.forEach(function (s) {
      var tr = document.createElement('tr');
      var name = document.createElement('span');
      name.appendChild(document.createTextNode(s.name + ' '));
      var id = document.createElement('code'); id.textContent = s.id; name.appendChild(id);
      cell(tr, name);
      cell(tr, s.version);
      var state = document.createElement('span');
      state.className = 'status ' + s.status;
      state.textContent = (STATES[s.status] || s.status) + (s.status === 'open' && s.checks && CHECKS[s.checks] ? ' · ' + CHECKS[s.checks] : '');
      var review = cell(tr, state);
      if (s.pr) { review.appendChild(document.createTextNode(' ')); review.appendChild(link(s.pr.url, '#' + s.pr.number)); }
      cell(tr, s.status === 'merged' ? String(s.downloads) : '–');
      var actions = cell(tr, '');
      if (s.status === 'merged' || s.status === 'open' || s.status === 'closed') {
        var again = document.createElement('button');
        again.type = 'button'; again.className = 'btn small'; again.textContent = 'New version';
        again.addEventListener('click', function () { newVersion(s.sid); });
        actions.appendChild(again);
      }
      if (s.status !== 'merged' && s.status !== 'withdrawn') {
        var w = document.createElement('button');
        w.type = 'button'; w.className = 'btn small'; w.textContent = 'Withdraw';
        w.addEventListener('click', function () {
          if (!confirm('Withdraw ' + s.name + ' ' + s.version + '? Its pull request is closed and its file deleted.')) return;
          call('POST', '/api/submissions/' + s.sid + '/withdraw').then(load, showError);
        });
        actions.appendChild(w);
      }
      tbody.appendChild(tr);
    });
  }

  function load() {
    return call('GET', '/api/me/submissions').then(render, showError);
  }

  function showError(e) { errorOut.textContent = e && e.message ? e.message : String(e); progress.textContent = ''; }

  /** The form filled from one of the author's submissions, its version raised. */
  function newVersion(sid) {
    call('GET', '/api/submissions/' + sid).then(function (s) {
      var f = s.fields;
      ['id', 'kind', 'name', 'summary', 'description', 'license', 'lapin', 'authorName', 'github', 'authorUrl', 'homepage', 'forum'].forEach(function (k) { if (form.elements[k]) form.elements[k].value = f[k] || ''; });
      ['tags', 'languages', 'items'].forEach(function (k) { form.elements[k].value = (f[k] || []).join(', '); });
      Array.prototype.forEach.call(form.querySelectorAll('[name=permissions]'), function (c) { c.checked = (f.permissions || []).indexOf(c.value) >= 0; });
      var m = /^(\d+)\.(\d+)\.(\d+)/.exec(f.version);
      form.elements.version.value = m ? m[1] + '.' + m[2] + '.' + (Number(m[3]) + 1) : '';
      kindChanged();
      $('[data-form-title]').textContent = 'A new version of ' + f.name;
      form.scrollIntoView({ behavior: 'smooth' });
    }, showError);
  }

  function kindChanged() { $('[data-permissions]').hidden = form.elements.kind.value !== 'script'; }
  form.elements.kind.addEventListener('change', kindChanged);
  $('[data-reset]').addEventListener('click', function () { form.reset(); kindChanged(); $('[data-form-title]').textContent = 'Submit an extension'; errorOut.textContent = ''; progress.textContent = ''; });

  form.addEventListener('submit', function (ev) {
    ev.preventDefault();
    errorOut.textContent = '';
    var el = form.elements;
    var file = el.file.files[0];
    if (!file) return showError(new Error('Choose the package file.'));
    if (!el.acceptTerms.checked) return showError(new Error('Accept the author terms to publish.'));
    var shots = Array.prototype.slice.call(el.screenshots.files, 0, 6);
    var fields = {
      id: el.id.value, kind: el.kind.value, name: el.name.value, version: el.version.value, summary: el.summary.value, description: el.description.value,
      license: el.license.value, lapin: el.lapin.value, authorName: el.authorName.value, github: el.github.value, authorUrl: el.authorUrl.value,
      homepage: el.homepage.value, forum: el.forum.value, tags: el.tags.value, languages: el.languages.value, items: el.items.value,
      permissions: Array.prototype.filter.call(form.querySelectorAll('[name=permissions]'), function (c) { return c.checked && el.kind.value === 'script'; }).map(function (c) { return c.value; }),
      acceptTerms: true,
    };
    var button = form.querySelector('[type=submit]');
    button.disabled = true;
    var sid;
    progress.textContent = 'Checking the details…';
    call('POST', '/api/submissions', fields).then(function (r) {
      sid = r.sid;
      progress.textContent = 'Uploading the file and checking it…';
      return call('PUT', '/api/submissions/' + sid + '/package', file, { 'x-file-name': file.name });
    }).then(function () {
      return shots.reduce(function (p, shot, i) {
        return p.then(function () { progress.textContent = 'Uploading screenshot ' + (i + 1) + '…'; return call('PUT', '/api/submissions/' + sid + '/screenshots/' + (i + 1), shot); });
      }, Promise.resolve());
    }).then(function () {
      progress.textContent = 'Opening the pull request…';
      return call('POST', '/api/submissions/' + sid + '/send');
    }).then(function (r) {
      progress.textContent = 'Submitted: pull request #' + r.pr.number + '. Its review shows above.';
      form.reset(); kindChanged();
      return load();
    }).catch(function (e) {
      showError(e);
      if (sid) progress.textContent = 'Not sent. It is kept as a draft above; fix what is said and submit again, or withdraw it.';
      load();
    }).then(function () { button.disabled = false; });
  });

  $('[data-logout]').addEventListener('click', function () {
    call('POST', '/auth/logout').then(function () { location.reload(); }, showError);
  });

  call('GET', '/api/me').then(function (r) {
    $('[data-who]').textContent = r.user.name + ' (' + r.user.username + ')';
    $('[data-logout]').hidden = false;
    $('[data-signed-in]').hidden = false;
    kindChanged();
    return load();
  }, function (e) {
    if (e.status === 401) $('[data-signed-out]').hidden = false;
    else { $('[data-signed-out]').hidden = false; showError(e); }
  });

  var toggle = $('[data-theme-toggle]');
  if (toggle) toggle.addEventListener('click', function () {
    var root = document.documentElement;
    var dark = root.getAttribute('data-theme') ? root.getAttribute('data-theme') === 'graphite' : matchMedia('(prefers-color-scheme: dark)').matches;
    root.setAttribute('data-theme', dark ? 'paper' : 'graphite');
    try { localStorage.setItem('lapin.store.theme', dark ? 'paper' : 'graphite'); } catch (e) { /* this page only */ }
  });
})();
