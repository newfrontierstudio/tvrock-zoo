(() => {
  if (!['/user/kwl', '/user/kws', '/user/reg'].includes(location.pathname)) return;
  if (document.getElementById('ts-reservation-ui')) return;
  const style = document.createElement('style');
  style.id = 'ts-reservation-ui';
  style.textContent = '.ts-offset {display:inline-flex;align-items:center;gap:4px;white-space:nowrap}.ts-offset select {width:4.5em;font:inherit}.ts-option-hidden {display:none!important}';
  document.head.append(style);

  // Keep the original named select in its form: only its presentation changes.
  for (const name of ['ready', 'tale']) {
    for (const original of document.querySelectorAll(`select[name="${name}"]`)) {
      const initial = Number(original.value);
      if (!Number.isFinite(initial)) continue;
      const values = [...original.options].map(o => Number(o.value)).filter(Number.isFinite);
      const lower = Math.min(...values), upper = Math.max(...values);
      const label = name === 'ready' ? '録画開始' : '録画終了';
      const wrapper = document.createElement('span');
      wrapper.className = 'ts-offset';
      const minutes = document.createElement('select');
      const seconds = document.createElement('select');
      for (let m = Math.trunc(lower / 60); m <= Math.trunc(upper / 60); m++) minutes.add(new Option(String(m), String(m)));
      for (let s = -59; s <= 59; s++) seconds.add(new Option(String(s), String(s)));
      minutes.setAttribute('aria-label', label + ' 分');
      seconds.setAttribute('aria-label', label + ' 秒');
      minutes.value = String(Math.trunc(initial / 60)); seconds.value = String(initial % 60);
      wrapper.title = name === 'ready' ? '正数は開始前、負数は開始後（分×60＋秒）' : '正数は終了後、負数は終了前（分×60＋秒）';
      const sync = () => {
        const m = Number(minutes.value), s = Number(seconds.value);
        const total = m * 60 + s;
        seconds.setCustomValidity('');
        if (!Number.isInteger(m) || !Number.isInteger(s) || Math.abs(s) > 59 || total < lower || total > upper) {
          seconds.setCustomValidity(`合計を${lower}～${upper}秒の整数で入力してください。`);
          return false;
        }
        if (![...original.options].some(o => o.value === String(total))) original.add(new Option(String(total), String(total)));
        original.value = String(total);
        original.dispatchEvent(new Event('change', {bubbles:true}));
        return true;
      };
      minutes.addEventListener('change', sync); seconds.addEventListener('change', sync);
      original.before(wrapper);
      wrapper.append(minutes, '分', seconds);
      // Existing following text supplies 秒前 / 秒後; preserve these semantics.
      original.hidden = true;
      original.form?.addEventListener('submit', event => {
        if (!sync()) { event.preventDefault(); seconds.reportValidity(); }
      });
      original.form?.addEventListener('reset', () => setTimeout(() => {
        const value = Number(original.value);
        minutes.value = String(Math.trunc(value / 60)); seconds.value = String(value % 60);
        seconds.setCustomValidity('');
      }, 0));
    }
  }

  if (location.pathname === '/user/reg') return;
  const defaults = ['ronly', 'asd', 'eflw', 'coop', 'dchk2', 'tflw'];
  const hidden = ['wonly', 'npri', 'rmt', 'ron', 'roff', 'dchk', 'vsbt', 'vdsc', 'oneseg'];
  for (const form of document.forms) {
    if (!form.querySelector('input[type="checkbox"][name="eflw"]')) continue;
    const buttons = [...form.querySelectorAll('input[type="submit"],button')];
    const isNew = buttons.some(b => /自動検索予約.*(?:追加|登録)/.test(b.value || b.textContent))
      && !buttons.some(b => /自動検索予約変更/.test(b.value || b.textContent));
    if (isNew) {
      for (const name of defaults) {
        const box = form.querySelector(`input[type="checkbox"][name="${name}"]`);
        if (box) { box.checked = true; box.defaultChecked = true; }
      }
    }
    for (const name of hidden) {
      const box = form.querySelector(`input[type="checkbox"][name="${name}"]`);
      const cell = box?.closest('td');
      // Never disable/remove the input: an existing checked value must still submit.
      if (cell && cell.querySelectorAll('input').length === 1) {
        const wrap = document.createElement('span');
        wrap.className = 'ts-option-hidden';
        while (cell.firstChild) wrap.append(cell.firstChild);
        cell.append(wrap);
      }
    }
    for (const row of form.querySelectorAll('tr')) {
      if (row.cells.length && [...row.cells].every(c => c.querySelector(':scope > .ts-option-hidden') && c.children.length === 1)) row.hidden = true;
    }
  }
})();
