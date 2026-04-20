const JOB_TYPES = { 1: 'Full-time', 2: 'Part-time', 3: 'Qua dem' };
const MODERATION_LABELS = { 0: 'Dang cho duyet', 1: 'Da bi tu choi', 2: 'Cong khai' };
const POST_STATUS_LABELS = { 1: 'Cong khai', 2: 'An bai dang' };

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

const DAY_LABELS = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];
const ROW_LABELS = ['Morning', 'Afternoon', 'Evening', 'Night'];

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

function attachSelectPicker(prefix, kind, selectId, inputId, emptyLabel, onValueChanged) {
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

  syncFromSelect();
  input.addEventListener('focus', () => {
    const currentValue = input.value.trim();
    const shouldOpenAll = !currentValue || select.value === '' || normalizeText(currentValue) === normalizedEmptyLabel;
    renderOptions(shouldOpenAll ? '' : currentValue);
  });
  input.addEventListener('click', () => {
    const currentValue = input.value.trim();
    const shouldOpenAll = !currentValue || select.value === '' || normalizeText(currentValue) === normalizedEmptyLabel;
    renderOptions(shouldOpenAll ? '' : currentValue);
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
  attachSelectPicker('cf', 'childPicker', 'cf-childProfileId', 'cf-childProfileText', 'Chọn trẻ', () => handleCreateChildChange());
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
  if (!value) return 'Khong xac dinh';
  const number = Number(value);
  if (!Number.isFinite(number) || number <= 0) return 'Khong xac dinh';
  return new Intl.NumberFormat('vi-VN').format(number) + ' VND';
}

function formatSalaryRange(min, max, negotiable) {
  if (negotiable) return 'Thuong luong';
  if (min && max) return `${formatSalary(min)} - ${formatSalary(max)}`;
  if (min) return `Tu ${formatSalary(min)}`;
  if (max) return `Den ${formatSalary(max)}`;
  return 'Khong xac dinh';
}

function getJobPlanLabel(job) {
  const code = String(job?.subscriptionPlanCode || '').trim().toUpperCase();
  if (code === 'PRO') return 'Parent Pro';
  if (code === 'PLUS') return 'Parent Plus';
  return '';
}

function renderJobBenefitBadges(job) {
  const badges = [];
  const planLabel = getJobPlanLabel(job);

  if (planLabel) {
    badges.push(`<span class="px-3 py-1 rounded-full bg-amber-50 text-amber-700 text-xs font-bold">${escapeHtml(planLabel)}</span>`);
  }

  if (job?.searchPriority) {
    badges.push('<span class="px-3 py-1 rounded-full bg-amber-50 text-amber-700 text-xs font-bold">Uu tien tim kiem</span>');
  } else if (job?.featuredBadge) {
    badges.push('<span class="px-3 py-1 rounded-full bg-orange-50 text-orange-700 text-xs font-bold">Tin noi bat</span>');
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
  if (min && max) return `${min} - ${max} tuoi`;
  if (min) return `Tu ${min} tuoi`;
  if (max) return `Den ${max} tuoi`;
  return 'Khong yeu cau';
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
    button.title = isFavorite ? 'Bo luu bai dang' : 'Luu bai dang';
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
    notifyToast('Vui long dang nhap de luu bai dang.', 'warning');
    return;
  }

  if (!isNannyRole()) {
    notifyToast('Chi Nanny moi co quyen luu bai dang.', 'warning');
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
      notifyToast(json?.message || 'Khong the cap nhat luu bai dang.', 'error');
      return;
    }

    const favoriteState = typeof payload?.isFavorite === 'boolean'
      ? payload.isFavorite
      : !!json?.isFavorite;

    setJobFavoriteState(jobId, favoriteState);
    notifyToast(
      payload?.message || json?.message || (favoriteState ? 'Da luu bai dang.' : 'Da bo luu bai dang.'),
      favoriteState ? 'success' : 'info'
    );
  } catch {
    notifyToast('Khong the cap nhat luu bai dang.', 'error');
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
        throw new Error('Phien dang nhap da het han. Vui long dang nhap lai.');
      }
      throw new Error('Server tra ve du lieu khong hop le khi upload media.');
    }
  }

  if (!response.ok) {
    throw new Error(result?.message || `Upload media that bai (HTTP ${response.status}).`);
  }

  if (!result?.success || !result?.data?.urls || !Array.isArray(result.data.urls) || result.data.urls.length === 0) {
    throw new Error(result?.message || 'Upload media that bai.');
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
        '/Complain/UploadComplainMedia?mediaType=image',
        'Vui long chon it nhat mot anh.'
      );
      resolve(urls[0]);
    } catch (error) {
      reject({ message: error?.message || 'Khong the upload anh.' });
    }
  });
}

