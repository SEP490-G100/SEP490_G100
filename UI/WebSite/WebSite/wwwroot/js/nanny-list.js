let nannyMap;
let nannyMarkers = [];
let nannyProfiles = [];
let nannyAllProfiles = [];
let nannySearchTimer = null;
const nannyAddressSuggestionCache = new Map();
const nannyGeoCache = new Map();
let suppressNextNannyMapMove = false;
let currentNannyDetailId = null;
let currentNannyDetailUserId = null;
window.currentNannyDetailUserId = null;
const NANNY_DAY_LABELS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const NANNY_TIME_LABELS = ['Sáng', 'Chiều', 'Tối', 'Đêm'];
const NANNY_GEO_DEFAULT = { lat: 16.047, lng: 108.206, zoom: 6 };
const NANNY_GEO_FALLBACK = {
  'ho chi minh': { lat: 10.776, lng: 106.701, zoom: 11 },
  'hcm': { lat: 10.776, lng: 106.701, zoom: 11 },
  'ha noi': { lat: 21.028, lng: 105.854, zoom: 11 },
  'da nang': { lat: 16.054, lng: 108.202, zoom: 11 },
  'hai phong': { lat: 20.844, lng: 106.688, zoom: 11 },
  'can tho': { lat: 10.046, lng: 105.746, zoom: 11 },
  'hue': { lat: 16.463, lng: 107.59, zoom: 11 }
};

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function normalizeText(value) {
  return String(value ?? '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim();
}

function normalizeAdministrativeName(value) {
  return normalizeText(value)
    .replace(/^thanh pho\s+/i, '')
    .replace(/^tp\.?\s*/i, '')
    .replace(/^tinh\s+/i, '');
}

function getNannyGeoCacheKey(cityName) {
  const normalized = normalizeAdministrativeName(cityName);
  return normalized || '__empty__';
}

function getNannyStaticGeoFallback(cityName) {
  return NANNY_GEO_FALLBACK[getNannyGeoCacheKey(cityName)] || NANNY_GEO_DEFAULT;
}

function isFiniteCoordinate(value) {
  return Number.isFinite(Number(value));
}

function formatCurrency(value) {
  const amount = Number(value);
  if (!Number.isFinite(amount) || amount <= 0) return 'Thỏa thuận';
  return new Intl.NumberFormat('vi-VN').format(amount) + ' VND';
}

function formatSalary(min, max) {
  if (min && max) return `${formatCurrency(min)} - ${formatCurrency(max)}`;
  if (min) return `Từ ${formatCurrency(min)}`;
  if (max) return `Đến ${formatCurrency(max)}`;
  return 'Thỏa thuận';
}

function getNannyPlanLabel(profile) {
  const code = String(profile?.subscriptionPlanCode || '').trim().toUpperCase();
  if (code === 'NANNY_PRO') return 'Gói Pro';
  if (code === 'NANNY_PLUS') return 'Gói Plus';
  return '';
}

function renderNannyBenefitPills(profile) {
  const pills = [];
  const planLabel = getNannyPlanLabel(profile);

  if (planLabel) {
    pills.push(`<span class="nanny-pill ${profile?.searchPriority ? 'nanny-pill--orange' : ''}">${escapeHtml(planLabel)}</span>`);
  }

  if (profile?.searchPriority) {
    pills.push('<span class="nanny-pill nanny-pill--orange">Ưu tiên hiển thị</span>');
  } else if (profile?.featuredBadge) {
    pills.push('<span class="nanny-pill">Hồ sơ nổi bật</span>');
  }

  return pills.join('');
}

function isLoggedIn() {
  return typeof IS_AUTH !== 'undefined' && IS_AUTH === true;
}

function isParentRole() {
  return typeof IS_PARENT !== 'undefined' && IS_PARENT === true;
}

function showNannyToast(message, type = 'info') {
  if (!message) return;
  if (typeof window.showToast === 'function') {
    window.showToast(message, { type });
  }
}

function normalizeGuid(value) {
  return String(value ?? '').trim().toLowerCase();
}

function updateNannyFavoriteUi(nannyId, isFavorite) {
  const normalized = normalizeGuid(nannyId);
  document.querySelectorAll(`.nanny-card-favorite[data-nanny-id="${normalized}"]`).forEach((button) => {
    button.classList.toggle('active', !!isFavorite);
    button.setAttribute('aria-pressed', isFavorite ? 'true' : 'false');
    button.title = isFavorite ? 'Bỏ yêu thích' : 'Yêu thích bảo mẫu';
    const icon = button.querySelector('.material-icons-round');
    if (icon) icon.textContent = isFavorite ? 'favorite' : 'favorite_border';
  });

  const detailButton = document.getElementById('nd-favoriteBtn');
  const detailIcon = detailButton?.querySelector('.material-icons-round');
  const detailText = document.getElementById('nd-favoriteBtnText');
  if (detailButton && currentNannyDetailId && normalizeGuid(currentNannyDetailId) === normalized) {
    detailButton.classList.toggle('active', !!isFavorite);
    if (detailIcon) detailIcon.textContent = isFavorite ? 'favorite' : 'favorite_border';
    if (detailText) detailText.textContent = isFavorite ? 'Bỏ yêu thích' : 'Yêu thích';
  }
}

function setNannyFavoriteState(nannyId, isFavorite) {
  const normalized = normalizeGuid(nannyId);

  nannyAllProfiles = nannyAllProfiles.map((profile) => {
    if (normalizeGuid(profile?.id) === normalized) return { ...profile, isFavorite: !!isFavorite };
    return profile;
  });

  nannyProfiles = nannyProfiles.map((profile) => {
    if (normalizeGuid(profile?.id) === normalized) return { ...profile, isFavorite: !!isFavorite };
    return profile;
  });

  updateNannyFavoriteUi(nannyId, isFavorite);
}

async function toggleNannyFavorite(nannyId, event) {
  event?.stopPropagation?.();

  if (!isLoggedIn()) {
    showNannyToast('Vui lòng đăng nhập để yêu thích bảo mẫu.', 'error');
    return;
  }

  if (!isParentRole()) {
    showNannyToast('Chỉ phụ huynh mới có quyền yêu thích bảo mẫu.', 'error');
    return;
  }

  try {
    const response = await fetch(`/Nanny/ToggleFavorite?id=${encodeURIComponent(nannyId)}`, {
      method: 'POST',
      credentials: 'same-origin'
    });
    const json = await response.json();
    if (!json?.success) {
      showNannyToast(json?.message || 'Không thể cập nhật yêu thích.', 'error');
      return;
    }

    const favoriteState = !!json.isFavorite;
    setNannyFavoriteState(nannyId, favoriteState);
    showNannyToast(
      json.message || (favoriteState ? 'Đã yêu thích bảo mẫu.' : 'Đã bỏ yêu thích.'),
      'success'
    );
  } catch {
    showNannyToast('Không thể cập nhật yêu thích.', 'error');
  }
}

function toggleNannyFavoriteFromDetail(event) {
  if (!currentNannyDetailId) return;
  toggleNannyFavorite(currentNannyDetailId, event);
}

async function sendContactRequest(nannyProfileId, message) {
  if (!isLoggedIn()) {
    showNannyToast('Vui lòng đăng nhập để gửi yêu cầu liên hệ.', 'error');
    return null;
  }

  if (!isParentRole()) {
    showNannyToast('Chỉ phụ huynh mới có quyền gửi yêu cầu liên hệ.', 'error');
    return null;
  }

  if (!nannyProfileId) {
    showNannyToast('Không tìm thấy hồ sơ bảo mẫu để gửi yêu cầu.', 'error');
    return null;
  }

  try {
    const response = await fetch('/Nanny/SendContactRequest', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        nannyProfileId,
        message: String(message ?? '').trim() || null
      })
    });

    const json = await response.json();
    if (!response.ok || !json?.success) {
      showNannyToast(json?.message || 'Không thể gửi yêu cầu liên hệ.', 'error');
      return null;
    }

    showNannyToast(json?.message || 'Đã gửi yêu cầu liên hệ thành công.', 'success');
    window.dispatchEvent(new CustomEvent('nm:notifications-refresh'));
    return json;
  } catch {
    showNannyToast('Không thể gửi yêu cầu liên hệ.', 'error');
    return null;
  }
}

