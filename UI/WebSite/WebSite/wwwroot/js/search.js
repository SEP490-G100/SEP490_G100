const JOB_TYPES = { 1: 'Toàn thời gian', 2: 'Bán thời gian', 3: 'Qua đêm' };
const MODERATION_LABELS = { 0: 'Đang chờ duyệt', 1: 'Đã bị từ chối', 2: 'Công khai' };
const POST_STATUS_LABELS = { 1: 'Công khai', 2: 'Ẩn bài đăng' };

let map;
let markers = [];
let currentJobs = [];
let createChildren = [];
let editChildren = [];
let createSkills = [];
let editSkills = [];
let createSchedule = [];
let editSchedule = [];
let debounceTimer = null;
let editingJobId = null;
let isSubmittingCreate = false;
let isSubmittingEdit = false;
let provinces = [];
let locationDataPromise = null;
let suppressNextMapSearch = false;
const autocompleteDropdowns = new Map();
const selectPickerSyncFns = [];
const addressSuggestionCache = new Map();
const districtOptionsCache = new Map();
const ownedJobIds = new Set();
const appliedJobIds = new Set();
let ownedJobsLoaded = false;

const DAY_LABELS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const ROW_LABELS = ['Sáng', 'Chiều', 'Tối', 'Đêm'];

const GEO_FALLBACK = {
  'Ho Chi Minh': { lat: 10.776, lng: 106.701, radius: 7000, zoom: 11 },
  'Thanh pho Ho Chi Minh': { lat: 10.776, lng: 106.701, radius: 7000, zoom: 11 },
  'Ha Noi': { lat: 21.028, lng: 105.854, radius: 7000, zoom: 11 },
  'Da Nang': { lat: 16.054, lng: 108.202, radius: 6500, zoom: 11 }
};

let pendingComplainJob = null;
let isSubmittingJobComplain = false;
let complainEditorsInitialized = false;

function loadLocationData() {
  if (locationDataPromise) return locationDataPromise;

  locationDataPromise = fetch('/Address/LocationTree', { credentials: 'same-origin' })
    .then((response) => response.ok ? response.json() : [])
    .then((data) => {
      provinces = Array.isArray(data) ? data : [];
      bindLocationInputs('cf');
      return provinces;
    })
    .catch(() => {
      provinces = [];
      bindLocationInputs('cf');
      return provinces;
    });

  return locationDataPromise;
}


