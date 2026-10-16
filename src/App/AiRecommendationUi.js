(() => {
  if (location.pathname !== '/user/list') return;
  document.getElementById('ts-ai-recommendation-style')?.remove();
  const style = document.createElement('style');
  style.id = 'ts-ai-recommendation-style';
  style.textContent = '.ts-ai-recommendation,.ts-ai-recommendation *{font-style:italic!important}.ts-ai-recommendation{display:block;max-width:10em;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}';
  document.head.append(style);
  const clean = s => String(s || '').replace(/\s+/g, ' ').trim();
  const idOf = (link, route) => { try { const u = new URL(link.getAttribute('href'), location.href); return u.pathname === route ? u.searchParams.get('i') : null; } catch { return null; } };
  const apply = () => {
    const entries = Array.isArray(window.__tsAiRecommendations) ? window.__tsAiRecommendations.filter(e => e && typeof e === 'object') : [];
    for (const row of document.querySelectorAll('tr')) {
      const links = [...row.querySelectorAll('a[href]')].filter(a => a.closest('tr') === row);
      const reservationLink = links.find(a => idOf(a, '/user/reg'));
      if (!reservationLink) continue;
      const reservationId = idOf(reservationLink, '/user/reg');
      const keywordLink = links.find(a => idOf(a, '/user/kwl'));
      const keywordId = keywordLink && idOf(keywordLink, '/user/kwl');
      const suppression = (window.__tsDuplicateSuppressions || []).find(e => String(e.reservationId) === reservationId && clean(e.programTitle) === clean(reservationLink.textContent));
      const disabled = links.find(a => idOf(a, '/user/list') === reservationId && a.textContent.trim() === '×');
      const hideDuplicate = suppression && disabled && !new URLSearchParams(location.search).has('iv');
      if (hideDuplicate) { if (row.style.display !== 'none') row.style.display = 'none'; row.dataset.tsSuppressedDuplicate = 'true'; }
      else if (row.dataset.tsSuppressedDuplicate) { row.style.display = ''; delete row.dataset.tsSuppressedDuplicate; }
      if (suppression && disabled) { const reason = '重複整理: ' + suppression.reason + '\nこの予約を有効に戻すと、再び一覧へ表示します。'; if (disabled.title !== reason) disabled.title = reason; }
      const recommendation = entries.find(e =>
        (e.reservationId && String(e.reservationId) === reservationId && e.programTitle && clean(reservationLink.textContent) === clean(e.programTitle)) ||
        (!e.reservationId && e.keywordId && String(e.keywordId) === keywordId));
      if (!recommendation) continue;
      const cell = row.querySelector(':scope > .ts-search-column') || keywordLink?.closest('td') || [...row.cells].find(c => c.getAttribute('width') === '2%');
      if (!cell) continue;
      cell.classList.add('ts-search-column');
      const channel = links.find(a => a.getAttribute('href').includes('google.co.jp/search'))?.closest('td');
      if (channel && channel.nextElementSibling !== cell) channel.after(cell);
      let target = keywordLink;
      if (!target) { target = cell.querySelector('.ts-ai-recommendation'); if (!target) { target = document.createElement('span'); cell.replaceChildren(target); } const text = clean(recommendation.searchWord) || 'AIおすすめ'; if (target.textContent !== text) target.textContent = text; }
      if (!target.classList.contains('ts-ai-recommendation')) target.classList.add('ts-ai-recommendation');
      const tooltip = 'AIおすすめ（PC内で選定）\n検索: ' + (clean(recommendation.searchWord) || clean(target.textContent)) + (recommendation.reason ? '\n理由: ' + recommendation.reason : '');
      if (target.title !== tooltip) target.title = tooltip;
      if (target.getAttribute('aria-label') !== tooltip) target.setAttribute('aria-label', tooltip);
    }
  };
  window.__tsApplyAiRecommendations = apply;
  window.__tsAiObserver?.disconnect();
  const observer = new MutationObserver(apply);
  observer.observe(document.body, {subtree:true, childList:true, characterData:true, attributes:true, attributeFilter:['title']});
  window.__tsAiObserver = observer;
  apply();
})();
