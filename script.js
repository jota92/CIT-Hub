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
      button.addEventListener('click', async (event) => {
        event.preventDefault();
        
        if (isAndroid()) {
          try {
            const response = await fetch('./assets/version.json');
            const data = await response.json();
            const latestVersion = data.versions[0];
            if (latestVersion && latestVersion.downloadUrl) {
              window.location.href = latestVersion.downloadUrl;
            } else {
              throw new Error('Download URL not found');
            }
          } catch (e) {
            if (downloadNotice) {
              downloadNotice.hidden = false;
              downloadNotice.textContent = '申し訳ありません。Android版のファイル情報を取得できませんでした。';
            }
          }
          return;
        }

        // isIOS or fallback
        window.location.href = IOS_STORE_URL;
      });
    });
  }
})();
