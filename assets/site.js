(() => {
  const downloadButtons = document.querySelectorAll('.js-app-download');
  const notice = document.querySelector('#download-notice');
  const navToggle = document.querySelector('.nav-toggle');
  const nav = document.querySelector('.site-nav');
  const iosStoreUrl = 'https://apps.apple.com/jp/app/cit-hub/id6760315556';
  const isAndroid = () => /Android/i.test(navigator.userAgent || '');
  navToggle?.addEventListener('click', () => {
    const isOpen = nav?.classList.toggle('is-open') ?? false;
    navToggle.setAttribute('aria-expanded', String(isOpen));
  });
  nav?.querySelectorAll('a').forEach((link) => link.addEventListener('click', () => {
    nav.classList.remove('is-open');
    navToggle?.setAttribute('aria-expanded', 'false');
  }));
  downloadButtons.forEach((button) => button.addEventListener('click', async (event) => {
    event.preventDefault();
    if (!isAndroid()) { window.location.assign(iosStoreUrl); return; }
    try {
      const response = await fetch('./assets/version.json', { cache: 'no-store' });
      const data = await response.json();
      const current = Array.isArray(data.versions) ? data.versions[0] : null;
      if (!current?.downloadUrl) throw new Error('missing download URL');
      window.location.assign(current.downloadUrl);
    } catch {
      if (notice) { notice.hidden = false; notice.textContent = 'Android版の配布情報を取得できませんでした。時間を置いて再度お試しください。'; }
    }
  }));
  if (!window.matchMedia('(prefers-reduced-motion: reduce)').matches && 'IntersectionObserver' in window) {
    const observer = new IntersectionObserver((entries) => entries.forEach((entry) => {
      if (entry.isIntersecting) { entry.target.classList.add('is-visible'); observer.unobserve(entry.target); }
    }), { threshold: .08 });
    document.querySelectorAll('.reveal').forEach((element) => observer.observe(element));
  } else document.querySelectorAll('.reveal').forEach((element) => element.classList.add('is-visible'));
})();