async function sendContactRequestFromDetail(event) {
  event?.stopPropagation?.();
  if (!currentNannyDetailId) return;

  const contactButton = document.getElementById('nd-contactBtn');
  if (contactButton) contactButton.disabled = true;

  const defaultMessage = 'Tôi muốn trao đổi thêm về công việc và lịch làm việc.';
  const message = window.prompt('Nhập lời nhắn gửi đến bảo mẫu (có thể bỏ trống):', defaultMessage);
  if (message === null) {
    if (contactButton) contactButton.disabled = false;
    return;
  }

  await sendContactRequest(currentNannyDetailId, message);
  if (contactButton) contactButton.disabled = false;
}

function debounceNannySearch() {
  clearTimeout(nannySearchTimer);
  nannySearchTimer = setTimeout(doNannySearch, 320);
}

async function fetchNannyAddressSuggestions(query) {
  const normalized = String(query ?? '').trim();
  if (normalized.length < 2) return [];

  const cacheKey = normalizeText(normalized);
  if (nannyAddressSuggestionCache.has(cacheKey)) {
    return nannyAddressSuggestionCache.get(cacheKey) || [];
  }

  try {
    const response = await fetch(`/Address/Suggest?q=${encodeURIComponent(normalized)}&limit=10`, { credentials: 'same-origin' });
    if (!response.ok) return [];
    const json = await response.json();
    const items = Array.isArray(json) ? json : [];
    nannyAddressSuggestionCache.set(cacheKey, items);
    return items;
  } catch {
    return [];
  }
}

