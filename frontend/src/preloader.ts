const loaderConfig = {
  bundleUrl: 'main.bundle.js'
};

const preloaderRoot = document.getElementById('preloader');
const progressBar = document.createElement('div');
const progressFill = document.createElement('div');
const loadingText = document.createElement('div');

progressBar.style.position = 'absolute';
progressBar.style.left = '50%';
progressBar.style.top = '60%';
progressBar.style.width = '40%';
progressBar.style.height = '8px';
progressBar.style.transform = 'translateX(-50%)';
progressBar.style.background = '#222';
progressBar.style.borderRadius = '4px';

progressFill.style.width = '0%';
progressFill.style.height = '100%';
progressFill.style.background = '#4de3ff';
progressFill.style.borderRadius = '4px';

loadingText.style.position = 'absolute';
loadingText.style.top = '50%';
loadingText.style.left = '50%';
loadingText.style.transform = 'translate(-50%, -50%)';
loadingText.style.color = '#ffffff';
loadingText.style.fontFamily = 'Arial, sans-serif';
loadingText.style.fontSize = '14px';
loadingText.innerText = 'Loading... 0%';

progressBar.appendChild(progressFill);
preloaderRoot?.appendChild(progressBar);
preloaderRoot?.appendChild(loadingText);

function updateProgress(percent: number): void {
  const clamped = Math.min(100, Math.max(0, percent));
  progressFill.style.width = `${clamped}%`;
  loadingText.innerText = `Loading... ${clamped.toFixed(0)}%`;
}

function hidePreloader(): void {
  if (preloaderRoot) {
    preloaderRoot.style.opacity = '0';
    preloaderRoot.style.pointerEvents = 'none';
  }
}

function appendScript(data: string): void {
  const script = document.createElement('script');
  script.innerHTML = data;
  document.body.appendChild(script);
}

function streamMainBundle(): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open('GET', loaderConfig.bundleUrl, true);
    xhr.responseType = 'text';

    xhr.onprogress = (event) => {
      if (event.lengthComputable) {
        const percent = (event.loaded / event.total) * 100;
        updateProgress(percent);
      }
    };

    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) {
        appendScript(xhr.responseText);
        updateProgress(100);
        resolve();
      } else {
        reject(new Error(`Failed to load bundle: ${xhr.statusText}`));
      }
    };

    xhr.onerror = () => reject(new Error('Network error while loading bundle'));
    xhr.send();
  });
}

streamMainBundle()
  .then(() => hidePreloader())
  .catch((err) => {
    console.error(err);
    if (window.onerror) {
      window.onerror(err.message, loaderConfig.bundleUrl, 0, 0, err);
    }
  });

