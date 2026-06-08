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

  if (sections.length && 'IntersectionObserver' in window) {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('is-visible');
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.08, rootMargin: '0px 0px -6% 0px' }
    );

    sections.forEach((section) => observer.observe(section));
  } else {
    sections.forEach((section) => section.classList.add('is-visible'));
  }

  if (appDownloadButtons.length) {
    appDownloadButtons.forEach((button) => {
      button.addEventListener('click', async (event) => {
        event.preventDefault();

        if (isAndroid()) {
          try {
            const response = await fetch('./assets/version.json', { cache: 'no-store' });
            const data = await response.json();
            const latestVersion = Array.isArray(data.versions) ? data.versions[0] : null;
            if (latestVersion && latestVersion.downloadUrl) {
              window.location.href = latestVersion.downloadUrl;
              return;
            }
            throw new Error('download url not found');
          } catch (error) {
            if (downloadNotice) {
              downloadNotice.hidden = false;
              downloadNotice.textContent = 'Android版の配布情報を取得できませんでした。時間を置いて再度お試しください。';
            }
            return;
          }
        }

        if (isIOS() || !isAndroid()) {
          window.location.href = IOS_STORE_URL;
        }
      });
    });
  }
})();