function handleComplainTinyFilePicker(callback, meta) {
  const isImage = meta.filetype === 'image';
  const accept = isImage
    ? 'image/*'
    : 'video/mp4,video/webm,video/ogg,video/quicktime,.mp4,.webm,.ogg,.mov';
  const endpoint = isImage
    ? '/Complain/UploadComplainMedia?mediaType=image'
    : '/Complain/UploadComplainMedia?mediaType=video';

  pickLocalComplainFile(accept, false).then(async (file) => {
    if (!file) return;

    try {
      const urls = await uploadComplainMediaFiles([file], endpoint, 'Vui long chon file de upload.');
      const url = urls[0];
      if (isImage) callback(url, { alt: file.name || 'image' });
      else callback(url, { source2: '', poster: '' });
    } catch (error) {
      notifyToast(error?.message || 'Khong the upload media.', 'error');
    }
  });
}

const complainMediaUploadConfig = {
  image: {
    accept: 'image/*',
    endpoint: '/Complain/UploadComplainMedia?mediaType=image',
    emptyMessage: 'Vui long chon it nhat mot anh.',
    successMessage: 'Upload anh thanh cong.',
    failedMessage: 'Khong the upload anh.',
    buildHtml: (url) => `<p><img src="${url}" alt="complain-image" /></p>`
  },
  video: {
    accept: 'video/mp4,video/webm,video/ogg,video/quicktime,.mp4,.webm,.ogg,.mov',
    endpoint: '/Complain/UploadComplainMedia?mediaType=video',
    emptyMessage: 'Vui long chon it nhat mot video.',
    successMessage: 'Upload video thanh cong.',
    failedMessage: 'Khong the upload video.',
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
      if (applyTextEl) applyTextEl.textContent = applied ? 'Da ung tuyen' : 'Ung tuyen ngay';
    } else {
      applyBtn.disabled = false;
      applyBtn.classList.remove('opacity-60', 'cursor-not-allowed');
      applyBtn.onclick = null;
      applyBtn.removeAttribute('data-job-id');
      if (applyTextEl) applyTextEl.textContent = 'Ung tuyen ngay';
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

  if (!isLoggedIn()) {
    notifyToast('Vui long dang nhap de ung tuyen bai dang.', 'warning');
    window.location.href = '/Auth/Login';
    return;
  }

  if (!isNannyRole()) {
    notifyToast('Chi Nanny moi co quyen ung tuyen bai dang.', 'warning');
    return;
  }

  if (!jobId) {
    notifyToast('Khong tim thay bai dang de ung tuyen.', 'error');
    return;
  }

  if (isJobApplied(jobId)) {
    notifyToast('Ban da gui don ung tuyen cho bai dang nay.', 'info');
    return;
  }

  const applyBtn = document.getElementById('pv-applyBtn');
  const applyTextEl = document.getElementById('pv-applyBtnText');
  const previousText = applyTextEl?.textContent || 'Ung tuyen ngay';

  try {
    if (applyBtn) {
      applyBtn.disabled = true;
      applyBtn.classList.add('opacity-60', 'cursor-not-allowed');
    }
    if (applyTextEl) applyTextEl.textContent = 'Dang gui...';

    const response = await fetch(`/Search/ApplyJob?jobPostingId=${encodeURIComponent(jobId)}`, {
      method: 'POST',
      credentials: 'same-origin'
    });
    const json = await response.json();
    const payload = json?.raw && typeof json.raw === 'object' ? json.raw : json;
    const isSuccess = !!(json?.success || payload?.success);

    if (!isSuccess) {
      notifyToast(json?.message || payload?.message || 'Khong the ung tuyen bai dang.', 'error');
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

    notifyToast('Gui don thanh cong. Vui long cho Parent phan hoi.', 'success');
    window.dispatchEvent(new CustomEvent('nm:notifications-refresh'));
  } catch {
    notifyToast('Khong the ung tuyen bai dang.', 'error');
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
    notifyToast('Vui long dang nhap de gui phan nan.', 'warning');
    window.location.href = '/Auth/Login';
    return;
  }

  if (!canComplainJob(job)) {
    notifyToast('Ban khong the phan nan bai dang cua chinh minh.', 'warning');
    return;
  }

  pendingComplainJob = job;
  isSubmittingJobComplain = false;
  ensureComplainEditorsInitialized();

  const targetEl = document.getElementById('jobComplainTarget');
  const jobPostingIdInput = document.getElementById('jobComplainJobPostingId');
  if (targetEl) targetEl.textContent = job?.title ? `Bai dang: ${job.title}` : 'Bai dang';
  if (jobPostingIdInput) jobPostingIdInput.value = job?.id || '';

  setComplainEditorContent('jobComplainReason', '');
  setComplainEditorContent('jobComplainEvidence', '');

  const submitBtn = document.getElementById('jobComplainSubmitBtn');
  if (submitBtn) {
    submitBtn.disabled = false;
    submitBtn.textContent = 'Gui phan nan';
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
    notifyToast('Khong tim thay bai dang can phan nan.', 'error');
    return;
  }

  const reasonEl = document.getElementById('jobComplainReason');
  const submitBtn = document.getElementById('jobComplainSubmitBtn');

  const reasonHtml = getComplainEditorContent('jobComplainReason');
  const evidenceHtml = getComplainEditorContent('jobComplainEvidence');
  const reason = extractPlainText(reasonHtml);
  const evidence = String(evidenceHtml || '').trim();

  if (reason.length < 5) {
    notifyToast('Ly do phan nan phai co it nhat 5 ky tu.', 'warning');
    if (typeof tinymce !== 'undefined' && tinymce.get('jobComplainReason')) tinymce.get('jobComplainReason').focus();
    else reasonEl?.focus();
    return;
  }

  if (isSubmittingJobComplain) return;
  isSubmittingJobComplain = true;

  if (submitBtn) {
    submitBtn.disabled = true;
    submitBtn.textContent = 'Dang gui...';
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
      notifyToast(json?.message || payload?.message || 'Khong the gui phan nan bai dang.', 'error');
      isSubmittingJobComplain = false;
      if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Gui phan nan';
      }
      return;
    }

    closeJobComplainModal();
    notifyToast('Gui phan nan thanh cong.', 'success');
    window.dispatchEvent(new CustomEvent('nm:notifications-refresh'));
  } catch {
    notifyToast('Khong the gui phan nan bai dang.', 'error');
    isSubmittingJobComplain = false;
    if (submitBtn) {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Gui phan nan';
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
    return '<span class="text-sm text-gray-400">Chua chon lich cu the.</span>';
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

  resultCount.textContent = `${jobs.length} tin dang`;
  clearMapMarkers();

  if (!jobs.length) {
    list.innerHTML = `
      <div class="rounded-[24px] border border-dashed border-slate-200 bg-white px-6 py-16 text-center text-slate-500">
        <span class="material-icons-round text-[34px] text-orange-300">search_off</span>
        <h3 class="mt-3 text-lg font-bold text-slate-900">Khong tim thay bai dang</h3>
        <p class="mt-2 text-sm text-slate-500">Thu tim o thanh pho khac hoac doi tu khoa.</p>
      </div>`;
    return;
  }

  list.innerHTML = jobs.map((job, idx) => `
    <article class="job-card rounded-[26px] border border-orange-100 bg-white p-5 mb-4 shadow-[0_10px_30px_rgba(15,23,42,.04)]"
             data-idx="${idx}" data-id="${escapeHtml(job.id)}">
      <div class="flex items-start justify-between gap-3">
        <div class="min-w-0">
          <h3 class="text-[18px] leading-6 font-extrabold text-slate-900">${escapeHtml(job.title || 'Tin dang')}</h3>
          <p class="mt-2 text-sm font-semibold text-slate-500">${escapeHtml(job.parentName || 'Phu huynh')}</p>
        </div>
        <div class="job-card__head-right">
          <span class="shrink-0 rounded-full bg-orange-50 px-3 py-1 text-xs font-extrabold text-orange-700">${escapeHtml(formatSalaryRange(job.salaryMin, job.salaryMax, job.salaryNegotiable))}</span>
          ${isNannyRole() ? `
            <button type="button"
                    class="job-save-btn ${job.isFavorite ? 'active' : ''}"
                    data-job-id="${escapeHtml(normalizeGuid(job.id))}"
                    aria-pressed="${job.isFavorite ? 'true' : 'false'}"
                    aria-label="${job.isFavorite ? 'Bo luu bai dang' : 'Luu bai dang'}"
                    title="${job.isFavorite ? 'Bo luu bai dang' : 'Luu bai dang'}"
                    onclick="toggleJobFavorite('${escapeJs(job.id)}', event)">
              <span class="material-icons-round">${job.isFavorite ? 'bookmark' : 'bookmark_border'}</span>
            </button>` : ''}
        </div>
      </div>
      <p class="mt-3 text-sm font-semibold text-orange-600">${escapeHtml([job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chua cap nhat dia diem')}</p>
      <p class="mt-3 text-sm leading-6 text-slate-500 line-clamp-2">${escapeHtml(job.description || 'Khong co mo ta.')}</p>
      <div class="mt-4 flex flex-wrap gap-2">
        ${renderJobBenefitBadges(job)}
        <span class="px-3 py-1 rounded-full bg-orange-50 text-orange-700 text-xs font-bold">${escapeHtml(JOB_TYPES[job.jobType] || 'Khac')}</span>
        <span class="px-3 py-1 rounded-full bg-slate-100 text-slate-700 text-xs font-bold">${escapeHtml(MODERATION_LABELS[job.moderationStatus] || 'Dang cap nhat')}</span>
        <span class="px-3 py-1 rounded-full bg-blue-50 text-blue-700 text-xs font-bold">${job.numberOfChildren ? `${job.numberOfChildren} be` : 'Chua ro'}</span>
        ${isNannyRole() && isJobApplied(job.id) ? '<span class="px-3 py-1 rounded-full bg-emerald-50 text-emerald-700 text-xs font-bold">Da ung tuyen</span>' : ''}
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
    marker.bindPopup(`<div class="text-sm font-semibold text-slate-700">${escapeHtml(job.title || 'Tin dang')}</div><div class="text-xs text-slate-500 mt-1">Khu vuc gan dung</div>`);
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

function ensureChildrenCountOptions(selectId, preferredValue) {
  const select = document.getElementById(selectId);
  if (!select) return;

  const maxChildren = 10;
  const hasExpectedOptions = select.options.length === maxChildren
    && Array.from(select.options).every((option, idx) => Number(option.value) === idx + 1);

  if (!hasExpectedOptions) {
    select.innerHTML = Array.from({ length: maxChildren }, (_, idx) => {
      const value = idx + 1;
      return `<option value="${value}">${value} bé</option>`;
    }).join('');
  }

  const resolvedValue = normalizeChildrenCount(preferredValue ?? select.value ?? 1);
  select.value = String(resolvedValue);
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
  for (let idx = 0; idx < count - 1; idx += 1) {
    tryAddChild(document.getElementById(`cf-extraChildProfileId-${idx}`)?.value || '');
  }

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
  const countInput = document.getElementById('cf-children');
  if (!container || !countInput) return;

  const requiredCount = normalizeChildrenCount(countInput.value);
  countInput.value = String(requiredCount);
  const extraCount = Math.max(requiredCount - 1, 0);
  if (requiredCount <= 1) {
    container.innerHTML = '';
    return;
  }

  const existingSelections = {};
  container.querySelectorAll('select[id^="cf-extraChildProfileId-"]').forEach((selectEl) => {
    existingSelections[selectEl.id] = selectEl.value || '';
  });

  const firstSelectedId = String(document.getElementById('cf-childProfileId')?.value || '').toLowerCase();
  const usedIds = new Set(firstSelectedId ? [firstSelectedId] : []);
  const findChildById = (childId) => {
    const normalizedId = String(childId || '').toLowerCase();
    return createChildren.find((child) => String(child.id).toLowerCase() === normalizedId) || null;
  };

  let html = '';

  for (let idx = 0; idx < extraCount; idx += 1) {
    const selectId = `cf-extraChildProfileId-${idx}`;
    const preservedValue = existingSelections[selectId] || '';
    const preservedChild = findChildById(preservedValue);

    let selectedChild = null;
    if (preservedChild && !usedIds.has(String(preservedChild.id).toLowerCase())) {
      selectedChild = preservedChild;
    } else {
      selectedChild = createChildren.find((child) => !usedIds.has(String(child.id).toLowerCase())) || null;
    }

    const selectedChildId = selectedChild ? String(selectedChild.id) : '';
    if (selectedChild) usedIds.add(String(selectedChild.id).toLowerCase());

    const selectableChildren = createChildren.filter((child) => {
      const normalizedId = String(child.id).toLowerCase();
      if (selectedChild && normalizedId === String(selectedChild.id).toLowerCase()) return true;
      return !usedIds.has(normalizedId);
    });

    const selectOptions = selectableChildren.length
      ? selectableChildren.map((child) => `
          <option value="${escapeHtml(child.id)}" ${String(child.id) === selectedChildId ? 'selected' : ''}>
            ${escapeHtml(child.label || 'Be')}
          </option>`).join('')
      : '<option value="">Chua co Child Profile phu hop</option>';

    const child = selectedChild;
    const displayIndex = idx + 2;

    if (!child) {
      html += `
        <div class="child-profile-panel child-profile-panel--missing">
          <div class="child-profile-panel__head">
            <p class="child-profile-panel__title">Be thu ${displayIndex}</p>
          </div>
          <div class="mt-2 mb-3">
            <label class="form-label">Chon be thu ${displayIndex}</label>
            <select id="${selectId}" class="form-input">${selectOptions}</select>
          </div>
          <p class="child-profile-panel__empty">Chua co Child Profile cho be nay. Vui long tao them Child Profile trong Quan ly con em.</p>
        </div>`;
      continue;
    }

    html += `
      <div class="child-profile-panel">
        <div class="child-profile-panel__head">
          <p class="child-profile-panel__title">Be thu ${displayIndex}</p>
          <span class="child-profile-panel__badge">${escapeHtml(child.label || `Be ${displayIndex}`)}</span>
        </div>
        <div class="mt-2 mb-3">
          <label class="form-label">Chon be thu ${displayIndex}</label>
          <select id="${selectId}" class="form-input">${selectOptions}</select>
        </div>
        <div class="child-profile-grid">
          <div class="child-profile-field">
            <span class="child-profile-field__label">Dac diem</span>
            <p class="child-profile-field__value">${escapeHtml(child.characteristic || 'Chua cap nhat')}</p>
          </div>
          <div class="child-profile-field">
            <span class="child-profile-field__label">Nhom tuoi</span>
            <p class="child-profile-field__value">${escapeHtml(child.birthTypeLabel || 'Chua cap nhat')}</p>
          </div>
          <div class="child-profile-field child-profile-field--full">
            <span class="child-profile-field__label">Nhu cau dac biet</span>
            <p class="child-profile-field__value">${escapeHtml(child.specialNeeds || 'Khong co')}</p>
          </div>
        </div>
      </div>`;
  }

  container.innerHTML = html;
  container.querySelectorAll('select[id^="cf-extraChildProfileId-"]').forEach((selectEl) => {
    bindClickOnlySelect(selectEl);
    selectEl.addEventListener('change', renderCreateExtraChildProfiles);
  });
}

function renderChildren(prefix, selectedChildId) {
  const select = document.getElementById(`${prefix}-childProfileId`);
  if (!select) return;
  const collection = prefix === 'cf' ? createChildren : editChildren;
  select.innerHTML = collection.length
    ? collection.map((child) => `<option value="${escapeHtml(child.id)}">${escapeHtml(child.label)}</option>`).join('')
    : '<option value="">Chua co Child Profile</option>';
  if (selectedChildId) select.value = selectedChildId;
  syncSelectPickerTexts();
  setProfileFields(prefix, getSelectedChild(prefix));
  if (prefix === 'cf') renderCreateExtraChildProfiles();
}

function applyPrefill(prefix, data, selectedChildId) {
  const childrenInput = document.getElementById(`${prefix}-children`);
  if (childrenInput) {
    const preferredCount = data?.numberOfChildren ?? childrenInput.value ?? 1;
    if (prefix === 'cf') {
      ensureChildrenCountOptions('cf-children', preferredCount);
    } else {
      childrenInput.value = String(normalizeChildrenCount(preferredCount));
    }
  }
  const collection = prefix === 'cf' ? createChildren : editChildren;
  collection.splice(0, collection.length, ...((data?.children) || []));
  renderChildren(prefix, selectedChildId || data?.selectedChildProfileId);
}

function handleCreateChildChange() {
  setProfileFields('cf', getSelectedChild('cf'));
  renderCreateExtraChildProfiles();
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
  ensureChildrenCountOptions('cf-children', 1);
  renderSkillCollection('cf-skills', createSkills, 'removeCreateSkill');
  renderScheduleGrid('cf-schedule', createSchedule, 'toggleCreateSchedule');
  applyPrefill('cf', {}, null);

  try {
    const res = await fetch('/Search/Prefill', { credentials: 'same-origin' });
    const json = await res.json();
    if (json.success && json.data) applyPrefill('cf', json.data, json.data.selectedChildProfileId);
  } catch { }

  renderCreateExtraChildProfiles();

  syncSelectPickerTexts();
  document.getElementById('createModal')?.classList.add('show');
}

function closeCreate() {
  document.getElementById('createModal')?.classList.remove('show');
  isSubmittingCreate = false;
}

function getCreatePayload() {
  const numberOfChildren = normalizeChildrenCount(document.getElementById('cf-children')?.value || 1);
  const selectedChildren = getCreateSelectedChildren(numberOfChildren);
  return {
    title: document.getElementById('cf-title')?.value.trim() || '',
    description: document.getElementById('cf-desc')?.value.trim() || '',
    jobType: Number(document.getElementById('cf-type')?.value || 1),
    numberOfChildren,
    childProfileId: selectedChildren[0]?.id || document.getElementById('cf-childProfileId')?.value || null,
    childProfileIds: selectedChildren.map((child) => child.id),
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
  if (!payload.title || payload.title.length < 5) return 'Tieu de bai dang phai tu 5 ky tu tro len.';
  if (!payload.description || payload.description.length < 10) return 'Mo ta chi tiet phai tu 10 ky tu tro len.';
  if (!Number.isFinite(payload.numberOfChildren) || payload.numberOfChildren < 1 || payload.numberOfChildren > 10) {
    return 'So tre can cham phai tu 1 den 10.';
  }
  if (payload.numberOfChildren > createChildren.length) {
    return `Ban da chon ${payload.numberOfChildren} tre nhung hien chi co ${createChildren.length} Child Profile. Vui long tao them Child Profile truoc khi dang bai.`;
  }
  if (!Array.isArray(payload.childProfileIds) || payload.childProfileIds.length !== payload.numberOfChildren) {
    return 'Danh sach tre duoc chon chua hop le. Vui long chon lai so tre can cham.';
  }
  if (!payload.childProfileId) return 'Vui long chon tre tu Child Profile.';
  if (!payload.salaryNegotiable && payload.salaryMin == null) return 'Vui long nhap luong toi thieu trong khoang 8.000.000 - 50.000.000 VND hoac bat thuong luong.';
  if (payload.salaryMin != null && (!Number.isFinite(payload.salaryMin) || payload.salaryMin < minimumSalary || payload.salaryMin > maximumSalary)) {
    return 'Luong toi thieu phai trong khoang 8.000.000 - 50.000.000 VND.';
  }
  if (payload.salaryMax != null && (!Number.isFinite(payload.salaryMax) || payload.salaryMax < minimumSalary || payload.salaryMax > maximumSalary)) {
    return 'Luong toi da phai trong khoang 8.000.000 - 50.000.000 VND.';
  }
  if (payload.salaryMin != null && payload.salaryMax != null && payload.salaryMin > payload.salaryMax) {
    return 'Luong toi thieu khong duoc lon hon luong toi da.';
  }
  if (!payload.location || payload.location.length < 3) return 'Vui long nhap dia chi chi tiet.';
  if (!payload.city) return 'Vui long nhap thanh pho.';
  if (!payload.district) return 'Vui long nhap phuong/xa.';
  if (!Array.isArray(payload.skills) || !payload.skills.length) return 'Vui long chon it nhat 1 ky nang.';
  if (!Array.isArray(payload.scheduleSlots) || !payload.scheduleSlots.length) return 'Vui long chon it nhat 1 khung lich.';
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
    submitBtn.textContent = 'Dang gui...';
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
      notifyToast(json.message || 'Dang bai that bai');
      isSubmittingCreate = false;
      if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Dang bai';
      }
      return;
    }

    closeCreate();
    notifyToast('Bai dang da duoc tao va dang cho moderator duyet');
    window.dispatchEvent(new CustomEvent('nm:notifications-refresh'));
    doSearch();
  } catch {
    notifyToast('Loi ket noi server');
    isSubmittingCreate = false;
    if (submitBtn) {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Dang bai';
    }
  }
}

async function openEdit(job) {
  if (!job?.id) return;
  if (!canEditJob(job)) {
    notifyToast('Ban khong co quyen chinh sua bai dang nay.');
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
      notifyToast('Ban khong co quyen chinh sua bai dang nay.');
      return;
    }
    window.location.href = `/Search/Edit/${editingJobId}`;
  }
}

async function deleteJob() {
  if (!editingJobId) return;
  if (!ownedJobIds.has(normalizeGuid(editingJobId))) {
    notifyToast('Ban khong co quyen xoa bai dang nay.');
    return;
  }
  if (!confirm('Ban co chac muon xoa bai dang nay?')) return;
  try {
    const res = await fetch(`/Search/DeleteJob/${editingJobId}`, { method: 'DELETE', credentials: 'same-origin' });
    const json = await res.json();
    if (!json.success) {
      notifyToast(json.message || 'Xoa that bai');
      return;
    }
    closeEdit();
    notifyToast('Da xoa bai dang');
    doSearch();
  } catch {
    notifyToast('Loi ket noi server');
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
    : (childProfiles[0]?.birthTypeLabel || job.birthTypeLabel || 'Chua cap nhat');
  document.getElementById('pv-title').textContent = job.title || 'Tin dang';
  document.getElementById('pv-parentName').textContent = job.parentName || 'Phu huynh';
  const premiumBadges = document.getElementById('pv-premiumBadges');
  if (premiumBadges) premiumBadges.innerHTML = renderJobBenefitBadges(job);
  document.getElementById('pv-type').textContent = JOB_TYPES[job.jobType] || 'Khac';
  document.getElementById('pv-sal').textContent = formatSalaryRange(job.salaryMin, job.salaryMax, job.salaryNegotiable);
  document.getElementById('pv-status').textContent = POST_STATUS_LABELS[job.status] || 'Dang cap nhat';
  document.getElementById('pv-loc').textContent = [job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chua cap nhat';
  document.getElementById('pv-kids').textContent = job.numberOfChildren ? `${job.numberOfChildren} be` : 'Chua cap nhat';
  document.getElementById('pv-characteristic').textContent = job.characteristic || 'Chua cap nhat';
  document.getElementById('pv-birthType').textContent = birthTypeText;
  document.getElementById('pv-specialNeeds').textContent = job.specialNeeds || 'Khong co';
  document.getElementById('pv-ageRange').textContent = formatAgeRange(job.minNannyAge, job.maxNannyAge);
  document.getElementById('pv-coords').textContent = job.latitude && job.longitude ? `${job.latitude}, ${job.longitude}` : 'Khu vuc gan dung';
  document.getElementById('pv-distance').textContent = job.distanceKm ? `${job.distanceKm.toFixed(1)} km` : 'Chua xac dinh';
  document.getElementById('pv-desc').textContent = job.description || 'Khong co mo ta';
  document.getElementById('pv-moderation').textContent = MODERATION_LABELS[job.moderationStatus] || 'Dang cap nhat';
  renderPreviewChildProfiles(job, childProfiles);
  const noteEl = document.getElementById('pv-note');
  noteEl.textContent = job.moderationNote || '';
  noteEl.classList.toggle('hidden', !job.moderationNote);

  const skillsEl = document.getElementById('pv-skills');
  skillsEl.innerHTML = (job.skills && job.skills.length)
    ? job.skills.map((skill) => `<span class="px-3 py-1 rounded-full bg-white text-orange-700 text-xs font-bold border border-orange-100">${escapeHtml(skill)}</span>`).join('')
    : '<span class="text-sm text-slate-400">Chua co ky nang yeu cau.</span>';

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

  selectElement.addEventListener('keydown', (event) => {
    if (event.key === 'Tab') return;
    event.preventDefault();
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
    createChildrenInput.addEventListener('change', renderCreateExtraChildProfiles);
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
