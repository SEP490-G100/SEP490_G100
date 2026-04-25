let nannyMap;
let nannyMarkers = [];
let nannyProfiles = [];
let nannyAllProfiles = [];
let nannySearchTimer = null;
let nannyProvinces = [];
let nannyLocationPromise = null;
let nannyProvinceCatalog = [];
let nannyProvinceCatalogPromise = null;
const nannyAutocompleteDropdowns = new Map();
const nannyAddressSuggestionCache = new Map();
const nannyDistrictOptionsCache = new Map();
const nannyGeoCache = new Map();
const nannySelectPickerSyncHandlers = [];
let nannyScheduleFilters = [];
let suppressNextNannyMapMove = false;
let currentNannyDetailId = null;
let currentNannyDetailUserId = null;
window.currentNannyDetailUserId = null;
const NANNY_DAY_LABELS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const NANNY_TIME_LABELS = ['Sáng', 'Chiều', 'Tối', 'Đêm'];
const NANNY_FALLBACK_PROVINCES = [
    'Thành phố Hà Nội',
    'Tỉnh Cao Bằng',
    'Tỉnh Tuyên Quang',
    'Tỉnh Điện Biên',
    'Tỉnh Lai Châu',
    'Tỉnh Sơn La',
    'Tỉnh Lào Cai',
    'Tỉnh Thái Nguyên',
    'Tỉnh Lạng Sơn',
    'Tỉnh Quảng Ninh',
    'Tỉnh Bắc Ninh',
    'Tỉnh Phú Thọ',
    'Thành phố Hải Phòng',
    'Tỉnh Hưng Yên',
    'Tỉnh Ninh Bình',
    'Tỉnh Thanh Hóa',
    'Tỉnh Nghệ An',
    'Tỉnh Hà Tĩnh',
    'Tỉnh Quảng Trị',
    'Thành phố Huế',
    'Thành phố Đà Nẵng',
    'Tỉnh Quảng Ngãi',
    'Tỉnh Gia Lai',
    'Tỉnh Khánh Hòa',
    'Tỉnh Đắk Lắk',
    'Tỉnh Lâm Đồng',
    'Tỉnh Đồng Nai',
    'Thành phố Hồ Chí Minh',
    'Tỉnh Tây Ninh',
    'Tỉnh Đồng Tháp',
    'Tỉnh Vĩnh Long',
    'Tỉnh An Giang',
    'Thành phố Cần Thơ',
    'Tỉnh Cà Mau'
];
const NANNY_FALLBACK_DISTRICTS_BY_CITY = {
  'ho chi minh': [
    'Quận 1', 'Quận 3', 'Quận 4', 'Quận 5', 'Quận 6', 'Quận 7', 'Quận 8',
    'Quận 10', 'Quận 11', 'Quận 12', 'Quận Bình Thạnh', 'Quận Gò Vấp',
    'Quận Phú Nhuận', 'Quận Tân Bình', 'Quận Tân Phú', 'Thành phố Thủ Đức',
    'Huyện Bình Chánh', 'Huyện Cần Giờ', 'Huyện Củ Chi', 'Huyện Hóc Môn', 'Huyện Nhà Bè'
  ],
    'ha noi': [
        'Phường Hoàn Kiếm',
        'Phường Cửa Nam',
        'Phường Ba Đình',
        'Phường Ngọc Hà',
        'Phường Giảng Võ',
        'Phường Kim Mã',
        'Phường Đống Đa',
        'Phường Ô Chợ Dừa',
        'Phường Văn Miếu - Quốc Tử Giám',
        'Phường Kim Liên',
        'Phường Láng',
        'Phường Ô Chợ Dừa',
        'Phường Hai Bà Trưng',
        'Phường Bạch Mai',
        'Phường Vĩnh Tuy',
        'Phường Thanh Nhàn',
        'Phường Hoàng Mai',
        'Phường Tương Mai',
        'Phường Định Công',
        'Phường Hoàng Liệt',
        'Phường Yên Sở',
        'Phường Thanh Xuân',
        'Phường Khương Đình',
        'Phường Phương Liệt',
        'Phường Hạ Đình',
        'Phường Cầu Giấy',
        'Phường Nghĩa Đô',
        'Phường Yên Hòa',
        'Phường Tây Hồ',
        'Phường Phú Thượng',
        'Phường Đông Ngạc',
        'Phường Thượng Cát',
        'Phường Xuân Đỉnh',
        'Phường Từ Liêm',
        'Phường Tây Tựu',
        'Phường Đại Mỗ',
        'Phường Long Biên',
        'Phường Bồ Đề',
        'Phường Việt Hưng',
        'Phường Phúc Lợi',
        'Phường Hà Đông',
        'Phường Dương Nội',
        'Phường Yên Nghĩa',
        'Phường Phú Lương',
        'Phường Kiến Hưng',
        'Phường Thanh Liệt',
        'Phường Sơn Tây',
        'Phường Tùng Thiện',

        'Xã Thanh Trì',
        'Xã Đại Thanh',
        'Xã Nam Phù',
        'Xã Ngọc Hồi',
        'Xã Thượng Phúc',
        'Xã Thường Tín',
        'Xã Chương Dương',
        'Xã Hồng Vân',
        'Xã Phú Xuyên',
        'Xã Phượng Dực',
        'Xã Chuyên Mỹ',
        'Xã Vân Đình',
        'Xã Quảng Bị',
        'Xã Quốc Oai',
        'Xã Hưng Đạo',
        'Xã Kiều Phú',
        'Xã Phú Cát',
        'Xã Hoài Đức',
        'Xã Dương Hòa',
        'Xã Sơn Đồng',
        'Xã An Khánh',
        'Xã Đan Phượng',
        'Xã Tiến Thắng'
    ],
  'da nang': ['Quận Hải Châu', 'Quận Thanh Khê', 'Quận Sơn Trà', 'Quận Ngũ Hành Sơn', 'Quận Liên Chiểu', 'Huyện Hòa Vang'],
  'can tho': ['Quận Ninh Kiều', 'Quận Bình Thủy', 'Quận Cái Răng', 'Quận Ô Môn', 'Quận Thốt Nốt'],
  'hai phong': ['Quận Hồng Bàng', 'Quận Ngô Quyền', 'Quận Lê Chân', 'Quận Hải An', 'Quận Kiến An', 'Quận Dương Kinh', 'Quận Đồ Sơn'],
  'hue': ['Quận Phú Xuân', 'Quận Thuận Hóa', 'Thị xã Hương Thủy', 'Thị xã Hương Trà']
};

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

