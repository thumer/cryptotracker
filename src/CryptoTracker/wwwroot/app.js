window.cryptoTracker = window.cryptoTracker || {};
window.cryptoTracker._manualOverlayCount = window.cryptoTracker._manualOverlayCount || 0;

window.cryptoTracker.updateOverlayState = function () {
  if (!document || !document.body || !document.documentElement) {
    return;
  }

  const hasOverlay = !!document.querySelector(".wizard-overlay, .modal-overlay");
  const isOpen = hasOverlay || window.cryptoTracker._manualOverlayCount > 0;

  document.body.classList.toggle("overlay-open", isOpen);
  document.documentElement.classList.toggle("overlay-open", isOpen);
};

window.cryptoTracker.setOverlayOpen = function (isOpen) {
  if (!document) {
    return;
  }

  if (isOpen) {
    window.cryptoTracker._manualOverlayCount += 1;
  } else {
    window.cryptoTracker._manualOverlayCount = Math.max(0, window.cryptoTracker._manualOverlayCount - 1);
  }

  window.cryptoTracker.updateOverlayState();
};

window.cryptoTracker.setWizardOpen = function (isOpen) {
  window.cryptoTracker.setOverlayOpen(isOpen);
};

(function initOverlayObserver() {
  if (!document) {
    return;
  }

  const start = function () {
    if (window.cryptoTracker._overlayObserver || !document.body) {
      return;
    }

    const observer = new MutationObserver(function () {
      window.cryptoTracker.updateOverlayState();
    });

    observer.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ["class"] });
    window.cryptoTracker._overlayObserver = observer;
    window.cryptoTracker.updateOverlayState();
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", start, { once: true });
  } else {
    start();
  }
})();
