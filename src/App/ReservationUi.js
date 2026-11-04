(() => {
  if (!['/user/kwl', '/user/kws', '/user/reg'].includes(location.pathname)) return;
  if (document.body.textContent.includes('この自動検索予約を削除してよろしいですか？')) {
    for (const form of document.forms) {
      if (!form.querySelector('input[type="hidden"][name="i"]') || form.dataset.zooConfirmation) continue;
      form.dataset.zooConfirmation='1';
      form.addEventListener('submit', event => {
        if(form.dataset.zooSending){event.preventDefault();return;}
        const choice=event.submitter;
        if (!choice || !['はい','いいえ'].includes(choice.value.trim())) return;
        event.preventDefault();
        if(form.dataset.zooSending) return;
        if(choice.value.trim()==='いいえ'){location.href='kwl';return;}
        const fields={};
        for(const input of form.querySelectorAll('input[type="hidden"][name]')) fields[input.name]=input.value;
        fields[choice.name]=choice.value;
        form.dataset.zooSending='1';
        for(const button of form.querySelectorAll('input[type="submit"]'))button.disabled=true;
        choice.value='削除を確認中…';
        window.chrome.webview.postMessage({type:'zoo-keyword-delete',fields});
      });
    }
  }
  if (document.getElementById('ts-reservation-ui')) return;
  const style = document.createElement('style');
  style.id = 'ts-reservation-ui';
  style.textContent = '.ts-offset {display:inline-flex;align-items:center;gap:4px;white-space:nowrap}select,input[type="text"],input[type="number"] {box-sizing:border-box!important;height:28px!important;min-height:28px!important;vertical-align:middle;font:inherit;padding:1px 4px!important}.ts-offset select,select[name="idle"] {width:4.5em!important;font:inherit}.ts-option-hidden {display:none!important}';
  document.head.append(style);

  // Keep the original named select in its form: only its presentation changes.
  for (const name of ['ready', 'reday', 'tale']) {
    for (const original of document.querySelectorAll(`select[name="${name}"]`)) {
      const initial = Number(original.value);
      if (!Number.isFinite(initial)) continue;
      const values = [...original.options].map(o => Number(o.value)).filter(Number.isFinite);
      const lower = Math.min(...values), upper = Math.max(...values);
      const label = name !== 'tale' ? '録画開始' : '録画終了';
      const wrapper = document.createElement('span');
      wrapper.className = 'ts-offset';
      const direction = document.createElement('select');
      direction.add(new Option(name !== 'tale' ? '早める' : '短縮する', '-1')); direction.add(new Option(name !== 'tale' ? '遅らせる' : '延長する', '1'));
      const sign = name !== 'tale' ? -1 : 1;
      direction.value = String(initial * sign <= 0 ? -1 : 1);
      direction.setAttribute('aria-label', label + ' 方向');
      const minutes = document.createElement('select');
      const seconds = document.createElement('select');
      for (let m = 0; m <= Math.floor(Math.max(Math.abs(lower), Math.abs(upper)) / 60); m++) minutes.add(new Option(String(m), String(m)));
      for (let s = 0; s <= 59; s++) seconds.add(new Option(String(s), String(s)));
      minutes.setAttribute('aria-label', label + ' 分');
      seconds.setAttribute('aria-label', label + ' 秒');
      minutes.value = String(Math.floor(Math.abs(initial) / 60)); seconds.value = String(Math.abs(initial) % 60);
      wrapper.title = label + 'の調整量（分・秒）';
      const sync = () => {
        const m = Number(minutes.value), s = Number(seconds.value);
        const total = (m * 60 + s) * Number(direction.value) * sign;
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
      direction.addEventListener('change', sync); minutes.addEventListener('change', sync); seconds.addEventListener('change', sync);
      original.before(wrapper);
      wrapper.append(direction, minutes, '分', seconds); direction.style.setProperty('width','6.5em','important');
      // Direction now carries the before/after meaning; retain only the seconds unit.
      if (original.nextSibling?.nodeType === 3) original.nextSibling.textContent = original.nextSibling.textContent.replace(/^\s*秒(?:前|後)/, '秒');
      original.hidden = true;
      original.form?.addEventListener('submit', event => {
        if (!sync()) { event.preventDefault(); seconds.reportValidity(); }
      });
      original.form?.addEventListener('reset', () => setTimeout(() => {
        const value = Number(original.value); direction.value = String(value * sign <= 0 ? -1 : 1);
        minutes.value = String(Math.floor(Math.abs(value) / 60)); seconds.value = String(Math.abs(value) % 60);
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