function getAutocompleteKey(kind) {
  return `nanny-${kind}`;
}

function hideAutocomplete(kind) {
  const dropdown = nannyAutocompleteDropdowns.get(getAutocompleteKey(kind));
  if (!dropdown) return;
  dropdown.classList.remove('show');
}

function getProvinceOptions() {
  return nannyProvinces.map((province) => province.name);
}

function getDistrictOptions(cityName) {
  const normalizedCity = normalizeAdministrativeName(cityName);
  const selectedProvince = nannyProvinces.find((province) =>
    normalizeAdministrativeName(province.name) === normalizedCity
  );
  return (selectedProvince?.districts || []).map((district) => district.name);
}

function getNannyFallbackDistrictOptions(cityName) {
  return NANNY_FALLBACK_DISTRICTS_BY_CITY[normalizeAdministrativeName(cityName)] || [];
}

function getNannyDistrictCacheKey(cityName) {
  return normalizeAdministrativeName(cityName);
}

function cacheNannyDistrictOptions(cityName, values) {
  nannyDistrictOptionsCache.set(getNannyDistrictCacheKey(cityName), values);
  return values;
}

function getCachedNannyDistrictOptions(cityName) {
  return nannyDistrictOptionsCache.get(getNannyDistrictCacheKey(cityName)) || [];
}

function loadNannyProvinceCatalog() {
  if (nannyProvinceCatalogPromise) return nannyProvinceCatalogPromise;

  nannyProvinceCatalogPromise = fetch('https://provinces.open-api.vn/api/v2/p/')
    .then((response) => response.ok ? response.json() : [])
    .then((data) => {
      nannyProvinceCatalog = Array.isArray(data) ? data : [];
      return nannyProvinceCatalog;
    })
    .catch(() => {
      nannyProvinceCatalog = [];
      return nannyProvinceCatalog;
    });

  return nannyProvinceCatalogPromise;
}