function normalizeText(value) {
  return String(value ?? '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[đĐ]/g, 'd')
    .toLowerCase()
    .trim();
}

function normalizeAdministrativeName(value) {
  return normalizeText(value)
    .replace(/^thanh pho\s+/i, '')
    .replace(/^tp\.?\s*/i, '')
    .replace(/^tinh\s+/i, '');
}

function getProvinceOptions() {
  return provinces.map((province) => province.name);
}

function getDistrictOptions(cityName) {
  const normalizedCity = normalizeAdministrativeName(cityName);
  const selectedProvince = provinces.find((province) =>
    normalizeAdministrativeName(province.name) === normalizedCity
  );
  return (selectedProvince?.districts || []).map((district) => district.name);
}


function getDistrictCacheKey(cityName) {
  return normalizeAdministrativeName(cityName);
}

function cacheDistrictOptions(cityName, values) {
  districtOptionsCache.set(getDistrictCacheKey(cityName), values);
  return values;
}

function getCachedDistrictOptions(cityName) {
  return districtOptionsCache.get(getDistrictCacheKey(cityName)) || [];
}


async function fetchDistrictOptionsByCity(cityName) {
  const normalizedCity = String(cityName ?? '').trim();
  if (!normalizedCity) return [];

  const cached = getCachedDistrictOptions(normalizedCity);
  if (cached.length) return cached;

  return cacheDistrictOptions(normalizedCity, getDistrictOptions(normalizedCity));
}

async function fetchAddressSuggestions(query) {
  const normalized = String(query ?? '').trim();
  if (normalized.length < 2) return [];

  const cacheKey = normalizeText(normalized);
  if (addressSuggestionCache.has(cacheKey)) {
    return addressSuggestionCache.get(cacheKey) || [];
  }

  try {
    const response = await fetch(`/Address/Suggest?q=${encodeURIComponent(normalized)}&limit=10`, { credentials: 'same-origin' });
    if (!response.ok) return [];
    const json = await response.json();
    const items = Array.isArray(json) ? json : [];
    addressSuggestionCache.set(cacheKey, items);
    return items;
  } catch {
    return [];
  }
}

function uniqueNormalizedValues(values, query) {
  const seen = new Set();
  return values
    .filter((value) => value && (!query || normalizeText(value).includes(normalizeText(query))))
    .filter((value) => {
      const key = normalizeText(value);
      if (!key || seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .slice(0, 40);
}

async function getProvinceOptionsAsync(query) {
  return uniqueNormalizedValues(getProvinceOptions(), query);
}

async function getDistrictOptionsAsync(cityName, query) {
  const normalizedCity = String(cityName ?? '').trim();
  if (!normalizedCity) return [];
  const normalizedCityKey = normalizeAdministrativeName(normalizedCity);

  const localValues = uniqueNormalizedValues(await fetchDistrictOptionsByCity(normalizedCity), query);
  if (localValues.length) {
    return localValues;
  }

  if (!String(query ?? '').trim()) {
    return [];
  }

  const compositeQuery = [query, normalizedCity].filter(Boolean).join(', ');
  const suggestions = await fetchAddressSuggestions(compositeQuery);
  return uniqueNormalizedValues(
    suggestions
      .filter((item) => {
        if (!item.city) return true;
        return normalizeAdministrativeName(item.city).includes(normalizedCityKey);
      })
      .map((item) => item.district),
    query
  );
}

function getAutocompleteKey(prefix, kind) {
  return `${prefix}-${kind}`;
}

function hideAutocomplete(prefix, kind) {
  const dropdown = autocompleteDropdowns.get(getAutocompleteKey(prefix, kind));
  if (!dropdown) return;
  dropdown.classList.remove('show');
}

function renderAutocompleteOptions(prefix, kind, options, onSelect) {
  const dropdown = autocompleteDropdowns.get(getAutocompleteKey(prefix, kind));
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

function attachAutocomplete(prefix, kind, optionGetter, onSelect) {
  const input = document.getElementById(`${prefix}-${kind}`);
  if (!input || input.dataset.acReady === 'true') return;

  input.dataset.acReady = 'true';
  input.removeAttribute('list');
  input.parentElement?.classList.add('autocomplete-field');

  const datalist = document.getElementById(`${prefix}-${kind}Options`);
  if (datalist) datalist.remove();

  const dropdown = document.createElement('ul');
  dropdown.className = 'ac-dropdown';
  input.insertAdjacentElement('afterend', dropdown);
  autocompleteDropdowns.set(getAutocompleteKey(prefix, kind), dropdown);

  let requestToken = 0;
  const showForQuery = async () => {
    const currentToken = ++requestToken;
    const rawQuery = input.value.trim();
    const filtered = await Promise.resolve(optionGetter(rawQuery));
    if (currentToken !== requestToken) return;

    renderAutocompleteOptions(prefix, kind, filtered, (value) => {
      input.value = value;
      onSelect(value);
      hideAutocomplete(prefix, kind);
    });
  };

  input.addEventListener('focus', showForQuery);
  input.addEventListener('input', showForQuery);
  input.addEventListener('blur', () => {
    setTimeout(() => hideAutocomplete(prefix, kind), 120);
  });
}

function attachLocationAutocomplete(prefix) {
  attachAutocomplete(
    prefix,
    'city',
    (query) => getProvinceOptionsAsync(query),
    (value) => {
      const districtInput = document.getElementById(`${prefix}-district`);
      if (districtInput) districtInput.value = '';
      handleCityChange(prefix, value);
    }
  );

  attachAutocomplete(
    prefix,
    'district',
    (query) => getDistrictOptionsAsync(document.getElementById(`${prefix}-city`)?.value.trim() || '', query),
    () => {}
  );
}

function bindLocationInputs(prefix) {
  attachLocationAutocomplete(prefix);

  const cityInput = document.getElementById(`${prefix}-city`);
  if (!cityInput || cityInput.dataset.locationBindReady === 'true') return;

  cityInput.dataset.locationBindReady = 'true';
  cityInput.addEventListener('input', () => handleCityChange(prefix));
  cityInput.addEventListener('change', () => handleCityChange(prefix));
}

function attachSelectPicker(prefix, kind, selectId, inputId, emptyLabel, onValueChanged, alwaysShowAllOnOpen = false) {
  const select = document.getElementById(selectId);
  const input = document.getElementById(inputId);
  if (!select || !input || input.dataset.acReady === 'true') return;

  input.dataset.acReady = 'true';
  // Non-location pickers are click-to-select only.
  input.readOnly = true;
  input.setAttribute('aria-readonly', 'true');
  input.parentElement?.classList.add('autocomplete-field');

  const dropdown = document.createElement('ul');
  dropdown.className = 'ac-dropdown';
  input.insertAdjacentElement('afterend', dropdown);
  autocompleteDropdowns.set(getAutocompleteKey(prefix, kind), dropdown);
  const normalizedEmptyLabel = normalizeText(emptyLabel);

  const syncFromSelect = () => {
    const selectedOption = select.options[select.selectedIndex];
    input.value = selectedOption ? selectedOption.text.trim() : emptyLabel;
  };

  selectPickerSyncFns.push(syncFromSelect);

  const buildOptions = (query) => {
    const normalizedQuery = normalizeText(query);
    return Array.from(select.options)
      .map((option) => ({ value: option.value, label: option.text.trim() }))
      .filter((option) => option.label && (!normalizedQuery || normalizeText(option.label).includes(normalizedQuery)))
      .slice(0, 16);
  };

  const renderOptions = (query) => {
    const options = buildOptions(query);
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
        if (typeof onValueChanged === 'function') onValueChanged(select.value);
        hideAutocomplete(prefix, kind);
      });
    });
  };

  const openPickerQuery = () => {
    const currentValue = input.value.trim();
    const shouldOpenAll = alwaysShowAllOnOpen
      || !currentValue
      || select.value === ''
      || normalizeText(currentValue) === normalizedEmptyLabel;
    return shouldOpenAll ? '' : currentValue;
  };

  syncFromSelect();
  input.addEventListener('focus', () => {
    renderOptions(openPickerQuery());
  });
  input.addEventListener('click', () => {
    renderOptions(openPickerQuery());
  });
  input.addEventListener('blur', () => {
    setTimeout(() => {
      hideAutocomplete(prefix, kind);
      syncFromSelect();
    }, 120);
  });
}

function syncSelectPickerTexts() {
  selectPickerSyncFns.forEach((syncFn) => syncFn());
}

function attachCreateSelectPickers() {
  attachSelectPicker('cf', 'typePicker', 'cf-type', 'cf-typeText', 'Chọn loại công việc');
  attachSelectPicker('cf', 'childPicker', 'cf-childProfileId', 'cf-childProfileText', 'Chọn bé thứ 1', () => handleCreateChildChange(), true);
  attachSelectPicker('cf', 'skillPicker', 'cf-skillSelect', 'cf-skillSelectText', 'Chọn kỹ năng cần yêu cầu');
}

function handleCityChange(prefix, explicitCityValue) {
  const cityInput = document.getElementById(`${prefix}-city`);
  const districtInput = document.getElementById(`${prefix}-district`);
  if (!cityInput) return;

  const cityValue = (explicitCityValue ?? cityInput.value).trim();
  const allowedDistricts = getCachedDistrictOptions(cityValue);
  if (districtInput && districtInput.value && !allowedDistricts.includes(districtInput.value.trim())) {
    districtInput.value = '';
  }

  if (cityValue) {
    fetchDistrictOptionsByCity(cityValue);
  }
}

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function escapeJs(value) {
  return String(value ?? '').replaceAll('\\', '\\\\').replaceAll("'", "\\'");
}

function formatSalary(value) {
  if (!value) return 'Không xác định';
  const number = Number(value);
  if (!Number.isFinite(number) || number <= 0) return 'Không xác định';
  return new Intl.NumberFormat('vi-VN').format(number) + ' VND';
}

function formatSalaryRange(min, max, negotiable) {
  if (negotiable) return 'Thỏa thuận';
  if (min && max) return `${formatSalary(min)} - ${formatSalary(max)}`;
  if (min) return `Từ ${formatSalary(min)}`;
  if (max) return `Đến ${formatSalary(max)}`;
  return 'Không xác định';
}

function getJobPlanLabel(job) {
  const code = String(job?.subscriptionPlanCode || '').trim().toUpperCase();
  if (code === 'PRO') return 'Phụ huynh Pro';
  if (code === 'PLUS') return 'Phụ huynh Plus';
  return '';
}

function renderJobBenefitBadges(job) {
  const badges = [];
  const planLabel = getJobPlanLabel(job);

  if (planLabel) {
    badges.push(`<span class="px-3 py-1 rounded-full bg-amber-50 text-amber-700 text-xs font-bold">${escapeHtml(planLabel)}</span>`);
  }

  if (job?.searchPriority) {
    badges.push('<span class="px-3 py-1 rounded-full bg-amber-50 text-amber-700 text-xs font-bold">Ưu tiên tìm kiếm</span>');
  } else if (job?.featuredBadge) {
    badges.push('<span class="px-3 py-1 rounded-full bg-orange-50 text-orange-700 text-xs font-bold">Tin nổi bật</span>');
  }

  return badges.join('');
}

function sanitizeMoneyDigits(value) {
  return String(value ?? '').replace(/[^\d]/g, '');
}

function formatMoneyInputValue(value) {
  const digits = sanitizeMoneyDigits(value);
  if (!digits) return '';
  const normalized = digits.replace(/^0+(?=\d)/, '');
  const amount = Number(normalized || '0');
  return Number.isFinite(amount) ? amount.toLocaleString('vi-VN') : '';
}

function parseMoneyInput(value) {
  const digits = sanitizeMoneyDigits(value);
  if (!digits) return null;
  const normalized = digits.replace(/^0+(?=\d)/, '');
  const amount = Number(normalized || '0');
  return Number.isFinite(amount) ? amount : null;
}

function bindMoneyInputFormatting(inputId) {
  const input = document.getElementById(inputId);
  if (!input || input.dataset.moneyReady === 'true') return;

  input.dataset.moneyReady = 'true';
  const applyFormat = () => {
    input.value = formatMoneyInputValue(input.value);
  };

  input.addEventListener('input', applyFormat);
  input.addEventListener('blur', applyFormat);
  applyFormat();
}

function initMoneyInputs() {
  ['cf-salMin', 'cf-salMax', 'ef-salMin', 'ef-salMax'].forEach(bindMoneyInputFormatting);
}

function formatAgeRange(min, max) {
  if (min && max) return `${min} - ${max} tuổi`;
  if (min) return `Từ ${min} tuổi`;
  if (max) return `Đến ${max} tuổi`;
  return 'Không yêu cầu';
}

function notifyToast(message, type = 'info') {
  if (typeof window.showToast === 'function') {
    window.showToast(message, { type });
  }
}

function isLoggedIn() {
  return typeof IS_AUTH !== 'undefined' && IS_AUTH === true;
}

function isNannyRole() {
  return typeof IS_NANNY !== 'undefined' && IS_NANNY === true;
}

function normalizeGuid(value) {
  return String(value ?? '').trim().toLowerCase();
}

function setOwnedJobs(collection) {
  ownedJobIds.clear();
  (Array.isArray(collection) ? collection : []).forEach((job) => {
    if (job?.id) ownedJobIds.add(normalizeGuid(job.id));
  });
}

function canEditJob(job) {
  if (!isLoggedIn() || !job?.id) return false;
  return ownedJobIds.has(normalizeGuid(job.id));
}

window.canEditJob = canEditJob;

function isJobApplied(jobId) {
  return appliedJobIds.has(normalizeGuid(jobId));
}

function setJobAppliedState(jobId, isApplied) {
  const normalizedId = normalizeGuid(jobId);
  if (!normalizedId) return;

  if (isApplied) appliedJobIds.add(normalizedId);
  else appliedJobIds.delete(normalizedId);

  currentJobs = currentJobs.map((job) => {
    if (normalizeGuid(job?.id) === normalizedId) return { ...job, isApplied: !!isApplied };
    return job;
  });
}

function updateJobFavoriteUi(jobId, isFavorite) {
  const normalizedId = normalizeGuid(jobId);
  document.querySelectorAll(`.job-save-btn[data-job-id="${normalizedId}"]`).forEach((button) => {
    button.classList.toggle('active', !!isFavorite);
    button.setAttribute('aria-pressed', isFavorite ? 'true' : 'false');
    button.title = isFavorite ? 'Bỏ lưu bài đăng' : 'Lưu bài đăng';
    button.setAttribute('aria-label', button.title);
    const icon = button.querySelector('.material-icons-round');
    if (icon) icon.textContent = isFavorite ? 'bookmark' : 'bookmark_border';
  });
}

function setJobFavoriteState(jobId, isFavorite) {
  const normalizedId = normalizeGuid(jobId);
  currentJobs = currentJobs.map((job) => {
    if (normalizeGuid(job?.id) === normalizedId) return { ...job, isFavorite: !!isFavorite };
    return job;
  });
  updateJobFavoriteUi(jobId, !!isFavorite);
}

async function toggleJobFavorite(jobId, event) {
  event?.stopPropagation?.();

  if (!isLoggedIn()) {
    notifyToast('Vui lòng đăng nhập để lưu bài đăng.', 'error');
    return;
  }

  if (!isNannyRole()) {
    notifyToast('Chỉ bảo mẫu mới có quyền lưu bài đăng.', 'error');
    return;
  }

  try {
    const response = await fetch(`/Search/ToggleFavoriteJob?jobPostingId=${encodeURIComponent(jobId)}`, {
      method: 'POST',
      credentials: 'same-origin'
    });
    const json = await response.json();
    const payload = json?.raw && typeof json.raw === 'object' ? json.raw : json;
    const isSuccess = !!(json?.success || payload?.success);

    if (!isSuccess) {
      notifyToast(json?.message || 'Không thể cập nhật lưu bài đăng.', 'error');
      return;
    }

    const favoriteState = typeof payload?.isFavorite === 'boolean'
      ? payload.isFavorite
      : !!json?.isFavorite;

    setJobFavoriteState(jobId, favoriteState);
    notifyToast(
      payload?.message || json?.message || (favoriteState ? 'Đã lưu bài đăng.' : 'Đã bỏ lưu bài đăng.'),
      'success'
    );
  } catch {
    notifyToast('Không thể cập nhật lưu bài đăng.', 'error');
  }
}

async function loadOwnedJobs() {
  if (!isLoggedIn()) {
    setOwnedJobs([]);
    ownedJobsLoaded = true;
    return;
  }

  try {
    const res = await fetch('/Search/MyJobs', { credentials: 'same-origin' });
    const json = await res.json();
    if (json?.success) setOwnedJobs(json.data);
    else setOwnedJobs([]);
  } catch {
    setOwnedJobs([]);
  } finally {
    ownedJobsLoaded = true;
  }
}

function canApplyJob(job) {
  if (!isLoggedIn() || !isNannyRole() || !job?.id) return false;
  if (canEditJob(job)) return false;

  const moderationStatus = Number(job.moderationStatus);
  const postStatus = Number(job.status);
  return moderationStatus === 2 && postStatus === 1;
}

function canComplainJob(job) {
  if (!isLoggedIn() || !job?.id) return false;
  if (canEditJob(job)) return false;
  return true;
}

function extractPlainText(html) {
  const div = document.createElement('div');
  div.innerHTML = String(html ?? '');
  return (div.textContent || div.innerText || '').replace(/\s+/g, ' ').trim();
}

function getComplainEditorContent(editorId) {
  if (typeof tinymce !== 'undefined') {
    const editor = tinymce.get(editorId);
    if (editor) return editor.getContent();
  }
  const input = document.getElementById(editorId);
  return input ? (input.value || '') : '';
}

function setComplainEditorContent(editorId, value) {
  if (typeof tinymce !== 'undefined') {
    const editor = tinymce.get(editorId);
    if (editor) {
      editor.setContent(value || '');
      return;
    }
  }
  const input = document.getElementById(editorId);
  if (input) input.value = value || '';
}

function appendToComplainEvidence(htmlSnippet) {
  if (!htmlSnippet) return;

  if (typeof tinymce !== 'undefined') {
    const editor = tinymce.get('jobComplainEvidence');
    if (editor) {
      editor.insertContent(htmlSnippet);
      return;
    }
  }

  const evidenceEl = document.getElementById('jobComplainEvidence');
  if (!evidenceEl) return;
  evidenceEl.value = `${evidenceEl.value || ''}\n${extractPlainText(htmlSnippet)}`.trim();
}

function ensureComplainEditorsInitialized() {
  if (complainEditorsInitialized) return;
  if (typeof tinymce === 'undefined') return;

  tinymce.init({
    selector: '#jobComplainReason',
    license_key: 'gpl',
    menubar: false,
    branding: false,
    plugins: 'lists code',
    toolbar: 'undo redo | bold italic underline | bullist numlist | removeformat | code',
    height: 160,
    statusbar: false,
    content_style: 'body{font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.6;}'
  });

  tinymce.init({
    selector: '#jobComplainEvidence',
    license_key: 'gpl',
    menubar: false,
    branding: false,
    plugins: 'link lists code image media',
    toolbar: 'undo redo | bold italic underline | bullist numlist | link image media | removeformat | code',
    height: 220,
    automatic_uploads: true,
    images_upload_handler: function (blobInfo) {
      return uploadSingleComplainImageForTiny(blobInfo);
    },
    file_picker_types: 'image media',
    file_picker_callback: function (callback, _value, meta) {
      handleComplainTinyFilePicker(callback, meta);
    },
    statusbar: false,
    content_style: 'body{font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.6;} img,video{max-width:100%;height:auto;border-radius:8px;}'
  });

  complainEditorsInitialized = true;
}

async function uploadComplainMediaFiles(files, endpoint, emptyMessage) {
  if (!files || !files.length) throw new Error(emptyMessage);

  const formData = new FormData();
  Array.from(files).forEach((file) => formData.append('files', file));

  const response = await fetch(endpoint, {
    method: 'POST',
    credentials: 'same-origin',
    body: formData
  });
  const raw = await response.text();
  let result = null;

  if (raw) {
    try {
      result = JSON.parse(raw);
    } catch {
      if (response.redirected && response.url && response.url.includes('/Auth/Login')) {
        throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');
      }
      throw new Error('Máy chủ trả về dữ liệu không hợp lệ khi tải media.');
    }
  }

  if (!response.ok) {
    throw new Error(result?.message || `Tải media thất bại (HTTP ${response.status}).`);
  }

  if (!result?.success || !result?.data?.urls || !Array.isArray(result.data.urls) || result.data.urls.length === 0) {
    throw new Error(result?.message || 'Tải media thất bại.');
  }

  return result.data.urls;
}

function pickLocalComplainFile(accept, multiple = true) {
  return new Promise((resolve) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = accept;
    input.multiple = !!multiple;
    input.onchange = () => {
      const files = input.files ? Array.from(input.files) : [];
      resolve(multiple ? files : (files[0] || null));
    };
    input.click();
  });
}