async function resolveNannyCityGeo(cityName) {
  const key = getNannyGeoCacheKey(cityName);
  if (nannyGeoCache.has(key)) {
    return nannyGeoCache.get(key);
  }

  let resolved = getNannyStaticGeoFallback(cityName);
  const cityValue = String(cityName ?? '').trim();
  if (cityValue.length >= 2) {
    const suggestions = await fetchNannyAddressSuggestions(`${cityValue}, Vietnam`);
    const first = suggestions.find((item) => isFiniteCoordinate(item?.latitude) && isFiniteCoordinate(item?.longitude));
    if (first) {
      resolved = {
        lat: Number(first.latitude),
        lng: Number(first.longitude),
        zoom: 11
      };
    }
  }

  nannyGeoCache.set(key, resolved);
  return resolved;
}

async function hydrateNannyGeoForMap(items) {
  if (!Array.isArray(items) || !items.length) return;

  const missingCities = new Map();

  items.forEach((profile) => {
    if (isFiniteCoordinate(profile?.latitude) && isFiniteCoordinate(profile?.longitude)) {
      profile.__isGeoFallback = false;
      return;
    }

    const cityValue = String(profile?.city ?? '').trim();
    const key = getNannyGeoCacheKey(cityValue);
    if (key !== '__empty__' && !missingCities.has(key)) {
      missingCities.set(key, cityValue);
    }
  });

  await Promise.all(
    Array.from(missingCities.values()).map((cityName) => resolveNannyCityGeo(cityName))
  );

  items.forEach((profile) => {
    if (isFiniteCoordinate(profile?.latitude) && isFiniteCoordinate(profile?.longitude)) return;

    const cityValue = String(profile?.city ?? '').trim();
    const fallback = nannyGeoCache.get(getNannyGeoCacheKey(cityValue)) || getNannyStaticGeoFallback(cityValue);
    profile.latitude = fallback.lat;
    profile.longitude = fallback.lng;
    profile.__isGeoFallback = true;
  });
}