async function fetchNannyDistrictOptionsByCity(cityName) {
  const normalizedCity = String(cityName ?? '').trim();
  if (!normalizedCity) return [];

  const cached = getCachedNannyDistrictOptions(normalizedCity);
  if (cached.length) return cached;

  const localOptions = getDistrictOptions(normalizedCity);
  if (localOptions.length) {
    return cacheNannyDistrictOptions(normalizedCity, localOptions);
  }

  const fallbackOptions = getNannyFallbackDistrictOptions(normalizedCity);
  if (fallbackOptions.length) {
    return cacheNannyDistrictOptions(normalizedCity, fallbackOptions);
  }

  try {
    const response = await fetch(`/Address/Districts?city=${encodeURIComponent(normalizedCity)}`, {
      credentials: 'same-origin'
    });
    if (!response.ok) return [];

    const data = await response.json();
    const districts = Array.isArray(data)
      ? data.filter(Boolean)
      : [];
    if (districts.length) {
      return cacheNannyDistrictOptions(normalizedCity, districts);
    }
    return cacheNannyDistrictOptions(normalizedCity, fallbackOptions);
  } catch {
    return cacheNannyDistrictOptions(normalizedCity, fallbackOptions);
  }
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

function uniqueNannyValues(values, query) {
  const seen = new Set();
  return values
    .filter((value) => value && (!query || normalizeText(value).includes(normalizeText(query))))
    .filter((value) => {
      const key = normalizeText(value);
      if (!key || seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .slice(0, 12);
}

async function getProvinceOptionsAsync(query) {
  if (nannyProvinces.length) {
    return uniqueNannyValues(getProvinceOptions(), query);
  }

  if (!String(query ?? '').trim()) {
    return [...NANNY_FALLBACK_PROVINCES];
  }

  const suggestions = await fetchNannyAddressSuggestions(query);
  const values = uniqueNannyValues(suggestions.map((item) => item.city), query);
  return values.length ? values : uniqueNannyValues(NANNY_FALLBACK_PROVINCES, query);
}

async function getDistrictOptionsAsync(cityName, query) {
  const normalizedCity = String(cityName ?? '').trim();
  if (!normalizedCity) return [];
  const normalizedCityKey = normalizeAdministrativeName(normalizedCity);

  const localValues = uniqueNannyValues(await fetchNannyDistrictOptionsByCity(normalizedCity), query);
  if (localValues.length) {
    return localValues;
  }

  if (!String(query ?? '').trim()) {
    return [];
  }

  const compositeQuery = [query, normalizedCity].filter(Boolean).join(', ');
  const suggestions = await fetchNannyAddressSuggestions(compositeQuery);
  return uniqueNannyValues(
    suggestions
      .filter((item) => {
        if (!item.city) return true;
        return normalizeAdministrativeName(item.city).includes(normalizedCityKey);
      })
      .map((item) => item.district),
    query
  );
}

function handleNannyCityChange(explicitCityValue) {
  const cityInput = document.getElementById('nannyCity');
  const districtInput = document.getElementById('nannyDistrict');
  if (!cityInput) return;

  const cityValue = (explicitCityValue ?? cityInput.value).trim();
  const allowedDistricts = getCachedNannyDistrictOptions(cityValue);
  if (districtInput && districtInput.value && !allowedDistricts.includes(districtInput.value.trim())) {
    districtInput.value = '';
  }

  if (cityValue) {
    fetchNannyDistrictOptionsByCity(cityValue);
  }
}

function renderAutocompleteOptions(kind, options, onSelect) {
  const dropdown = nannyAutocompleteDropdowns.get(getAutocompleteKey(kind));
  if (!dropdown) return;

  if (!options.length) {
    dropdown.innerHTML = '';
    dropdown.classList.remove('show');
    return;
  }

  dropdown.innerHTML = options.map((option) => `<li data-value="${escapeHtml(option)}">${escapeHtml(option)}</li>`).join('');
  dropdown.classList.add('show');

  dropdown.querySelectorAll('li').forEach((item) => {
    item.addEventListener('mousedown', (event) => {
      event.preventDefault();
      onSelect(item.dataset.value || '');
    });
  });
}

function getAutocompleteInputId(kind) {
  if (kind === 'city') return 'nannyCity';
  if (kind === 'district') return 'nannyDistrict';
  return '';
}

function attachAutocomplete(kind, optionGetter, onSelect) {
  const inputId = getAutocompleteInputId(kind);
  const input = inputId ? document.getElementById(inputId) : null;
  if (!input || input.dataset.acReady === 'true') return;

  input.dataset.acReady = 'true';
  input.parentElement?.classList.add('autocomplete-field');

  const dropdown = document.createElement('ul');
  dropdown.className = 'ac-dropdown';
  input.insertAdjacentElement('afterend', dropdown);
  nannyAutocompleteDropdowns.set(getAutocompleteKey(kind), dropdown);

  let requestToken = 0;
  const showForQuery = async () => {
    const currentToken = ++requestToken;
    const rawQuery = input.value.trim();
    const filtered = await Promise.resolve(optionGetter(rawQuery));
    if (currentToken !== requestToken) return;

    renderAutocompleteOptions(kind, filtered, (value) => {
      input.value = value;
      onSelect(value);
      hideAutocomplete(kind);
    });
  };

  input.addEventListener('focus', showForQuery);
  input.addEventListener('input', showForQuery);
  input.addEventListener('blur', () => {
    setTimeout(() => hideAutocomplete(kind), 120);
  });
}

function attachLocationAutocomplete() {
  attachAutocomplete('city', (query) => getProvinceOptionsAsync(query), (value) => {
    const districtInput = document.getElementById('nannyDistrict');
    if (districtInput) districtInput.value = '';
    handleNannyCityChange(value);
  });

  attachAutocomplete(
    'district',
    (query) => getDistrictOptionsAsync(document.getElementById('nannyCity')?.value.trim() || '', query),
    () => {}
  );
}

function attachSelectPicker(kind, selectId, textInputId, emptyLabel) {
  const select = document.getElementById(selectId);
  const input = document.getElementById(textInputId);
  if (!select || !input || input.dataset.acReady === 'true') return;

  input.dataset.acReady = 'true';
  // Non-location pickers are click-to-select only.
  input.readOnly = true;
  input.setAttribute('aria-readonly', 'true');
  input.parentElement?.classList.add('autocomplete-field');
  input.classList.add('nanny-filter--picker');

  const dropdown = document.createElement('ul');
  dropdown.className = 'ac-dropdown';
  input.insertAdjacentElement('afterend', dropdown);
  nannyAutocompleteDropdowns.set(getAutocompleteKey(kind), dropdown);

  const syncTextFromSelect = () => {
    const selectedOption = select.options[select.selectedIndex];
    input.value = selectedOption ? selectedOption.text.trim() : emptyLabel;
  };

  if (!nannySelectPickerSyncHandlers.includes(syncTextFromSelect)) {
    nannySelectPickerSyncHandlers.push(syncTextFromSelect);
  }

  const getFilteredOptions = (query) => {
    const normalizedQuery = normalizeText(query);
    return Array.from(select.options)
      .map((option) => ({ value: option.value, label: option.text.trim() }))
      .filter((option) => option.label && (!normalizedQuery || normalizeText(option.label).includes(normalizedQuery)))
      .slice(0, 16);
  };

  const renderOptions = (query) => {
    const options = getFilteredOptions(query);
    if (!options.length) {
      dropdown.innerHTML = '';
      dropdown.classList.remove('show');
      return;
    }

    dropdown.innerHTML = options.map((option) => `
      <li data-value="${escapeHtml(option.value)}" data-label="${escapeHtml(option.label)}">${escapeHtml(option.label)}</li>
    `).join('');
    dropdown.classList.add('show');

    dropdown.querySelectorAll('li').forEach((item) => {
      item.addEventListener('mousedown', (event) => {
        event.preventDefault();
        select.value = item.dataset.value || '';
        input.value = item.dataset.label || emptyLabel;
        hideAutocomplete(kind);
      });
    });
  };

  syncTextFromSelect();
  input.addEventListener('focus', () => renderOptions(input.value === emptyLabel ? '' : input.value.trim()));
  input.addEventListener('click', () => renderOptions(input.value === emptyLabel ? '' : input.value.trim()));
  input.addEventListener('blur', () => {
    setTimeout(() => {
      hideAutocomplete(kind);
      syncTextFromSelect();
    }, 120);
  });
}

function syncSelectPickerText() {
  nannySelectPickerSyncHandlers.forEach((syncFn) => syncFn());
}

function attachSkillAndVerificationAutocomplete() {
  attachSelectPicker('skill', 'nannySkillId', 'nannySkillText', 'Tất cả kỹ năng');
  attachSelectPicker('verification', 'nannyVerification', 'nannyVerificationText', 'Tất cả trạng thái');
}

function getNannyScheduleFilterKey(dayOfWeek, timeSlot) {
  return `${dayOfWeek}-${timeSlot}`;
}

function renderNannyScheduleFilter() {
  const container = document.getElementById('nannyScheduleFilter');
  if (!container) return;

  const selected = new Set(
    nannyScheduleFilters.map((slot) => getNannyScheduleFilterKey(slot.dayOfWeek, slot.timeSlot))
  );

  let html = '<div></div>';
  NANNY_DAY_LABELS.forEach((label) => { html += `<div class="nanny-schedule-col-label">${label}</div>`; });

  NANNY_TIME_LABELS.forEach((rowLabel, timeSlot) => {
    html += `<div class="nanny-schedule-row-label">${rowLabel}</div>`;
    for (let dayOfWeek = 0; dayOfWeek < 7; dayOfWeek += 1) {
      const isActive = selected.has(getNannyScheduleFilterKey(dayOfWeek, timeSlot));
      html += `
        <button type="button"
                class="nanny-schedule-cell ${isActive ? 'active' : ''}"
                onclick="toggleNannyScheduleFilter(${dayOfWeek}, ${timeSlot})"
                aria-label="${rowLabel} - ${NANNY_DAY_LABELS[dayOfWeek]}">
          <span class="nanny-schedule-check">${isActive ? '&#10003;' : ''}</span>
        </button>`;
    }
  });

  container.innerHTML = html;
}

function toggleNannyScheduleFilter(dayOfWeek, timeSlot) {
  const key = getNannyScheduleFilterKey(dayOfWeek, timeSlot);
  const index = nannyScheduleFilters.findIndex((slot) => getNannyScheduleFilterKey(slot.dayOfWeek, slot.timeSlot) === key);
  if (index >= 0) nannyScheduleFilters.splice(index, 1);
  else nannyScheduleFilters.push({ dayOfWeek, timeSlot });
  renderNannyScheduleFilter();
}

function applyNannyScheduleFilter(items) {
  if (!Array.isArray(items) || !nannyScheduleFilters.length) return Array.isArray(items) ? items : [];
  const selected = new Set(
    nannyScheduleFilters.map((slot) => getNannyScheduleFilterKey(slot.dayOfWeek, slot.timeSlot))
  );

  return items.filter((profile) => {
    const slots = Array.isArray(profile?.availabilitySlots) ? profile.availabilitySlots : [];
    return slots.some((slot) => selected.has(getNannyScheduleFilterKey(Number(slot.dayOfWeek), Number(slot.timeSlot))));
  });
}

function loadLocationData() {
  if (nannyLocationPromise) return nannyLocationPromise;

  // Attach autocomplete ngay, để người dùng vẫn chọn được khi API ngoài chậm/lỗi.
  attachLocationAutocomplete();

  nannyLocationPromise = fetch('https://provinces.open-api.vn/api/v2/?depth=2')
    .then((response) => (response.ok ? response.json() : []))
    .then((data) => {
      nannyProvinces = Array.isArray(data) ? data : [];
      return nannyProvinces;
    })
    .catch(() => {
      nannyProvinces = [];
      return nannyProvinces;
    });

  return nannyLocationPromise;
}

function openNannyFilters() {
  renderNannyScheduleFilter();
  document.getElementById('nannyFilterModal')?.classList.add('show');
}

function closeNannyFilters() {
  document.getElementById('nannyFilterModal')?.classList.remove('show');
}

function resetNannyFilters() {
  const ids = [
    'nannyCity',
    'nannyDistrict',
    'nannySkillId',
    'nannySkillText',
    'nannyVerification',
    'nannyVerificationText',
    'nannyExperience',
    'nannyAgeMin',
    'nannySalaryMin',
    'nannySalaryMax'
  ];

  ids.forEach((id) => {
    const el = document.getElementById(id);
    if (el) el.value = '';
  });
  nannyScheduleFilters = [];
  syncSelectPickerText();
  renderNannyScheduleFilter();

  doNannySearch();
}

function applyNannyFilters() {
  closeNannyFilters();
  doNannySearch();
}

function initNannyMap() {
  const mapEl = document.getElementById('nannyMap');
  if (nannyMap || !mapEl || typeof L === 'undefined') return;

  nannyMap = L.map('nannyMap', { zoomControl: true }).setView([10.776, 106.701], 11);
  L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    attribution: '&copy; CartoDB',
    maxZoom: 19
  }).addTo(nannyMap);

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
  if (active && openPopup) markerData.marker?.openPopup();
  if (!active) markerData.marker?.closePopup();
}

function focusNannyMarker(idx) {
  const markerData = nannyMarkers[idx];
  if (!markerData || !nannyMap) return;
  suppressNextNannyMapMove = true;
  nannyMap.flyTo([markerData.point.lat, markerData.point.lng], markerData.point.zoom || 13, { duration: 0.4 });
  setNannyMarkerHover(idx, true, true);
}

function renderNannyCards(items, options = {}) {
  const { fitToMarkers = false } = options;
  const list = document.getElementById('nannyList');
  const count = document.getElementById('nannyResultCount');
  if (!list || !count) return;

  count.textContent = `${items.length} hồ sơ`;
  clearNannyMarkers();

  if (!items.length) {
    list.innerHTML = `
      <div class="nanny-empty">
        <span class="material-icons-round">search_off</span>
        <h3>Không tìm thấy hồ sơ phù hợp</h3>
        <p>Thử thay đổi từ khóa, khu vực hoặc kỹ năng để mở rộng kết quả.</p>
      </div>`;
    return;
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

  if (fitToMarkers && nannyMap && nannyMarkers.length) {
    const bounds = L.latLngBounds(
      nannyMarkers.map((entry) => [entry.point.lat, entry.point.lng])
    );
    suppressNextNannyMapMove = true;
    nannyMap.fitBounds(bounds, { padding: [24, 24], maxZoom: 13 });
  }

  list.querySelectorAll('.nanny-card').forEach((card) => {
    const idx = Number(card.dataset.idx);
    card.addEventListener('mouseenter', () => setNannyMarkerHover(idx, true, true));
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
  const city = document.getElementById('nannyCity')?.value.trim();
  const district = document.getElementById('nannyDistrict')?.value.trim();
  const skillId = document.getElementById('nannySkillId')?.value.trim();
  const verificationStatus = document.getElementById('nannyVerification')?.value.trim();
  const minExperience = document.getElementById('nannyExperience')?.value.trim();
  const minAge = document.getElementById('nannyAgeMin')?.value.trim();
  const minExpectedSalary = document.getElementById('nannySalaryMin')?.value.trim();
  const maxExpectedSalary = document.getElementById('nannySalaryMax')?.value.trim();
  const singleScheduleFilter = nannyScheduleFilters.length === 1 ? nannyScheduleFilters[0] : null;

  if (keyword) params.append('keyword', keyword);
  if (city) params.append('city', city);
  if (district) params.append('district', district);
  if (skillId) params.append('skillIds', skillId);
  if (verificationStatus) params.append('verificationStatus', verificationStatus);
  if (minExperience) params.append('minExperience', minExperience);
  if (minAge) params.append('minAge', minAge);
  if (minExpectedSalary) params.append('minExpectedSalary', minExpectedSalary);
  if (maxExpectedSalary) params.append('maxExpectedSalary', maxExpectedSalary);
  if (singleScheduleFilter) {
    params.append('dayOfWeek', String(singleScheduleFilter.dayOfWeek));
    params.append('timeSlot', String(singleScheduleFilter.timeSlot));
  }

  try {
    const response = await fetch(`/Nanny/BrowseData?${params.toString()}`, { credentials: 'same-origin' });
    const json = await response.json();
    const rawProfiles = Array.isArray(json.data) ? json.data : [];
    await hydrateNannyGeoForMap(rawProfiles);
    nannyAllProfiles = nannyScheduleFilters.length > 1 ? applyNannyScheduleFilter(rawProfiles) : rawProfiles;
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
  ['nannyCity', 'nannyDistrict', 'nannySkillText', 'nannyVerificationText'].forEach((id) => {
    document.getElementById(id)?.setAttribute('autocomplete', 'off');
  });
  attachSkillAndVerificationAutocomplete();
  renderNannyScheduleFilter();
  initNannyMap();
  loadLocationData();
  doNannySearch();
  tryOpenNannyDetailFromQuery();
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', bootstrapNannyListPage, { once: true });
} else {
  bootstrapNannyListPage();
}
