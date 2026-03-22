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
const autocompleteDropdowns = new Map();

const DAY_LABELS = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];
const ROW_LABELS = ['Morning', 'Afternoon', 'Evening', 'Night'];

const GEO_FALLBACK = {
  'Ho Chi Minh': { lat: 10.776, lng: 106.701, radius: 7000, zoom: 11 },
  'Thanh pho Ho Chi Minh': { lat: 10.776, lng: 106.701, radius: 7000, zoom: 11 },
  'Ha Noi': { lat: 21.028, lng: 105.854, radius: 7000, zoom: 11 },
  'Da Nang': { lat: 16.054, lng: 108.202, radius: 6500, zoom: 11 }
};

function loadLocationData() {
  if (locationDataPromise) return locationDataPromise;

  locationDataPromise = fetch('https://provinces.open-api.vn/api/v2/?depth=3')
    .then((response) => response.ok ? response.json() : [])
    .then((data) => {
      provinces = Array.isArray(data) ? data : [];
      attachLocationAutocomplete('cf');
      attachLocationAutocomplete('ef');
      return provinces;
    })
    .catch(() => {
      provinces = [];
      return provinces;
    });

  return locationDataPromise;
}

function normalizeText(value) {
  return String(value ?? '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim();
}

function getProvinceOptions() {
  return provinces.map((province) => province.name);
}

function getDistrictOptions(cityName) {
  const selectedProvince = provinces.find((province) => province.name === cityName);
  return (selectedProvince?.districts || []).map((district) => district.name);
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

  const showForQuery = () => {
    const query = normalizeText(input.value);
    const filtered = optionGetter()
      .filter((option) => !query || normalizeText(option).includes(query))
      .slice(0, 12);

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
    () => getProvinceOptions(),
    (value) => {
      const districtInput = document.getElementById(`${prefix}-district`);
      if (districtInput) districtInput.value = '';
      handleCityChange(prefix, value);
    }
  );

  attachAutocomplete(
    prefix,
    'district',
    () => getDistrictOptions(document.getElementById(`${prefix}-city`)?.value.trim() || ''),
    () => {}
  );
}

function handleCityChange(prefix, explicitCityValue) {
  const cityInput = document.getElementById(`${prefix}-city`);
  const districtInput = document.getElementById(`${prefix}-district`);
  if (!cityInput) return;

  const cityValue = (explicitCityValue ?? cityInput.value).trim();
  const allowedDistricts = getDistrictOptions(cityValue);
  if (districtInput && districtInput.value && !allowedDistricts.includes(districtInput.value.trim())) {
    districtInput.value = '';
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

function formatAgeRange(min, max) {
  if (min && max) return `${min} - ${max} tuoi`;
  if (min) return `Tu ${min} tuoi`;
  if (max) return `Den ${max} tuoi`;
  return 'Khong yeu cau';
}

function showToast(message) {
  const toast = document.getElementById('toast');
  if (!toast) return;
  toast.textContent = message;
  toast.classList.add('show');
  clearTimeout(showToast._timer);
  showToast._timer = setTimeout(() => toast.classList.remove('show'), 2600);
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
        <span class="shrink-0 rounded-full bg-orange-50 px-3 py-1 text-xs font-extrabold text-orange-700">${escapeHtml(formatSalaryRange(job.salaryMin, job.salaryMax, job.salaryNegotiable))}</span>
      </div>
      <p class="mt-3 text-sm font-semibold text-orange-600">${escapeHtml([job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chua cap nhat dia diem')}</p>
      <p class="mt-3 text-sm leading-6 text-slate-500 line-clamp-2">${escapeHtml(job.description || 'Khong co mo ta.')}</p>
      <div class="mt-4 flex flex-wrap gap-2">
        <span class="px-3 py-1 rounded-full bg-orange-50 text-orange-700 text-xs font-bold">${escapeHtml(JOB_TYPES[job.jobType] || 'Khac')}</span>
        <span class="px-3 py-1 rounded-full bg-slate-100 text-slate-700 text-xs font-bold">${escapeHtml(MODERATION_LABELS[job.moderationStatus] || 'Dang cap nhat')}</span>
        <span class="px-3 py-1 rounded-full bg-blue-50 text-blue-700 text-xs font-bold">${job.numberOfChildren ? `${job.numberOfChildren} be` : 'Chua ro'}</span>
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

function getSelectedChild(prefix) {
  const childId = document.getElementById(`${prefix}-childProfileId`)?.value;
  const collection = prefix === 'cf' ? createChildren : editChildren;
  return collection.find((child) => String(child.id).toLowerCase() === String(childId).toLowerCase()) || null;
}

function renderChildren(prefix, selectedChildId) {
  const select = document.getElementById(`${prefix}-childProfileId`);
  if (!select) return;
  const collection = prefix === 'cf' ? createChildren : editChildren;
  select.innerHTML = collection.length
    ? collection.map((child) => `<option value="${escapeHtml(child.id)}">${escapeHtml(child.label)}</option>`).join('')
    : '<option value="">Chua co Child Profile</option>';
  if (selectedChildId) select.value = selectedChildId;
  setProfileFields(prefix, getSelectedChild(prefix));
}

function applyPrefill(prefix, data, selectedChildId) {
  const childrenInput = document.getElementById(`${prefix}-children`);
  if (childrenInput && (!childrenInput.value || Number(childrenInput.value) < 1)) {
    childrenInput.value = data?.numberOfChildren || 1;
  }
  const collection = prefix === 'cf' ? createChildren : editChildren;
  collection.splice(0, collection.length, ...((data?.children) || []));
  renderChildren(prefix, selectedChildId || data?.selectedChildProfileId);
}

function handleCreateChildChange() {
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
  await loadLocationData();
  document.getElementById('createForm')?.reset();
  createSkills = [];
  createSchedule = [];
  createChildren = [];
  setCreateStatus(1);
  renderSkillCollection('cf-skills', createSkills, 'removeCreateSkill');
  renderScheduleGrid('cf-schedule', createSchedule, 'toggleCreateSchedule');
  applyPrefill('cf', {}, null);

  try {
    const res = await fetch('/Search/Prefill', { credentials: 'same-origin' });
    const json = await res.json();
    if (json.success && json.data) applyPrefill('cf', json.data, json.data.selectedChildProfileId);
  } catch { }

  document.getElementById('createModal')?.classList.add('show');
}

function closeCreate() {
  document.getElementById('createModal')?.classList.remove('show');
  isSubmittingCreate = false;
}

function getCreatePayload() {
  return {
    title: document.getElementById('cf-title')?.value.trim() || '',
    description: document.getElementById('cf-desc')?.value.trim() || '',
    jobType: Number(document.getElementById('cf-type')?.value || 1),
    numberOfChildren: Number(document.getElementById('cf-children')?.value || 1),
    childProfileId: document.getElementById('cf-childProfileId')?.value || null,
    salaryMin: document.getElementById('cf-salMin')?.value ? Number(document.getElementById('cf-salMin').value) : null,
    salaryMax: document.getElementById('cf-salMax')?.value ? Number(document.getElementById('cf-salMax').value) : null,
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
  if (!payload.title || payload.title.length < 5) return 'Tieu de bai dang phai tu 5 ky tu tro len.';
  if (!payload.description || payload.description.length < 10) return 'Mo ta chi tiet phai tu 10 ky tu tro len.';
  if (!payload.childProfileId) return 'Vui long chon tre tu Child Profile.';
  if (!payload.location || payload.location.length < 3) return 'Vui long nhap dia chi chi tiet.';
  if (!payload.city) return 'Vui long nhap thanh pho.';
  if (!payload.district) return 'Vui long nhap quan/huyen.';
  if (!Array.isArray(payload.skills) || !payload.skills.length) return 'Vui long chon it nhat 1 ky nang.';
  if (!Array.isArray(payload.scheduleSlots) || !payload.scheduleSlots.length) return 'Vui long chon it nhat 1 khung lich.';
  return '';
}

async function submitCreate() {
  if (isSubmittingCreate) return;
  const payload = getCreatePayload();
  const error = validatePayload(payload);
  if (error) {
    showToast(error);
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
      showToast(json.message || 'Dang bai that bai');
      isSubmittingCreate = false;
      if (submitBtn) {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Dang bai';
      }
      return;
    }

    closeCreate();
    showToast('Bai dang da duoc tao va dang cho moderator duyet');
    window.dispatchEvent(new CustomEvent('nm:notifications-refresh'));
    doSearch();
  } catch {
    showToast('Loi ket noi server');
    isSubmittingCreate = false;
    if (submitBtn) {
      submitBtn.disabled = false;
      submitBtn.textContent = 'Dang bai';
    }
  }
}

async function openEdit(job) {
  if (!job?.id) return;
  window.location.href = `/Search/Edit/${job.id}`;
}

function closeEdit() {
  document.getElementById('editModal')?.classList.remove('show');
  isSubmittingEdit = false;
}

async function submitEdit() {
  if (editingJobId) {
    window.location.href = `/Search/Edit/${editingJobId}`;
  }
}

async function deleteJob() {
  if (!editingJobId) return;
  if (!confirm('Ban co chac muon xoa bai dang nay?')) return;
  try {
    const res = await fetch(`/Search/DeleteJob/${editingJobId}`, { method: 'DELETE', credentials: 'same-origin' });
    const json = await res.json();
    if (!json.success) {
      showToast(json.message || 'Xoa that bai');
      return;
    }
    closeEdit();
    showToast('Da xoa bai dang');
    doSearch();
  } catch {
    showToast('Loi ket noi server');
  }
}

function openPreview(job) {
  if (!job) return;
  editingJobId = job.id;
  document.getElementById('pv-title').textContent = job.title || 'Tin dang';
  document.getElementById('pv-parentName').textContent = job.parentName || 'Phu huynh';
  document.getElementById('pv-type').textContent = JOB_TYPES[job.jobType] || 'Khac';
  document.getElementById('pv-sal').textContent = formatSalaryRange(job.salaryMin, job.salaryMax, job.salaryNegotiable);
  document.getElementById('pv-status').textContent = POST_STATUS_LABELS[job.status] || 'Dang cap nhat';
  document.getElementById('pv-loc').textContent = [job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chua cap nhat';
  document.getElementById('pv-kids').textContent = job.numberOfChildren ? `${job.numberOfChildren} be` : 'Chua cap nhat';
  document.getElementById('pv-characteristic').textContent = job.characteristic || 'Chua cap nhat';
  document.getElementById('pv-birthType').textContent = job.birthTypeLabel || 'Chua cap nhat';
  document.getElementById('pv-specialNeeds').textContent = job.specialNeeds || 'Khong co';
  document.getElementById('pv-ageRange').textContent = formatAgeRange(job.minNannyAge, job.maxNannyAge);
  document.getElementById('pv-coords').textContent = job.latitude && job.longitude ? `${job.latitude}, ${job.longitude}` : 'Khu vuc gan dung';
  document.getElementById('pv-distance').textContent = job.distanceKm ? `${job.distanceKm.toFixed(1)} km` : 'Chua xac dinh';
  document.getElementById('pv-desc').textContent = job.description || 'Khong co mo ta';
  document.getElementById('pv-moderation').textContent = MODERATION_LABELS[job.moderationStatus] || 'Dang cap nhat';
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
    parentBtn.href = job.parentProfileId ? `/ParentProfile/Detail/${job.parentProfileId}` : '#';
  }

  document.getElementById('previewModal')?.classList.add('show');
}

function closePreview() {
  document.getElementById('previewModal')?.classList.remove('show');
}

function handleCreateCityChange() {
  handleCityChange('cf');
}

function handleEditCityChange() {
  handleCityChange('ef');
}

function bootstrapSearchPage() {
  initMap();
  loadLocationData();
  doSearch();
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