function initNannyMap() {
  const mapEl = document.getElementById('nannyMap');
  if (nannyMap || !mapEl || typeof L === 'undefined') return;

  nannyMap = L.map('nannyMap', { zoomControl: true }).setView([10.776, 106.701], 11);
  window.__leafletNannyMap = nannyMap;  // expose for rec panel (window.nannyMap = DOM element)
  L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    attribution: '&copy; CartoDB',
    maxZoom: 19
  }).addTo(nannyMap);

  nannyMap.whenReady(() => {
    try {
      nannyMap.invalidateSize({ animate: false });
    } catch (_) {
      /* ignore */
    }
  });
  setTimeout(() => {
    try {
      nannyMap?.invalidateSize({ animate: false });
    } catch (_) {
      /* ignore */
    }
  }, 0);

  nannyMap.on('moveend', () => {
    if (suppressNextNannyMapMove) {
      suppressNextNannyMapMove = false;
      return;
    }

    renderNannyProfilesForCurrentBounds(false);
  });
}

function getCurrentNannyMapBounds() {
  if (!nannyMap) return null;
  const bounds = nannyMap.getBounds();
  return bounds?.isValid?.() ? bounds : null;
}

function getNannyProfilesInCurrentBounds(items) {
  const collection = Array.isArray(items) ? items : [];
  const bounds = getCurrentNannyMapBounds();
  if (!bounds) return collection;

  return collection.filter((profile) => {
    if (!isFiniteCoordinate(profile?.latitude) || !isFiniteCoordinate(profile?.longitude)) return false;
    return bounds.contains([Number(profile.latitude), Number(profile.longitude)]);
  });
}

function renderNannyProfilesForCurrentBounds(fitToMarkers = false) {
  const visibleItems = getNannyProfilesInCurrentBounds(nannyAllProfiles);
  nannyProfiles = visibleItems;
  renderNannyCards(visibleItems, { fitToMarkers });
}

function getNannyPoint(profile, idx) {
  const fallback = getNannyStaticGeoFallback(profile?.city);
  const hasExactCoordinates =
    isFiniteCoordinate(profile?.latitude) &&
    isFiniteCoordinate(profile?.longitude) &&
    profile?.__isGeoFallback !== true;

  const baseLat = hasExactCoordinates ? Number(profile.latitude) : Number(profile?.latitude ?? fallback.lat);
  const baseLng = hasExactCoordinates ? Number(profile.longitude) : Number(profile?.longitude ?? fallback.lng);
  const shouldJitter = !hasExactCoordinates;

  const latOffset = shouldJitter ? ((idx % 4) - 1.5) * 0.0022 : 0;
  const lngOffset = shouldJitter ? ((Math.floor(idx / 4) % 4) - 1.5) * 0.0022 : 0;

  return {
    lat: baseLat + latOffset,
    lng: baseLng + lngOffset,
    zoom: hasExactCoordinates ? 13 : fallback.zoom,
    radius: hasExactCoordinates ? 500 : 1800
  };
}

function clearNannyMarkers() {
  nannyMarkers.forEach((entry) => {
    entry.marker?.remove();
    entry.circle?.remove();
  });
  nannyMarkers = [];
}

// Helpers for rec panel (window.xxx ≠ let vars in this file)
function pushRecNannyMarker(item) { nannyMarkers.push(item); }
function setSuppressNextNannySearch() { suppressNextNannyMapMove = true; }

/** Tránh Leaflet gọi layerPointToContainerPoint khi map/container chưa sẵn (openPopup trực tiếp dễ lỗi). */
function safeOpenNannyPopup(marker) {
  if (!marker || !nannyMap) return;
  try {
    nannyMap.invalidateSize({ animate: false });
  } catch (_) {
    /* ignore */
  }
  requestAnimationFrame(() => {
    try {
      const c = nannyMap.getContainer?.();
      if (!c || !c.isConnected) return;
      if (marker.getMap?.() !== nannyMap) return;
      marker.openPopup();
    } catch (_) {
      /* ignore */
    }
  });
}