function uploadSingleComplainImageForTiny(blobInfo) {
  return new Promise(async (resolve, reject) => {
    try {
      const urls = await uploadComplainMediaFiles(
        [blobInfo.blob()],
        '/Report/UploadReportMedia?mediaType=image',
        'Vui lòng chọn ít nhất một ảnh.'
      );
      resolve(urls[0]);
    } catch (error) {
      reject({ message: error?.message || 'Không thể tải ảnh lên.' });
    }
  });
}

function handleComplainTinyFilePicker(callback, meta) {
  const isImage = meta.filetype === 'image';
  const accept = isImage
    ? 'image/*'
    : 'video/mp4,video/webm,video/ogg,video/quicktime,.mp4,.webm,.ogg,.mov';
  const endpoint = isImage
    ? '/Report/UploadReportMedia?mediaType=image'
    : '/Report/UploadReportMedia?mediaType=video';

  pickLocalComplainFile(accept, false).then(async (file) => {
    if (!file) return;

    try {
      const urls = await uploadComplainMediaFiles([file], endpoint, 'Vui lòng chọn tệp để tải lên.');
      const url = urls[0];
      if (isImage) callback(url, { alt: file.name || 'image' });
      else callback(url, { source2: '', poster: '' });
    } catch (error) {
      notifyToast(error?.message || 'Không thể tải tệp media lên.', 'error');
    }
  });
}

const complainMediaUploadConfig = {
  image: {
    accept: 'image/*',
    endpoint: '/Report/UploadReportMedia?mediaType=image',
    emptyMessage: 'Vui lòng chọn ít nhất một ảnh.',
    successMessage: 'Tải ảnh lên thành công.',
    failedMessage: 'Không thể tải ảnh lên.',
    buildHtml: (url) => `<p><img src="${url}" alt="Ảnh kèm khiếu nại" /></p>`
  },
  video: {
    accept: 'video/mp4,video/webm,video/ogg,video/quicktime,.mp4,.webm,.ogg,.mov',
    endpoint: '/Report/UploadReportMedia?mediaType=video',
    emptyMessage: 'Vui lòng chọn ít nhất một video.',
    successMessage: 'Tải video lên thành công.',
    failedMessage: 'Không thể tải video lên.',
    buildHtml: (url) => `<p><video controls preload="metadata" src="${url}"></video></p>`
  }
};

async function uploadComplainLocalMedia(mediaType) {
  const cfg = complainMediaUploadConfig[mediaType];
  if (!cfg) return;

  try {
    const files = await pickLocalComplainFile(cfg.accept, true);
    if (!files.length) return;

    const urls = await uploadComplainMediaFiles(files, cfg.endpoint, cfg.emptyMessage);
    urls.forEach((url) => appendToComplainEvidence(cfg.buildHtml(url)));
    notifyToast(cfg.successMessage, 'success');
  } catch (error) {
    notifyToast(error?.message || cfg.failedMessage, 'error');
  }
}

async function uploadComplainLocalImages() {
  await uploadComplainLocalMedia('image');
}

async function uploadComplainLocalVideos() {
  await uploadComplainLocalMedia('video');
}

function updatePreviewActionButtons(job) {
  const editBtn = document.getElementById('pv-editBtn');
  const applyBtn = document.getElementById('pv-applyBtn');
  const applyTextEl = document.getElementById('pv-applyBtnText');
  const complainBtn = document.getElementById('pv-complainBtn');

  if (editBtn) {
    const canEditByOwner = canEditJob(job);
    const canEdit = canEditByOwner && [0, 2].includes(Number(job?.moderationStatus)) && !!job?.id;
    editBtn.classList.toggle('hidden', !canEdit);
    if (canEdit) editBtn.href = `/Search/Edit/${job.id}`;
    else editBtn.removeAttribute('href');
  }

  if (applyBtn) {
    const canApply = canApplyJob(job);
    applyBtn.classList.toggle('hidden', !canApply);

    if (canApply) {
      const applied = isJobApplied(job.id);
      applyBtn.disabled = applied;
      applyBtn.classList.toggle('opacity-60', applied);
      applyBtn.classList.toggle('cursor-not-allowed', applied);
      applyBtn.dataset.jobId = job.id;
      applyBtn.onclick = (event) => applyJob(job.id, event);
      if (applyTextEl) applyTextEl.textContent = applied ? 'Đã ứng tuyển' : 'Ứng tuyển ngay';
    } else {
      applyBtn.disabled = false;
      applyBtn.classList.remove('opacity-60', 'cursor-not-allowed');
      applyBtn.onclick = null;
      applyBtn.removeAttribute('data-job-id');
      if (applyTextEl) applyTextEl.textContent = 'Ứng tuyển ngay';
    }
  }

  if (complainBtn) {
    const canComplain = canComplainJob(job);
    complainBtn.classList.toggle('hidden', !canComplain);
    if (canComplain) {
      complainBtn.onclick = (event) => openJobComplainModal(job, event);
    } else {
      complainBtn.onclick = null;
    }
  }
}

