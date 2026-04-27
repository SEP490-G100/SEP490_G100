(() => {
  const ROOT_ID = 'nm-toast-root';
  const DEFAULT_DURATION = 2800;
  const ICON_BY_VARIANT = {
    success: 'check_circle',
    error: 'error'
  };
  const TYPE_ALIASES = {
    info: 'info',
    'thong bao': 'info',
    thongbao: 'info',
    notification: 'info',
    success: 'success',
    ok: 'success',
    'thanh cong': 'success',
    thanhcong: 'success',
    warning: 'warning',
    warn: 'warning',
    'canh bao': 'warning',
    canhbao: 'warning',
    error: 'error',
    err: 'error',
    loi: 'error',
    failed: 'error',
    failure: 'error',
    'that bai': 'error',
    thatbai: 'error'
  };
  const VARIANT_BY_TYPE = {
    info: 'success',
    success: 'success',
    warning: 'error',
    error: 'error'
  };

  function ensureRoot() {
    let root = document.getElementById(ROOT_ID);
    if (root) return root;

    root = document.createElement('div');
    root.id = ROOT_ID;
    root.className = 'nm-toast-root';
    document.body.appendChild(root);
    return root;
  }

  function normalizeOptions(options) {
    if (typeof options === 'string') return { type: options };
    return options || {};
  }

  function normalizeTypeKey(rawType) {
    const type = String(rawType || 'success').trim().toLowerCase();
    const normalized = typeof type.normalize === 'function'
      ? type.normalize('NFD').replace(/[\u0300-\u036f]/g, '')
      : type;

    return normalized.replace(/\s+/g, ' ');
  }

  function getType(rawType) {
    const typeKey = normalizeTypeKey(rawType);
    return TYPE_ALIASES[typeKey] || 'success';
  }

  function getVariant(type) {
    return VARIANT_BY_TYPE[type] || 'error';
  }

  function getDuration(rawDuration) {
    const duration = Number(rawDuration);
    if (!Number.isFinite(duration)) return DEFAULT_DURATION;
    return Math.min(10000, Math.max(1200, duration));
  }

  function showToast(message, options) {
    if (message == null || message === '') return;

    const opts = normalizeOptions(options);
    const type = getType(opts.type);
    const variant = getVariant(type);
    const duration = getDuration(opts.duration);
    const dismissible = opts.dismissible !== false;

    const root = ensureRoot();
    const toast = document.createElement('div');
    toast.className = `nm-toast nm-toast--${variant}`;
    toast.dataset.toastType = type;
    toast.dataset.toastVariant = variant;
    toast.setAttribute('role', 'status');
    toast.setAttribute('aria-live', variant === 'error' ? 'assertive' : 'polite');

    const icon = document.createElement('span');
    icon.className = 'material-icons-round nm-toast__icon';
    icon.textContent = ICON_BY_VARIANT[variant];

    const text = document.createElement('div');
    text.className = 'nm-toast__text';
    text.textContent = String(message);

    const closeButton = document.createElement('button');
    closeButton.type = 'button';
    closeButton.className = 'nm-toast__close material-icons-round';
    closeButton.textContent = 'close';
    closeButton.setAttribute('aria-label', '\u0110\u00f3ng th\u00f4ng b\u00e1o');
    closeButton.hidden = !dismissible;

    toast.appendChild(icon);
    toast.appendChild(text);
    toast.appendChild(closeButton);
    root.appendChild(toast);

    let removed = false;
    const removeToast = () => {
      if (removed) return;
      removed = true;
      toast.classList.remove('show');
      window.setTimeout(() => toast.remove(), 220);
    };

    closeButton.addEventListener('click', removeToast);
    window.setTimeout(removeToast, duration);
    window.requestAnimationFrame(() => toast.classList.add('show'));

    return removeToast;
  }

  function syncCurrentUserAvatarSlot(slot, avatarUrl) {
    if (!slot) return;

    const nextUrl = typeof avatarUrl === 'string' ? avatarUrl.trim() : '';
    const fallback = slot.querySelector('[data-current-user-avatar-fallback]');
    let image = slot.querySelector('[data-current-user-avatar-img]');
    const imageClassName = slot.getAttribute('data-current-user-avatar-img-class') || '';

    if (nextUrl) {
      if (!image) {
        image = document.createElement('img');
        image.alt = 'Avatar';
        image.setAttribute('data-current-user-avatar-img', '');
        if (imageClassName) {
          image.className = imageClassName;
        }

        slot.insertBefore(image, fallback || null);
      } else if (imageClassName) {
        image.className = imageClassName;
      }

      image.src = nextUrl;
      image.hidden = false;

      if (fallback) {
        fallback.classList.add('hidden');
      }

      return;
    }

    if (image) {
      image.remove();
    }

    if (fallback) {
      if (!fallback.textContent || !fallback.textContent.trim()) {
        fallback.textContent = slot.getAttribute('data-current-user-avatar-initial') || 'U';
      }

      fallback.classList.remove('hidden');
    }
  }

  function updateCurrentUserAvatar(avatarUrl) {
    document.querySelectorAll('[data-current-user-avatar-slot]').forEach((slot) => {
      syncCurrentUserAvatarSlot(slot, avatarUrl);
    });

    return typeof avatarUrl === 'string' ? avatarUrl.trim() : '';
  }

  window.showToast = showToast;
  window.updateCurrentUserAvatar = updateCurrentUserAvatar;
})();