function setNannyMarkerHover(idx, active, openPopup = false) {
  const markerData = nannyMarkers[idx];
  if (!markerData) return;
  markerData.element?.classList.toggle('active', active);
  markerData.circle?.setStyle({
    color: active ? '#f97316' : '#fdba74',
    fillColor: active ? '#fb923c' : '#fdba74',
    fillOpacity: active ? 0.18 : 0.1,
    weight: active ? 2 : 1
  });
  if (openPopup && active) {
    safeOpenNannyPopup(markerData.marker);
  }
}

function focusNannyMarker(idx) {
  const markerData = nannyMarkers[idx];
  if (!markerData || !nannyMap) return;
  suppressNextNannyMapMove = true;
  nannyMap.flyTo([markerData.point.lat, markerData.point.lng], markerData.point.zoom || 13, { duration: 0.4 });
  setNannyMarkerHover(idx, true, false);
}

function renderNannyCards(items, options = {}) {
  const { fitToMarkers = false } = options;
  const list = document.getElementById('nannyList');
  const count = document.getElementById('nannyResultCount');
  if (!list || !count) return;

  count.textContent = `${items.length} hồ sơ`;
  clearNannyMarkers();
  if (nannyMap) {
    try { nannyMap.invalidateSize({ animate: false }); } catch (_) {}
  }

  if (!items.length) {
    list.innerHTML = `
      <div class="nanny-empty">
        <span class="material-icons-round">search_off</span>
        <h3>Không tìm thấy hồ sơ phù hợp</h3>
        <p>Thử thay đổi từ khóa, khu vực hoặc kỹ năng để mở rộng kết quả.</p>
      </div>`;
    return;
  }

  if (!nannyMap) {
    initNannyMap();
  }

  list.innerHTML = items.map((profile, idx) => {
    const topSkills = Array.isArray(profile.skills) ? profile.skills.slice(0, 3) : [];
    return `
      <article class="nanny-card" data-idx="${idx}" data-id="${escapeHtml(profile.id)}">
        <div class="nanny-card__avatar-wrap">
          <img class="nanny-card__avatar" src="${escapeHtml(profile.avatarUrl || '/img/nanny-logo.jpg')}" alt="${escapeHtml(profile.fullName)}" />
        </div>
        <div class="nanny-card__body">
          <div class="nanny-card__head">
            <div>
              <h3>${escapeHtml(profile.fullName || 'Bảo mẫu')}</h3>
              <p>${escapeHtml([profile.district, profile.city].filter(Boolean).join(', ') || 'Chưa cập nhật khu vực')}</p>
            </div>
            <div class="nanny-card__head-actions">
              <span class="nanny-card__salary">${escapeHtml(formatSalary(profile.expectedSalaryMin, profile.expectedSalaryMax))}</span>
              ${isParentRole() ? `
                <button type="button"
                        class="nanny-card-favorite ${profile.isFavorite ? 'active' : ''}"
                        data-nanny-id="${escapeHtml(normalizeGuid(profile.id))}"
                        aria-pressed="${profile.isFavorite ? 'true' : 'false'}"
                        title="${profile.isFavorite ? 'Bỏ yêu thích' : 'Yêu thích bảo mẫu'}"
                        onclick="toggleNannyFavorite('${escapeHtml(profile.id)}', event)">
                  <span class="material-icons-round">${profile.isFavorite ? 'favorite' : 'favorite_border'}</span>
                </button>` : ''}
            </div>
          </div>
          <p class="nanny-card__bio">${escapeHtml(profile.bio || 'Hồ sơ chưa có mô tả giới thiệu.')}</p>
          <div class="nanny-card__meta">
            ${renderNannyBenefitPills(profile)}
            <span class="nanny-pill nanny-pill--orange">${escapeHtml(profile.verificationStatusLabel || 'Chưa xác minh')}</span>
            <span class="nanny-pill">${profile.age ? `${profile.age} tuổi` : 'Chưa rõ tuổi'}</span>
            <span class="nanny-pill">${profile.yearsOfExperience ? `${profile.yearsOfExperience} năm kinh nghiệm` : 'Chưa rõ kinh nghiệm'}</span>
          </div>
          <div class="nanny-card__skills">
            ${topSkills.length
              ? topSkills.map((skill) => `<span class="nanny-skill-chip">${escapeHtml(skill.skillName || '')}</span>`).join('')
              : '<span class="nanny-card__muted">Chưa có kỹ năng nổi bật</span>'}
          </div>
        </div>
      </article>`;
  }).join('');

  if (nannyMap && typeof L !== 'undefined') {
    items.forEach((profile, idx) => {
      const point = getNannyPoint(profile, idx);
      const icon = L.divIcon({
        className: '',
        html: `
        <div class="nanny-map-marker">
          <span class="nanny-map-marker__halo"></span>
          <span class="nanny-map-marker__pin"><span class="nanny-map-marker__core"></span></span>
        </div>`,
        iconSize: [28, 36],
        iconAnchor: [14, 30]
      });

      const marker = L.marker([point.lat, point.lng], { icon }).addTo(nannyMap);
      const circle = L.circle([point.lat, point.lng], {
        radius: point.radius,
        color: '#fdba74',
        fillColor: '#fdba74',
        fillOpacity: 0.1,
        weight: 1
      }).addTo(nannyMap);

      marker.bindPopup(`
      <div class="text-sm font-semibold text-slate-700">${escapeHtml(profile.fullName || 'Bảo mẫu')}</div>
      <div class="text-xs text-slate-500 mt-1">${escapeHtml([profile.district, profile.city].filter(Boolean).join(', ') || 'Chưa cập nhật khu vực')}</div>
    `);

      nannyMarkers.push({ marker, circle, point, element: marker.getElement() });
    });
  }

  if (fitToMarkers && nannyMap && nannyMarkers.length) {
    const bounds = L.latLngBounds(
      nannyMarkers.map((entry) => [entry.point.lat, entry.point.lng])
    );
    suppressNextNannyMapMove = true;
    try {
      nannyMap.fitBounds(bounds, { padding: [24, 24], maxZoom: 13, animate: false });
    } catch (e) {
      console.warn('[nanny-list] fitBounds error', e);
    }
  }

  list.querySelectorAll('.nanny-card').forEach((card) => {
    const idx = Number(card.dataset.idx);
    card.addEventListener('mouseenter', () => setNannyMarkerHover(idx, true, false));
    card.addEventListener('mouseleave', () => setNannyMarkerHover(idx, false, false));
    card.addEventListener('click', () => {
      list.querySelectorAll('.nanny-card').forEach((item) => item.classList.remove('active'));
      card.classList.add('active');
      focusNannyMarker(idx);
      openNannyDetail(items[idx].id);
    });
  });
}

