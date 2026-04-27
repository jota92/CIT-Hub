(() => {
  const sections = Array.from(document.querySelectorAll('.reveal'));
  const appDownloadButtons = Array.from(document.querySelectorAll('.js-app-download'));
  const downloadNotice = document.querySelector('#download-notice');

  const IOS_STORE_URL = 'https://apps.apple.com/jp/app/cit-hub/id6760315556';

  const isIOS = () => {
    const ua = navigator.userAgent || '';
    return /iPad|iPhone|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  };

  const isAndroid = () => /Android/i.test(navigator.userAgent || '');

  if (sections.length) {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('is-visible');
            observer.unobserve(entry.target);
          }
        });
      },
      {
        threshold: 0.1,
        rootMargin: '0px 0px -8% 0px'
      }
    );

    sections.forEach((section) => observer.observe(section));
  }

  if (appDownloadButtons.length) {
    appDownloadButtons.forEach((button) => {
      button.addEventListener('click', (event) => {
        event.preventDefault();
        if (isAndroid()) {
          if (downloadNotice) {
            downloadNotice.hidden = false;
            downloadNotice.textContent = 'Android版は間もなくリリース予定です。';
          }
          return;
        }
        if (isIOS()) {
          window.location.href = IOS_STORE_URL;
          return;
        }
        window.location.href = IOS_STORE_URL;
      });
    });
  }
})();
