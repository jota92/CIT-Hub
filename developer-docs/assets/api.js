(() => {
  const sections = [...document.querySelectorAll('.content section[data-title]')];
  const groups = new Map();
  for (const section of sections) {
    const group = section.dataset.group || 'Reference';
    if (!groups.has(group)) groups.set(group, []);
    groups.get(group).push(section);
  }
  const makeToc = (root, className = '') => {
    root.replaceChildren();
    for (const [name, items] of groups) {
      const wrap = document.createElement('nav');
      wrap.className = `navgroup ${className}`;
      wrap.setAttribute('aria-label', name);
      const title = document.createElement('h2');
      title.textContent = name;
      wrap.append(title);
      for (const section of items) {
        const link = document.createElement('a');
        link.href = `#${section.id}`;
        link.textContent = section.dataset.title;
        link.dataset.search = `${section.dataset.title} ${section.textContent}`.toLocaleLowerCase('ja');
        wrap.append(link);
      }
      root.append(wrap);
    }
  };
  makeToc(document.querySelector('#side-toc'));
  makeToc(document.querySelector('#right-toc'));
  makeToc(document.querySelector('#mobile-toc'));

  const searches = [...document.querySelectorAll('.search')];
  const status = document.createElement('p');
  status.className = 'search-status';
  status.setAttribute('role', 'status');
  status.setAttribute('aria-live', 'polite');
  const desktopSearch = document.querySelector('#api-search');
  if (desktopSearch) desktopSearch.insertAdjacentElement('afterend', status);
  searches.forEach(search => search.addEventListener('input', () => {
    const query = search.value.trim().toLocaleLowerCase('ja');
    searches.forEach(other => { if (other !== search) other.value = search.value; });
    let visibleCount = 0;
    for (const section of sections) {
      const match = !query || `${section.dataset.title} ${section.textContent}`.toLocaleLowerCase('ja').includes(query);
      section.hidden = !match;
      if (match) visibleCount += 1;
      document.querySelectorAll(`a[href="#${section.id}"]`).forEach(link => { link.hidden = !match; });
    }
    document.querySelectorAll('.navgroup').forEach(group => {
      const hasVisible = [...group.querySelectorAll('a')].some(link => !link.hidden);
      group.hidden = Boolean(query) && !hasVisible;
    });
    status.textContent = query ? `${visibleCount} 件の項目が見つかりました` : '';
  }));

  window.addEventListener('keydown', event => {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      (window.matchMedia('(max-width: 720px)').matches
        ? document.querySelector('#api-search-mobile')
        : document.querySelector('#api-search'))?.focus();
    }
    if (event.key === 'Escape' && document.activeElement?.matches('.search')) {
      document.activeElement.value = '';
      document.activeElement.dispatchEvent(new Event('input', { bubbles: true }));
      document.activeElement.blur();
    }
  });

  document.querySelectorAll('.copy').forEach(button => button.addEventListener('click', async () => {
    const content = button.closest('.codebox').querySelector('code').textContent;
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(content);
      } else {
        const textarea = document.createElement('textarea');
        textarea.value = content;
        textarea.setAttribute('readonly', '');
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.append(textarea);
        textarea.select();
        if (!document.execCommand('copy')) throw new Error('copy_failed');
        textarea.remove();
      }
      button.textContent = 'コピーしました';
    } catch {
      button.textContent = 'コピーできません';
    }
    window.setTimeout(() => { button.textContent = 'コピー'; }, 1400);
  }));

  document.querySelectorAll('.mobile-nav a').forEach(link => link.addEventListener('click', () => {
    link.closest('details')?.removeAttribute('open');
  }));

  const links = [...document.querySelectorAll('.sidebar a[href^="#"]')];
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver(entries => {
      for (const entry of entries) if (entry.isIntersecting) {
        links.forEach(link => link.classList.toggle('active', link.hash === `#${entry.target.id}`));
      }
    }, { rootMargin: '-15% 0px -75% 0px' });
    sections.forEach(section => observer.observe(section));
  }
})();