async function applyJob(jobId, event) {
  event?.preventDefault?.();
  event?.stopPropagation?.();

  const realtimeToastType = 'job-application-submitted';

  if (!isLoggedIn()) {
    notifyToast('Vui lòng đăng nhập để ứng tuyển bài đăng.', 'error');
    window.location.href = '/Auth/Login';
    return;
  }

  if (!isNannyRole()) {
    notifyToast('Chỉ bảo mẫu mới có quyền ứng tuyển bài đăng.', 'error');
    return;
  }

  if (!jobId) {
    notifyToast('Không tìm thấy bài đăng để ứng tuyển.', 'error');
    return;
  }

  if (isJobApplied(jobId)) {
    notifyToast('Bạn đã gửi đơn ứng tuyển cho bài đăng này.', 'error');
    return;
  }

  const applyBtn = document.getElementById('pv-applyBtn');
  const applyTextEl = document.getElementById('pv-applyBtnText');
  const previousText = applyTextEl?.textContent || 'Ứng tuyển ngay';

  try {
    if (applyBtn) {
      applyBtn.disabled = true;
      applyBtn.classList.add('opacity-60', 'cursor-not-allowed');
    }
    if (applyTextEl) applyTextEl.textContent = 'Đang gửi...';

    window.__nmNotificationToastSuppressType = realtimeToastType;
    window.__nmNotificationToastSuppressUntil = Date.now() + 5000;

    const response = await fetch(`/Search/ApplyJob?jobPostingId=${encodeURIComponent(jobId)}`, {
      method: 'POST',
      credentials: 'same-origin'
    });
    const json = await response.json();
    const payload = json?.raw && typeof json.raw === 'object' ? json.raw : json;
    const isSuccess = !!(json?.success || payload?.success);

    if (!isSuccess) {
      if (String(window.__nmNotificationToastSuppressType || '').trim().toLowerCase() === realtimeToastType) {
        window.__nmNotificationToastSuppressType = '';
        window.__nmNotificationToastSuppressUntil = 0;
      }
      notifyToast(json?.message || payload?.message || 'Không thể ứng tuyển bài đăng.', 'error');
      if (applyBtn) {
        applyBtn.disabled = false;
        applyBtn.classList.remove('opacity-60', 'cursor-not-allowed');
      }
      if (applyTextEl) applyTextEl.textContent = previousText;
      return;
    }

    setJobAppliedState(jobId, true);
    const activeJob = currentJobs.find((job) => normalizeGuid(job?.id) === normalizeGuid(jobId));
    if (activeJob) updatePreviewActionButtons(activeJob);

    notifyToast(payload?.message || json?.message || 'Gửi đơn thành công. Vui lòng chờ phụ huynh phản hồi.', 'success');
    window.dispatchEvent(new CustomEvent('nm:notifications-refresh'));
  } catch {
    if (String(window.__nmNotificationToastSuppressType || '').trim().toLowerCase() === realtimeToastType) {
      window.__nmNotificationToastSuppressType = '';
      window.__nmNotificationToastSuppressUntil = 0;
    }
    notifyToast('Không thể ứng tuyển bài đăng.', 'error');
    if (applyBtn) {
      applyBtn.disabled = false;
      applyBtn.classList.remove('opacity-60', 'cursor-not-allowed');
    }
    if (applyTextEl) applyTextEl.textContent = previousText;
  }
}

function openJobComplainModal(job, event) {
  event?.preventDefault?.();
  event?.stopPropagation?.();

  if (!isLoggedIn()) {
    notifyToast('Vui lòng đăng nhập để gửi khiếu nại.', 'error');
    window.location.href = '/Auth/Login';
    return;
  }

  if (!canComplainJob(job)) {
    notifyToast('Bạn không thể khiếu nại bài đăng của chính mình.', 'error');
    return;
  }

  pendingComplainJob = job;
  isSubmittingJobComplain = false;
  ensureComplainEditorsInitialized();

  const targetEl = document.getElementById('jobComplainTarget');
  const jobPostingIdInput = document.getElementById('jobComplainJobPostingId');
  if (targetEl) targetEl.textContent = job?.title ? `Bài đăng: ${job.title}` : 'Bài đăng';
  if (jobPostingIdInput) jobPostingIdInput.value = job?.id || '';

  setComplainEditorContent('jobComplainReason', '');
  setComplainEditorContent('jobComplainEvidence', '');

  const submitBtn = document.getElementById('jobComplainSubmitBtn');
  if (submitBtn) {
    submitBtn.disabled = false;
    submitBtn.textContent = 'Gửi khiếu nại';
  }

  document.getElementById('jobComplainModal')?.classList.add('show');
}

function closeJobComplainModal() {
  document.getElementById('jobComplainModal')?.classList.remove('show');
  const jobPostingIdInput = document.getElementById('jobComplainJobPostingId');
  if (jobPostingIdInput) jobPostingIdInput.value = '';
  pendingComplainJob = null;
  isSubmittingJobComplain = false;
}