async function doNannySearch() {
  const params = new URLSearchParams({
    page: '1',
    pageSize: '100'
  });

  const keyword = document.getElementById('nannyKeyword')?.value.trim();
  if (keyword) params.append('keyword', keyword);

  try {
    const response = await fetch(`/Nanny/BrowseData?${params.toString()}`, { credentials: 'same-origin' });
    const json = await response.json();
    const rawProfiles = Array.isArray(json.data) ? json.data : [];
    await hydrateNannyGeoForMap(rawProfiles);
    nannyAllProfiles = rawProfiles;
    nannyProfiles = nannyAllProfiles;
    renderNannyCards(nannyAllProfiles, { fitToMarkers: true });
  } catch {
    nannyAllProfiles = [];
    nannyProfiles = [];
    renderNannyCards([]);
  }
}

function renderAvailability(slots) {
  if (!Array.isArray(slots) || !slots.length) {
    return '<span class="nanny-card__muted">Chưa cập nhật lịch rảnh.</span>';
  }

  const dayAliases = {
    mo: 0, mon: 0, monday: 0, 'thu 2': 0, thuhai: 0,
    tu: 1, tue: 1, tues: 1, tuesday: 1, 'thu 3': 1, thuba: 1,
    we: 2, wed: 2, wednesday: 2, 'thu 4': 2, thutu: 2,
    th: 3, thu: 3, thur: 3, thurs: 3, thursday: 3, 'thu 5': 3, thunam: 3,
    fr: 4, fri: 4, friday: 4, 'thu 6': 4, thusau: 4,
    sa: 5, sat: 5, saturday: 5, 'thu 7': 5, thubay: 5,
    su: 6, sun: 6, sunday: 6, cn: 6, 'chu nhat': 6
  };
  const timeAliases = {
    morning: 0, sang: 0,
    afternoon: 1, chieu: 1,
    evening: 2, toi: 2,
    night: 3, dem: 3
  };

  const parseDayOfWeek = (slot) => {
    const raw = Number(slot?.dayOfWeek);
    if (Number.isInteger(raw) && raw >= 0 && raw <= 6) return raw;

    const key = normalizeText(slot?.dayLabel);
    if (!key) return null;
    if (Object.prototype.hasOwnProperty.call(dayAliases, key)) return dayAliases[key];
    return dayAliases[key.slice(0, 3)] ?? dayAliases[key.slice(0, 2)] ?? null;
  };

  const parseTimeSlot = (slot) => {
    const raw = Number(slot?.timeSlot);
    if (Number.isInteger(raw) && raw >= 0 && raw <= 3) return raw;

    const key = normalizeText(slot?.timeSlotLabel);
    if (!key) return null;
    return timeAliases[key] ?? null;
  };

  const selected = new Set();
  slots.forEach((slot) => {
    const dayOfWeek = parseDayOfWeek(slot);
    const timeSlot = parseTimeSlot(slot);
    if (dayOfWeek == null || timeSlot == null) return;
    selected.add(`${dayOfWeek}-${timeSlot}`);
  });

  let html = '<div class="nanny-detail-availability-grid"><div></div>';
  NANNY_DAY_LABELS.forEach((label) => { html += `<div class="nanny-schedule-col-label">${label}</div>`; });

  NANNY_TIME_LABELS.forEach((rowLabel, timeSlot) => {
    html += `<div class="nanny-schedule-row-label">${rowLabel}</div>`;
    for (let dayOfWeek = 0; dayOfWeek < 7; dayOfWeek += 1) {
      const isActive = selected.has(`${dayOfWeek}-${timeSlot}`);
      html += `
        <div class="nanny-schedule-cell ${isActive ? 'active' : ''}" aria-hidden="true">
          <span class="nanny-schedule-check">${isActive ? '&#10003;' : ''}</span>
        </div>`;
    }
  });

  html += '</div>';
  return html;
}