async function submitJobComplain() {
  if (!pendingComplainJob?.id) {
    notifyToast('Không tìm thấy bài đăng cần khiếu nại.', 'error');
    return;
  }

  const reasonEl = document.getElementById('jobComplainReason');
  const submitBtn = document.getElementById('jobComplainSubmitBtn');

  const reasonHtml = getComplainEditorContent('jobComplainReason');
  const evidenceHtml = getComplainEditorContent('jobComplainEvidence');
  const reason = extractPlainText(reasonHtml);
  const evidence = String(evidenceHtml || '').trim();

  if (reason.length < 5) {
    notifyToast('Lý do khiếu nại phải có ít nhất 5 ký tự.', 'error');
    if (typeof tinymce !== 'undefined' && tinymce.get('jobComplainReason')) tinymce.get('jobComplainReason').focus();
    else reasonEl?.focus();
    return;
  }

  if (isSubmittingJobComplain) return;
  isSubmittingJobComplain = true;

  if (submitBtn) {
    submitBtn.disabled = true;
    submitBtn.textContent = 'Đang gửi...';
  }

  try {
    const response = await fetch(`/Search/ComplainJobPosting?jobPostingId=${encodeURIComponent(pendingComplainJob.id)}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'same-origin',
      body: JSON.stringify({
        reason,
        evidence: evidence || null
      })
    });

    const json = await response.json();
    const payload = json?.raw && typeof json.raw === 'object' ? json.raw : json;
    const isSuccess = !!(json?.success || payload?.success);

    if (!isSuccess) {
      notifyToast(json?.message || payload?.message || 'Không thể gửi khiếu nại bài đăng.', 'error');
      isSubmittingJobComplain = false;
      if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Gửi khiếu nại';
      }
      return;
    }

    closeJobComplainModal();
    notifyToast('Gửi khiếu nại thành công.', 'success');
    window.dispatchEvent(new CustomEvent('nm:notifications-refresh'));
  } catch {
    notifyToast('Không thể gửi khiếu nại bài đăng.', 'error');
    isSubmittingJobComplain = false;
    if (submitBtn) {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Gửi khiếu nại';
    }
  }
}

async function loadAppliedJobs() {
  appliedJobIds.clear();

  if (!isLoggedIn() || !isNannyRole()) return;

  try {
    const res = await fetch('/Search/MyApplicationsData?page=1&pageSize=200', { credentials: 'same-origin' });
    const json = await res.json();
    const items = Array.isArray(json?.data) ? json.data : [];

    items.forEach((item) => {
      const status = Number(item?.status);
      if (item?.jobPostingId && status !== 3) {
        appliedJobIds.add(normalizeGuid(item.jobPostingId));
      }
    });
  } catch {
    // Keep UI responsive even when this request fails.
  }
}

function openHistory() {
  window.location.href = '/Search/History';
}

function closeHistory() {
  document.getElementById('historyModal')?.classList.remove('show');
}

function openPremium() {
  window.location.href = '/Subscription';
}

function debounceSearch() {
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(doSearch, 350);
}

function initMap() {
  const mapEl = document.getElementById('map');
  if (map || !mapEl || typeof L === 'undefined') return;
  map = L.map('map', { zoomControl: true }).setView([10.776, 106.701], 11);
  L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    attribution: '&copy; CartoDB',
    maxZoom: 19
  }).addTo(map);

  map.on('moveend', () => {
    if (suppressNextMapSearch) {
      suppressNextMapSearch = false;
      return;
    }

    debounceSearch();
  });
}

function getCurrentMapBoundsParams() {
  if (!map) return null;

  const bounds = map.getBounds();
  if (!bounds?.isValid?.()) return null;

  return {
    minLat: bounds.getSouth(),
    maxLat: bounds.getNorth(),
    minLng: bounds.getWest(),
    maxLng: bounds.getEast()
  };
}

function getAreaPresentation(job) {
  if (job?.latitude && job?.longitude) {
    return {
      lat: Number(job.latitude),
      lng: Number(job.longitude),
      radius: 2000,
      zoom: 13
    };
  }

  const fallback = GEO_FALLBACK[job?.city] || GEO_FALLBACK['Ho Chi Minh'];
  return { ...fallback };
}

function clearMapMarkers() {
  markers.forEach((item) => {
    item.marker?.remove();
    item.circle?.remove();
  });
  markers = [];
}

function setMarkerHover(idx, active, openPopup = false) {
  const markerData = markers[idx];
  if (!markerData) return;
  markerData.element?.classList.toggle('active', active);
  markerData.circle?.setStyle({
    color: active ? '#f97316' : '#fdba74',
    fillColor: active ? '#fb923c' : '#fdba74',
    fillOpacity: active ? 0.18 : 0.1,
    weight: active ? 2 : 1
  });
  if (openPopup && active) markerData.marker?.openPopup();
}

function highlightOnMap(idx) {
  const markerData = markers[idx];
  if (!markerData || !map) return;
  suppressNextMapSearch = true;
  map.flyTo([markerData.point.lat, markerData.point.lng], markerData.point.zoom || 13, { duration: 0.45 });
  setMarkerHover(idx, true, false);
}

function renderReadOnlySchedule(slots) {
  if (!Array.isArray(slots) || !slots.length) {
    return '<span class="text-sm text-gray-400">Chưa chọn lịch cụ thể.</span>';
  }
  const selectedSet = new Set(slots.map((slot) => `${slot.dayOfWeek}-${slot.timeSlot}`));
  let html = '<div class="schedule-grid schedule-grid--readonly"><div></div>';
  DAY_LABELS.forEach((label) => { html += `<div class="schedule-col-label">${label}</div>`; });
  ROW_LABELS.forEach((rowLabel, timeSlot) => {
    html += `<div class="schedule-row-label">${rowLabel}</div>`;
    for (let dayOfWeek = 0; dayOfWeek < 7; dayOfWeek += 1) {
      const active = selectedSet.has(`${dayOfWeek}-${timeSlot}`);
      html += `<div class="schedule-cell ${active ? 'active' : ''}" aria-hidden="true"><span class="schedule-check">${active ? '&#10003;' : ''}</span></div>`;
    }
  });
  html += '</div>';
  return html;
}

function renderJobs(jobs) {
  const list = document.getElementById('jobList');
  const resultCount = document.getElementById('resultCount');
  if (!list || !resultCount) return;

  resultCount.textContent = `${jobs.length} tin đăng`;
  clearMapMarkers();

  if (!jobs.length) {
    list.innerHTML = `
      <div class="rounded-[24px] border border-dashed border-slate-200 bg-white px-6 py-16 text-center text-slate-500">
        <span class="material-icons-round text-[34px] text-orange-300">search_off</span>
        <h3 class="mt-3 text-lg font-bold text-slate-900">Không tìm thấy bài đăng</h3>
        <p class="mt-2 text-sm text-slate-500">Thử tìm ở thành phố khác hoặc đổi từ khoá.</p>
      </div>`;
    return;
  }

  list.innerHTML = jobs.map((job, idx) => `
    <article class="job-card rounded-[26px] border border-orange-100 bg-white p-5 mb-4 shadow-[0_10px_30px_rgba(15,23,42,.04)]"
             data-idx="${idx}" data-id="${escapeHtml(job.id)}">
      <div class="flex items-start justify-between gap-3">
        <div class="min-w-0">
          <h3 class="text-[18px] leading-6 font-extrabold text-slate-900">${escapeHtml(job.title || 'Tin đăng')}</h3>
          <p class="mt-2 text-sm font-semibold text-slate-500">${escapeHtml(job.parentName || 'Phụ huynh')}</p>
        </div>
        <div class="job-card__head-right">
          <span class="shrink-0 rounded-full bg-orange-50 px-3 py-1 text-xs font-extrabold text-orange-700">${escapeHtml(formatSalaryRange(job.salaryMin, job.salaryMax, job.salaryNegotiable))}</span>
          ${isNannyRole() ? `
            <button type="button"
                    class="job-save-btn ${job.isFavorite ? 'active' : ''}"
                    data-job-id="${escapeHtml(normalizeGuid(job.id))}"
                    aria-pressed="${job.isFavorite ? 'true' : 'false'}"
                    aria-label="${job.isFavorite ? 'Bỏ lưu bài đăng' : 'Lưu bài đăng'}"
                    title="${job.isFavorite ? 'Bỏ lưu bài đăng' : 'Lưu bài đăng'}"
                    onclick="toggleJobFavorite('${escapeJs(job.id)}', event)">
              <span class="material-icons-round">${job.isFavorite ? 'bookmark' : 'bookmark_border'}</span>
            </button>` : ''}
        </div>
      </div>
      <p class="mt-3 text-sm font-semibold text-orange-600">${escapeHtml([job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chưa cập nhật địa điểm')}</p>
      <p class="mt-3 text-sm leading-6 text-slate-500 line-clamp-2">${escapeHtml(job.description || 'Không có mô tả.')}</p>
      <div class="mt-4 flex flex-wrap gap-2">
        ${renderJobBenefitBadges(job)}
        <span class="px-3 py-1 rounded-full bg-orange-50 text-orange-700 text-xs font-bold">${escapeHtml(JOB_TYPES[job.jobType] || 'Khác')}</span>
        <span class="px-3 py-1 rounded-full bg-slate-100 text-slate-700 text-xs font-bold">${escapeHtml(MODERATION_LABELS[job.moderationStatus] || 'Đang cập nhật')}</span>
        <span class="px-3 py-1 rounded-full bg-blue-50 text-blue-700 text-xs font-bold">${job.numberOfChildren ? `${job.numberOfChildren} bé` : 'Chưa rõ'}</span>
        ${isNannyRole() && isJobApplied(job.id) ? '<span class="px-3 py-1 rounded-full bg-emerald-50 text-emerald-700 text-xs font-bold">Đã ứng tuyển</span>' : ''}
      </div>
    </article>`).join('');

  jobs.forEach((job) => {
    const point = getAreaPresentation(job);
    const icon = L.divIcon({
      className: '',
      html: `
        <div class="job-map-marker">
          <span class="job-map-marker__halo"></span>
          <span class="job-map-marker__pin"><span class="job-map-marker__core"></span></span>
        </div>`,
      iconSize: [26, 34],
      iconAnchor: [13, 28]
    });
    const marker = L.marker([point.lat, point.lng], { icon }).addTo(map);
    const circle = L.circle([point.lat, point.lng], {
      radius: point.radius,
      color: '#fdba74',
      fillColor: '#fdba74',
      fillOpacity: 0.1,
      weight: 1
    }).addTo(map);
    marker.bindPopup(`<div class="text-sm font-semibold text-slate-700">${escapeHtml(job.title || 'Tin đăng')}</div><div class="text-xs text-slate-500 mt-1">Khu vực gần đúng</div>`);
    markers.push({ marker, circle, point, element: marker.getElement() });
  });

  list.querySelectorAll('.job-card').forEach((card) => {
    const idx = Number(card.dataset.idx);
    card.addEventListener('mouseenter', () => setMarkerHover(idx, true, false));
    card.addEventListener('mouseleave', () => setMarkerHover(idx, false, false));
    card.addEventListener('click', () => {
      list.querySelectorAll('.job-card').forEach((item) => item.classList.remove('active'));
      card.classList.add('active');
      highlightOnMap(idx);
      openPreview(currentJobs[idx]);
    });
  });
}

async function doSearch() {
  const city = document.getElementById('searchCity')?.value.trim() || '';
  const params = new URLSearchParams({ page: '1', pageSize: '20' });
  if (city) params.append('city', city);

  const bounds = getCurrentMapBoundsParams();
  if (bounds) {
    params.append('minLat', bounds.minLat.toString());
    params.append('maxLat', bounds.maxLat.toString());
    params.append('minLng', bounds.minLng.toString());
    params.append('maxLng', bounds.maxLng.toString());
  }

  try {
    const res = await fetch(`/Search/Jobs?${params.toString()}`, { credentials: 'same-origin' });
    const json = await res.json();
    currentJobs = Array.isArray(json.data) ? json.data : [];
    renderJobs(currentJobs);
  } catch {
    currentJobs = [];
    renderJobs([]);
  }
}

function setProfileFields(prefix, child) {
  const characteristic = document.getElementById(`${prefix}-characteristic`);
  const birthType = document.getElementById(`${prefix}-birthType`);
  const specialNeeds = document.getElementById(`${prefix}-specialNeeds`);
  if (characteristic) characteristic.value = child?.characteristic || '';
  if (birthType) birthType.value = child?.birthTypeLabel || '';
  if (specialNeeds) specialNeeds.value = child?.specialNeeds || '';
}

function normalizeChildrenCount(value) {
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) return 1;
  return Math.min(10, Math.max(1, Math.trunc(parsed)));
}

function getTotalChildProfiles(collection) {
  return Array.isArray(collection) ? collection.length : 0;
}

/**
 * @param {string} selectId
 * @param {number} totalProfiles - số hồ sơ bé đang có (0 = chưa tải / chưa có)
 * @param {number|string} [preferredValue] - giá trị mong muốn (sẽ clamp trong 1..max)
 */
function ensureChildrenCountOptions(selectId, totalProfiles, preferredValue) {
  const select = document.getElementById(selectId);
  if (!select) return;

  const total = Number(totalProfiles) || 0;
  const max = total > 0 ? Math.min(10, total) : 1;
  const rawPreferred = preferredValue ?? select.value ?? 1;
  const preferred = normalizeChildrenCount(rawPreferred);
  const clamped = Math.min(Math.max(preferred, 1), max);

  select.innerHTML = '';
  for (let i = 1; i <= max; i += 1) {
    const opt = document.createElement('option');
    opt.value = String(i);
    opt.textContent = `${i} bé`;
    select.appendChild(opt);
  }
  select.value = String(clamped);

  if (total <= 0) {
    select.disabled = true;
    select.setAttribute('aria-readonly', 'true');
  } else {
    select.disabled = false;
    select.removeAttribute('aria-readonly');
  }
}

function getSelectedChild(prefix) {
  const childId = document.getElementById(`${prefix}-childProfileId`)?.value;
  const collection = prefix === 'cf' ? createChildren : editChildren;
  return collection.find((child) => String(child.id).toLowerCase() === String(childId).toLowerCase()) || null;
}

function getCreateSelectedChildren(requiredCount) {
  const count = normalizeChildrenCount(requiredCount);
  if (!Array.isArray(createChildren) || createChildren.length === 0) return [];

  const selected = [];
  const selectedIds = new Set();
  const tryAddChild = (childId) => {
    const normalizedId = String(childId || '').toLowerCase();
    if (!normalizedId || selectedIds.has(normalizedId)) return;
    const child = createChildren.find((item) => String(item.id).toLowerCase() === normalizedId);
    if (!child) return;
    selectedIds.add(normalizedId);
    selected.push(child);
  };

  tryAddChild(document.getElementById('cf-childProfileId')?.value || '');

  for (let idx = 0; idx < createChildren.length && selected.length < count; idx += 1) {
    const fallbackId = String(createChildren[idx]?.id || '').toLowerCase();
    if (!fallbackId || selectedIds.has(fallbackId)) continue;
    selectedIds.add(fallbackId);
    selected.push(createChildren[idx]);
  }

  return selected;
}

function renderCreateExtraChildProfiles() {
  const container = document.getElementById('cf-extraChildProfiles');
  if (!container) return;
  container.innerHTML = '';
}

function renderChildren(prefix, selectedChildId) {
  const select = document.getElementById(`${prefix}-childProfileId`);
  if (!select) return;
  const collection = prefix === 'cf' ? createChildren : editChildren;
  select.innerHTML = collection.length
    ? collection.map((child) => `<option value="${escapeHtml(child.id)}">${escapeHtml(child.label)}</option>`).join('')
    : '<option value="">Chưa có hồ sơ bé</option>';
  if (selectedChildId) select.value = selectedChildId;
  syncSelectPickerTexts();
  setProfileFields(prefix, getSelectedChild(prefix));
  if (prefix === 'cf') handleCreateChildrenCountChange();
}

function applyPrefill(prefix, data, selectedChildId) {
  const collection = prefix === 'cf' ? createChildren : editChildren;
  const rawChildren = data?.children ?? data?.Children;
  const childrenList = Array.isArray(rawChildren) ? rawChildren : [];
  collection.splice(0, collection.length, ...childrenList);
  const childrenInput = document.getElementById(`${prefix}-children`);
  if (childrenInput) {
    const totalChildren = getTotalChildProfiles(collection);
    if (prefix === 'cf') {
      const rawNum = data?.numberOfChildren ?? data?.NumberOfChildren;
      const preferredFromData = rawNum != null ? normalizeChildrenCount(rawNum) : null;
      const preferredCount = totalChildren > 0
        ? (preferredFromData != null ? Math.min(preferredFromData, totalChildren) : totalChildren)
        : normalizeChildrenCount(rawNum ?? childrenInput.value ?? 1);
      ensureChildrenCountOptions('cf-children', totalChildren, preferredCount);
    } else {
      const preferredCount = totalChildren > 0
        ? totalChildren
        : normalizeChildrenCount(data?.numberOfChildren ?? data?.NumberOfChildren ?? childrenInput.value ?? 1);
      childrenInput.value = String(normalizeChildrenCount(preferredCount));
    }
  }
  renderChildren(prefix, selectedChildId || data?.selectedChildProfileId || data?.SelectedChildProfileId);
}

function handleCreateChildChange() {
  setProfileFields('cf', getSelectedChild('cf'));
  handleCreateChildrenCountChange();
}

function handleCreateChildrenCountChange() {
  const countInput = document.getElementById('cf-children');
  if (!countInput) return;

  const totalChildren = getTotalChildProfiles(createChildren);
  ensureChildrenCountOptions('cf-children', totalChildren, countInput.value);
  renderCreateExtraChildProfiles();
  setProfileFields('cf', getSelectedChild('cf'));
}

function handleEditChildChange() {
  setProfileFields('ef', getSelectedChild('ef'));
}

function renderScheduleGrid(containerId, selected, onToggleName) {
  const container = document.getElementById(containerId);
  if (!container) return;
  const selectedSet = new Set(selected.map((slot) => `${slot.dayOfWeek}-${slot.timeSlot}`));
  let html = '<div></div>';
  DAY_LABELS.forEach((label) => { html += `<div class="schedule-col-label">${label}</div>`; });
  ROW_LABELS.forEach((rowLabel, timeSlot) => {
    html += `<div class="schedule-row-label">${rowLabel}</div>`;
    for (let dayOfWeek = 0; dayOfWeek < 7; dayOfWeek += 1) {
      const active = selectedSet.has(`${dayOfWeek}-${timeSlot}`);
      html += `<button type="button" class="schedule-cell ${active ? 'active' : ''}" onclick="${onToggleName}(${dayOfWeek},${timeSlot})"><span class="schedule-check">${active ? '&#10003;' : ''}</span></button>`;
    }
  });
  container.innerHTML = html;
}

function toggleScheduleValue(collection, dayOfWeek, timeSlot) {
  const idx = collection.findIndex((slot) => slot.dayOfWeek === dayOfWeek && slot.timeSlot === timeSlot);
  if (idx >= 0) collection.splice(idx, 1);
  else collection.push({ dayOfWeek, timeSlot });
}

function toggleCreateSchedule(dayOfWeek, timeSlot) {
  toggleScheduleValue(createSchedule, dayOfWeek, timeSlot);
  renderScheduleGrid('cf-schedule', createSchedule, 'toggleCreateSchedule');
}

function toggleEditSchedule(dayOfWeek, timeSlot) {
  toggleScheduleValue(editSchedule, dayOfWeek, timeSlot);
  renderScheduleGrid('ef-schedule', editSchedule, 'toggleEditSchedule');
}

function renderSkillCollection(containerId, collection, removeHandlerName) {
  const container = document.getElementById(containerId);
  if (!container) return;
  container.innerHTML = collection.map((skill) => `
    <span class="skill-tag">${escapeHtml(skill)}
      <button type="button" onclick="${removeHandlerName}('${escapeJs(skill)}')">x</button>
    </span>`).join('');
}

function addSelectedSkillToCollection(selectId, collection, containerId, removeHandlerName) {
  const select = document.getElementById(selectId);
  if (!select) return;
  const value = select.value.trim();
  if (!value || collection.includes(value)) return;
  collection.push(value);
  select.value = '';
  syncSelectPickerTexts();
  renderSkillCollection(containerId, collection, removeHandlerName);
}

function addCreateSkill() {
  addSelectedSkillToCollection('cf-skillSelect', createSkills, 'cf-skills', 'removeCreateSkill');
}

function addEditSkill() {
  addSelectedSkillToCollection('ef-skillSelect', editSkills, 'ef-skills', 'removeEditSkill');
}

function removeCreateSkill(value) {
  createSkills = createSkills.filter((skill) => skill !== value);
  renderSkillCollection('cf-skills', createSkills, 'removeCreateSkill');
}

function removeEditSkill(value) {
  editSkills = editSkills.filter((skill) => skill !== value);
  renderSkillCollection('ef-skills', editSkills, 'removeEditSkill');
}

function setStatusToggle(toggleId, hiddenInputId, status) {
  const hiddenInput = document.getElementById(hiddenInputId);
  if (hiddenInput) hiddenInput.value = String(status);
  document.querySelectorAll(`#${toggleId} .status-option`).forEach((button, index) => {
    const optionStatus = index === 0 ? 1 : 2;
    button.classList.toggle('active', optionStatus === status);
  });
}

function setCreateStatus(status) {
  setStatusToggle('cf-statusToggle', 'cf-status', status);
}

function setEditStatus(status) {
  setStatusToggle('ef-statusToggle', 'ef-status', status);
}

async function openCreate() {
  if (!isLoggedIn()) {
    window.location.href = '/Auth/Login';
    return;
  }

  await loadLocationData();
  document.getElementById('createForm')?.reset();
  const createSkillSelect = document.getElementById('cf-skillSelect');
  if (createSkillSelect) createSkillSelect.value = '';
  createSkills = [];
  createSchedule = [];
  createChildren = [];
  setCreateStatus(1);
  ensureChildrenCountOptions('cf-children', 0, 1);
  renderSkillCollection('cf-skills', createSkills, 'removeCreateSkill');
  renderScheduleGrid('cf-schedule', createSchedule, 'toggleCreateSchedule');
  applyPrefill('cf', {}, null);

  try {
    const res = await fetch('/Search/Prefill', { credentials: 'same-origin' });
    const json = await res.json();
    if (json.success && json.data) {
      const d = json.data;
      applyPrefill('cf', d, d.selectedChildProfileId ?? d.SelectedChildProfileId);
    }
  } catch { }

  handleCreateChildrenCountChange();

  syncSelectPickerTexts();
  document.getElementById('createModal')?.classList.add('show');
}

function closeCreate() {
  document.getElementById('createModal')?.classList.remove('show');
  isSubmittingCreate = false;
}

function getCreatePayload() {
  const totalChildren = getTotalChildProfiles(createChildren);
  const countFromUi = normalizeChildrenCount(document.getElementById('cf-children')?.value || 1);
  const numberOfChildren = totalChildren > 0 ? Math.min(countFromUi, totalChildren) : countFromUi;
  const selectedChildren = getCreateSelectedChildren(numberOfChildren);
  const childProfileIds = selectedChildren.map((c) => c.id).filter(Boolean);
  return {
    title: document.getElementById('cf-title')?.value.trim() || '',
    description: document.getElementById('cf-desc')?.value.trim() || '',
    jobType: Number(document.getElementById('cf-type')?.value || 1),
    numberOfChildren,
    childProfileId: selectedChildren[0]?.id || document.getElementById('cf-childProfileId')?.value || null,
    childProfileIds,
    salaryMin: parseMoneyInput(document.getElementById('cf-salMin')?.value),
    salaryMax: parseMoneyInput(document.getElementById('cf-salMax')?.value),
    salaryNegotiable: !!document.getElementById('cf-negotiable')?.checked,
    location: document.getElementById('cf-location')?.value.trim() || '',
    city: document.getElementById('cf-city')?.value.trim() || '',
    district: document.getElementById('cf-district')?.value.trim() || '',
    minNannyAge: document.getElementById('cf-minAge')?.value ? Number(document.getElementById('cf-minAge').value) : null,
    maxNannyAge: document.getElementById('cf-maxAge')?.value ? Number(document.getElementById('cf-maxAge').value) : null,
    skills: createSkills,
    scheduleSlots: createSchedule,
    status: Number(document.getElementById('cf-status')?.value || 1)
  };
}

function validatePayload(payload) {
  const minimumSalary = 8000000;
  const maximumSalary = 50000000;
  const totalChildren = getTotalChildProfiles(createChildren);
  if (!payload.title || payload.title.length < 5) return 'Tiêu đề bài đăng phải từ 5 ký tự trở lên.';
  if (!payload.description || payload.description.length < 10) return 'Mô tả chi tiết phải từ 10 ký tự trở lên.';
  if (totalChildren < 1) {
    return 'Vui lòng tạo ít nhất 1 hồ sơ bé trước khi đăng bài.';
  }
  if (totalChildren > 10) {
    return 'Tối đa 10 hồ sơ bé có thể được sử dụng cho một bài đăng.';
  }
  if (!Number.isFinite(payload.numberOfChildren) || payload.numberOfChildren < 1 || payload.numberOfChildren > 10) {
    return 'Số trẻ cần chăm phải từ 1 đến 10.';
  }
  if (payload.numberOfChildren > totalChildren) {
    return `Số trẻ cần chăm không được vượt quá số hồ sơ bé hiện có (${totalChildren} bé).`;
  }
  const selectedForCount = getCreateSelectedChildren(payload.numberOfChildren);
  if (selectedForCount.length !== payload.numberOfChildren) {
    return 'Vui lòng chọn đủ hồ sơ bé cho từng trẻ.';
  }
  if (!payload.childProfileId) return 'Vui lòng chọn hồ sơ bé.';
  if (!payload.salaryNegotiable && payload.salaryMin == null) return 'Vui lòng nhập lương tối thiểu trong khoảng 8.000.000 - 50.000.000 VND hoặc bật lương thỏa thuận.';
  if (payload.salaryMin != null && (!Number.isFinite(payload.salaryMin) || payload.salaryMin < minimumSalary || payload.salaryMin > maximumSalary)) {
    return 'Lương tối thiểu phải trong khoảng 8.000.000 - 50.000.000 VND.';
  }
  if (payload.salaryMax != null && (!Number.isFinite(payload.salaryMax) || payload.salaryMax < minimumSalary || payload.salaryMax > maximumSalary)) {
    return 'Lương tối đa phải trong khoảng 8.000.000 - 50.000.000 VND.';
  }
  if (payload.salaryMin != null && payload.salaryMax != null && payload.salaryMin > payload.salaryMax) {
    return 'Lương tối thiểu không được lớn hơn lương tối đa.';
  }
  if (!payload.location || payload.location.length < 3) return 'Vui lòng nhập địa chỉ chi tiết.';
  if (!payload.city) return 'Vui lòng nhập thành phố.';
  if (!payload.district) return 'Vui lòng nhập phường/xã.';
  if (!Array.isArray(payload.skills) || !payload.skills.length) return 'Vui lòng chọn ít nhất 1 kỹ năng.';
  if (!Array.isArray(payload.scheduleSlots) || !payload.scheduleSlots.length) return 'Vui lòng chọn ít nhất 1 khung lịch.';
  return '';
}

async function submitCreate() {
  if (!isLoggedIn()) {
    window.location.href = '/Auth/Login';
    return;
  }

  if (isSubmittingCreate) return;
  const payload = getCreatePayload();
  const error = validatePayload(payload);
  if (error) {
    notifyToast(error);
    return;
  }

  isSubmittingCreate = true;
  const submitBtn = document.querySelector('#createModal .modal-btn-primary');
  if (submitBtn) {
    submitBtn.disabled = true;
    submitBtn.textContent = 'Đang gửi...';
  }

  try {
    const res = await fetch('/Search/CreateJob', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      credentials: 'same-origin',
      body: JSON.stringify(payload)
    });
    const json = await res.json();
    if (!json.success) {
      notifyToast(json.message || 'Đăng bài thất bại');
      isSubmittingCreate = false;
      if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Đăng bài';
      }
      return;
    }

    closeCreate();
    window.__nmNotificationToastSuppressType = 'job-posting-pending';
    window.__nmNotificationToastSuppressUntil = Date.now() + 5000;
    const successToast = 'Bài đăng đã được tạo và đang chờ kiểm duyệt viên duyệt.';
    const fallbackScheduledAt = Date.now();
    window.setTimeout(() => {
      const lastType = String(window.__nmLastRealtimeNotificationType || '').trim().toLowerCase();
      const lastAt = Number(window.__nmLastRealtimeNotificationAt || 0);
      const receivedRealtime = lastType === 'job-posting-pending' && lastAt >= fallbackScheduledAt - 3000;
      if (!receivedRealtime) {
        notifyToast(successToast);
      }
    }, 900);
    window.dispatchEvent(new CustomEvent('nm:notifications-refresh'));
    doSearch();
  } catch {
    notifyToast('Lỗi kết nối máy chủ.');
    isSubmittingCreate = false;
    if (submitBtn) {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Đăng bài';
    }
  }
}

async function openEdit(job) {
  if (!job?.id) return;
  if (!canEditJob(job)) {
    notifyToast('Bạn không có quyền chỉnh sửa bài đăng này.');
    return;
  }
  window.location.href = `/Search/Edit/${job.id}`;
}

function closeEdit() {
  document.getElementById('editModal')?.classList.remove('show');
  isSubmittingEdit = false;
}

async function submitEdit() {
  if (editingJobId) {
    if (!ownedJobIds.has(normalizeGuid(editingJobId))) {
      notifyToast('Bạn không có quyền chỉnh sửa bài đăng này.');
      return;
    }
    window.location.href = `/Search/Edit/${editingJobId}`;
  }
}

async function deleteJob() {
  if (!editingJobId) return;
  if (!ownedJobIds.has(normalizeGuid(editingJobId))) {
    notifyToast('Bạn không có quyền xóa bài đăng này.');
    return;
  }
  if (!confirm('Bạn có chắc muốn xóa bài đăng này?')) return;
  try {
    const res = await fetch(`/Search/DeleteJob/${editingJobId}`, { method: 'DELETE', credentials: 'same-origin' });
    const json = await res.json();
    if (!json.success) {
      notifyToast(json.message || 'Xóa thất bại');
      return;
    }
    closeEdit();
    notifyToast('Đã xóa bài đăng.');
    doSearch();
  } catch {
    notifyToast('Lỗi kết nối máy chủ.');
  }
}

function renderPreviewChildProfiles(job, childProfilesInput) {
  const container = document.getElementById('pv-childProfiles');
  if (!container) return;

  const childProfiles = Array.isArray(childProfilesInput) ? childProfilesInput.filter(Boolean) : [];
  const hasAggregateFallback = Boolean(job?.characteristic || job?.birthTypeLabel || job?.specialNeeds);
  const fallbackChildren = childProfiles.length
    ? childProfiles
    : (hasAggregateFallback
      ? [{
          label: 'Bé 1',
          characteristic: job?.characteristic || '',
          birthTypeLabel: job?.birthTypeLabel || '',
          specialNeeds: job?.specialNeeds || ''
        }]
      : []);
  const requiredCount = normalizeChildrenCount(job?.numberOfChildren || fallbackChildren.length || 1);

  if (!fallbackChildren.length && !job?.numberOfChildren) {
    container.innerHTML = '';
    container.classList.add('hidden');
    return;
  }

  let html = '';
  for (let idx = 0; idx < requiredCount; idx += 1) {
    const child = fallbackChildren[idx] || null;
    const displayIndex = idx + 1;

    if (!child) {
      html += `
        <div class="child-profile-panel child-profile-panel--missing child-profile-panel--preview">
          <div class="child-profile-panel__head">
          <p class="child-profile-panel__title">Bé thứ ${displayIndex}</p>
          </div>
          <p class="child-profile-panel__empty">Chưa thấy hồ sơ trẻ cho bé này.</p>
        </div>`;
      continue;
    }

    html += `
      <div class="child-profile-panel child-profile-panel--preview">
        <div class="child-profile-panel__head">
          <p class="child-profile-panel__title">Bé thứ ${displayIndex}</p>
          <span class="child-profile-panel__badge">${escapeHtml(child.label || `Bé ${displayIndex}`)}</span>
        </div>
        <div class="child-profile-grid">
          <div class="child-profile-field">
            <span class="child-profile-field__label">Đặc điểm</span>
            <p class="child-profile-field__value">${escapeHtml(child.characteristic || 'Chưa cập nhật')}</p>
          </div>
          <div class="child-profile-field">
            <span class="child-profile-field__label">Nhóm tuổi</span>
            <p class="child-profile-field__value">${escapeHtml(child.birthTypeLabel || 'Chưa cập nhật')}</p>
          </div>
          <div class="child-profile-field child-profile-field--full">
            <span class="child-profile-field__label">Nhu cầu đặc biệt</span>
            <p class="child-profile-field__value">${escapeHtml(child.specialNeeds || 'Không có')}</p>
          </div>
        </div>
      </div>`;
  }

  container.innerHTML = html;
  container.classList.remove('hidden');
}

function renderPreviewChildProfiles(job, childProfilesInput) {
  const container = document.getElementById('pv-childProfiles');
  if (!container) return;

  const childProfiles = Array.isArray(childProfilesInput) ? childProfilesInput.filter(Boolean) : [];
  const hasAggregateFallback = Boolean(job?.characteristic || job?.birthTypeLabel || job?.specialNeeds);
  const fallbackChildren = childProfiles.length
    ? childProfiles
    : (hasAggregateFallback
      ? [{
          label: 'Be 1',
          characteristic: job?.characteristic || '',
          birthTypeLabel: job?.birthTypeLabel || '',
          specialNeeds: job?.specialNeeds || ''
        }]
      : []);
  const requiredCount = normalizeChildrenCount(job?.numberOfChildren || fallbackChildren.length || 1);

  if (!fallbackChildren.length && !job?.numberOfChildren) {
    container.innerHTML = '';
    container.classList.add('hidden');
    return;
  }

  let html = '';
  for (let idx = 0; idx < requiredCount; idx += 1) {
    const child = fallbackChildren[idx] || null;
    const displayIndex = idx + 1;

    if (!child) {
      html += `
        <div class="child-profile-panel child-profile-panel--missing child-profile-panel--preview">
          <div class="child-profile-panel__head">
          <p class="child-profile-panel__title">Bé thứ ${displayIndex}</p>
          </div>
          <p class="child-profile-panel__empty">Chưa thấy Child Profile của bé này.</p>
        </div>`;
      continue;
    }

    html += `
      <div class="child-profile-panel child-profile-panel--preview">
        <div class="child-profile-panel__head">
          <p class="child-profile-panel__title">Bé thứ ${displayIndex}</p>
          <span class="child-profile-panel__badge">${escapeHtml(child.label || `Bé ${displayIndex}`)}</span>
        </div>
        <div class="child-profile-grid">
          <div class="child-profile-field">
            <span class="child-profile-field__label">Đặc điểm</span>
            <p class="child-profile-field__value">${escapeHtml(child.characteristic || 'Chưa cập nhật')}</p>
          </div>
          <div class="child-profile-field">
            <span class="child-profile-field__label">Nhóm tuổi</span>
            <p class="child-profile-field__value">${escapeHtml(child.birthTypeLabel || 'Chưa cập nhật')}</p>
          </div>
          <div class="child-profile-field child-profile-field--full">
            <span class="child-profile-field__label">Nhu cầu đặc biệt</span>
            <p class="child-profile-field__value">${escapeHtml(child.specialNeeds || 'Không có')}</p>
          </div>
        </div>
      </div>`;
  }

  container.innerHTML = html;
  container.classList.remove('hidden');
}

function openPreview(job) {
  if (!job) return;
  editingJobId = job.id;
  const childProfiles = Array.isArray(job.children) ? job.children : [];
  const birthTypeText = childProfiles.length > 1
    ? `${childProfiles.length} bé (xem chi tiết bên dưới)`
    : (childProfiles[0]?.birthTypeLabel || job.birthTypeLabel || 'Chưa cập nhật');
  document.getElementById('pv-title').textContent = job.title || 'Tin đăng';
  document.getElementById('pv-parentName').textContent = job.parentName || 'Phụ huynh';
  const premiumBadges = document.getElementById('pv-premiumBadges');
  if (premiumBadges) premiumBadges.innerHTML = renderJobBenefitBadges(job);
  document.getElementById('pv-type').textContent = JOB_TYPES[job.jobType] || 'Khác';
  document.getElementById('pv-sal').textContent = formatSalaryRange(job.salaryMin, job.salaryMax, job.salaryNegotiable);
  document.getElementById('pv-status').textContent = POST_STATUS_LABELS[job.status] || 'Đang cập nhật';
  document.getElementById('pv-loc').textContent = [job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chưa cập nhật';
  document.getElementById('pv-kids').textContent = job.numberOfChildren ? `${job.numberOfChildren} bé` : 'Chưa cập nhật';
  document.getElementById('pv-characteristic').textContent = job.characteristic || 'Chưa cập nhật';
  document.getElementById('pv-birthType').textContent = birthTypeText;
  document.getElementById('pv-specialNeeds').textContent = job.specialNeeds || 'Không có';
  document.getElementById('pv-ageRange').textContent = formatAgeRange(job.minNannyAge, job.maxNannyAge);
  document.getElementById('pv-coords').textContent = job.latitude && job.longitude ? `${job.latitude}, ${job.longitude}` : 'Khu vực gần đúng';
  document.getElementById('pv-distance').textContent = job.distanceKm ? `${job.distanceKm.toFixed(1)} km` : 'Chưa xác định';
  document.getElementById('pv-desc').textContent = job.description || 'Không có mô tả';
  document.getElementById('pv-moderation').textContent = MODERATION_LABELS[job.moderationStatus] || 'Đang cập nhật';
  renderPreviewChildProfiles(job, childProfiles);
  const noteEl = document.getElementById('pv-note');
  noteEl.textContent = job.moderationNote || '';
  noteEl.classList.toggle('hidden', !job.moderationNote);

  const skillsEl = document.getElementById('pv-skills');
  skillsEl.innerHTML = (job.skills && job.skills.length)
    ? job.skills.map((skill) => `<span class="px-3 py-1 rounded-full bg-white text-orange-700 text-xs font-bold border border-orange-100">${escapeHtml(skill)}</span>`).join('')
    : '<span class="text-sm text-slate-400">Chưa có kỹ năng yêu cầu.</span>';

  document.getElementById('pv-schedule').innerHTML = renderReadOnlySchedule(job.scheduleSlots || []);

  const parentBtn = document.getElementById('pv-parentBtn');
  if (parentBtn) {
    parentBtn.href = job.parentUserId ? `/Profile/ViewUser?userId=${encodeURIComponent(job.parentUserId)}` : '#';
  }

  updatePreviewActionButtons(job);

  document.getElementById('previewModal')?.classList.add('show');
}

function closePreview() {
  document.getElementById('previewModal')?.classList.remove('show');
}


function handleEditCityChange() {
  handleCityChange('ef');
}

function bindClickOnlySelect(selectElement) {
  if (!selectElement || selectElement.dataset.clickOnlyBound === 'true') return;

  selectElement.dataset.clickOnlyBound = 'true';
  selectElement.style.caretColor = 'transparent';
  let previousValue = selectElement.value;

  const emitSelectionChangeIfNeeded = () => {
    if (selectElement.value === previousValue) return;
    previousValue = selectElement.value;
    selectElement.dispatchEvent(new Event('input', { bubbles: true }));
    selectElement.dispatchEvent(new Event('change', { bubbles: true }));
  };

  selectElement.addEventListener('keydown', (event) => {
    if (event.key === 'Tab') return;
    event.preventDefault();
  });
  selectElement.addEventListener('mousedown', () => {
    previousValue = selectElement.value;
  });
  selectElement.addEventListener('click', () => {
    window.setTimeout(emitSelectionChangeIfNeeded, 0);
  });
}

function bindCreateJobPostingClickOnlySelects() {
  document.querySelectorAll('#createForm select').forEach((selectElement) => {
    bindClickOnlySelect(selectElement);
  });
}

async function bootstrapSearchPage() {
  ['cf-city', 'cf-district', 'ef-city', 'ef-district', 'cf-typeText', 'cf-childProfileText', 'cf-skillSelectText'].forEach((id) => {
    document.getElementById(id)?.setAttribute('autocomplete', 'off');
  });
  ['createForm', 'editForm'].forEach((id) => {
    document.getElementById(id)?.setAttribute('autocomplete', 'off');
  });

  attachCreateSelectPickers();
  bindCreateJobPostingClickOnlySelects();
  const createChildrenInput = document.getElementById('cf-children');
  if (createChildrenInput && createChildrenInput.dataset.bindChildrenCount !== 'true') {
    createChildrenInput.dataset.bindChildrenCount = 'true';
    createChildrenInput.addEventListener('change', handleCreateChildrenCountChange);
    createChildrenInput.addEventListener('input', handleCreateChildrenCountChange);
  }
  initMoneyInputs();
  initMap();
  loadLocationData();
  await loadOwnedJobs();
  await loadAppliedJobs();
  await doSearch();
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', bootstrapSearchPage, { once: true });
} else {
  bootstrapSearchPage();
}

window.addEventListener('pageshow', () => {
  if (!currentJobs.length) {
    bootstrapSearchPage();
  }
});