function formatPublicLocation(detail) {
  return [detail?.ward, detail?.district, detail?.city].filter(Boolean).join(', ');
}

async function openNannyDetail(id) {
  if (!id) return;

  try {
    const response = await fetch(`/Nanny/DetailData?id=${encodeURIComponent(id)}`, { credentials: 'same-origin' });
    const json = await response.json();
    const detail = json?.data;
    if (!detail) return;

    document.getElementById('nd-avatar').src = detail.avatarUrl || '/img/nanny-logo.jpg';
    document.getElementById('nd-name').textContent = detail.fullName || 'Bảo mẫu';
    const publicLocation = formatPublicLocation(detail);
    document.getElementById('nd-location').textContent = publicLocation || 'Chưa cập nhật khu vực';
    document.getElementById('nd-verify').textContent = detail.verificationStatusLabel || 'Đang cập nhật';
    document.getElementById('nd-bio').textContent = detail.bio || 'Hồ sơ chưa có mô tả.';
    document.getElementById('nd-phone').textContent = isParentRole() ? 'Gửi yêu cầu liên hệ để trao đổi trực tiếp với bảo mẫu' : 'Thông tin liên hệ chỉ hiện sau khi đã kết nối';
    document.getElementById('nd-address').textContent = publicLocation || 'Chưa cập nhật';
    document.getElementById('nd-travel').textContent = detail.maxTravelDistance ? `${detail.maxTravelDistance} km` : 'Chưa cập nhật';
    document.getElementById('nd-age').textContent = detail.age ? `${detail.age} tuổi` : 'Chưa rõ tuổi';
    document.getElementById('nd-exp').textContent = detail.yearsOfExperience ? `${detail.yearsOfExperience} năm kinh nghiệm` : 'Chưa rõ kinh nghiệm';
    document.getElementById('nd-education').textContent = detail.educationLevelLabel || 'Chưa cập nhật học vấn';
    document.getElementById('nd-salary').textContent = formatSalary(detail.expectedSalaryMin, detail.expectedSalaryMax);
    const planChip = document.getElementById('nd-plan');
    if (planChip) {
      const planLabel = getNannyPlanLabel(detail);
      const planText = detail.searchPriority
        ? `${planLabel || 'Hồ sơ nổi bật'} • Ưu tiên hiển thị`
        : detail.featuredBadge
          ? (planLabel || 'Hồ sơ nổi bật')
          : '';
      planChip.textContent = planText;
      planChip.classList.toggle('hidden', !planText);
    }

    const skillsEl = document.getElementById('nd-skills');
    skillsEl.innerHTML = Array.isArray(detail.skills) && detail.skills.length
      ? detail.skills.map((skill) => `
          <span class="nanny-detail-skill">
            ${escapeHtml(skill.skillName || '')}
            ${skill.proficiencyLevelLabel ? `<small>${escapeHtml(skill.proficiencyLevelLabel)}</small>` : ''}
          </span>`).join('')
      : '<span class="nanny-card__muted">Chưa có kỹ năng được khai báo.</span>';

    currentNannyDetailId = detail.id || id;
    currentNannyDetailUserId = detail.userId || null;
    window.currentNannyDetailUserId = currentNannyDetailUserId;

    const favoriteButton = document.getElementById('nd-favoriteBtn');
    if (favoriteButton) {
      favoriteButton.classList.toggle('hidden', !isParentRole());
      favoriteButton.classList.toggle('active', !!detail.isFavorite);
      const icon = favoriteButton.querySelector('.material-icons-round');
      const text = document.getElementById('nd-favoriteBtnText');
      if (icon) icon.textContent = detail.isFavorite ? 'favorite' : 'favorite_border';
      if (text) text.textContent = detail.isFavorite ? 'Bỏ yêu thích' : 'Yêu thích';
    }

    const contactButton = document.getElementById('nd-contactBtn');
    if (contactButton) {
      contactButton.classList.toggle('hidden', !isParentRole());
      contactButton.disabled = false;
    }

    setNannyFavoriteState(currentNannyDetailId, !!detail.isFavorite);
    document.getElementById('nd-availability').innerHTML = renderAvailability(detail.availabilitySlots || []);
    document.getElementById('nannyDetailModal')?.classList.add('show');
  } catch {
  }
}

function closeNannyDetail() {
  currentNannyDetailId = null;
  currentNannyDetailUserId = null;
  window.currentNannyDetailUserId = null;
  document.getElementById('nannyDetailModal')?.classList.remove('show');
}

function viewNannyProfileDetail(event) {
  event?.stopPropagation?.();
  if (!currentNannyDetailUserId) return;
  const params = new URLSearchParams({ userId: String(currentNannyDetailUserId) });
  if (currentNannyDetailId) params.set('nannyProfileId', String(currentNannyDetailId));
  window.location.href = `/Profile/ViewUser?${params.toString()}`;
}

function tryOpenNannyDetailFromQuery() {
  const params = new URLSearchParams(window.location.search);
  const detailId = (params.get('detailId') || '').trim();
  if (!detailId) return;
  openNannyDetail(detailId);
}

function bootstrapNannyListPage() {
  document.getElementById('nannyKeyword')?.setAttribute('autocomplete', 'off');
  initNannyMap();
  void (async () => {
    try {
      await doNannySearch();
      tryOpenNannyDetailFromQuery();
    } catch (_) {
      tryOpenNannyDetailFromQuery();
    }
  })();
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', bootstrapNannyListPage, { once: true });
} else {
  bootstrapNannyListPage();
}

window.addEventListener('pageshow', () => {
  try {
    if (nannyMap) {
      nannyMap.invalidateSize({ animate: false });
    }
  } catch (_) {
    /* ignore */
  }
});
