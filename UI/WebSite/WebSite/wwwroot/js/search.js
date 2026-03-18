const JOB_TYPES = { 1: 'Full-time', 2: 'Part-time', 3: 'Qua dem' };
const MODERATION_LABELS = { 0: 'Dang cho duyet', 1: 'Da bi tu choi', 2: 'Cong khai' };
const POST_STATUS_LABELS = { 1: 'Cong khai', 2: 'An bai dang' };

let map;
let markers = [];
let currentJobs = [];
let historyJobs = [];
let createSkills = [];
let editSkills = [];
let createSchedule = [];
let editSchedule = [];
let createChildren = [];
let editChildren = [];
let availableSkills = [];
let editingJobId = null;
let debounceTimer = null;
let provinces = [];
const pendingFocusJobId = new URLSearchParams(window.location.search).get('jobId');
let createValidationTouched = false;
let createTouchedFields = new Set();
let isSubmittingCreate = false;

const DAY_LABELS = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];
const ROW_LABELS = ['Morning', 'Afternoon', 'Evening', 'Night'];

const GEO_FALLBACK = {
  'Thanh pho Ho Chi Minh': { lat: 10.776, lng: 106.701 },
  'Ha Noi': { lat: 21.028, lng: 105.854 },
  'Da Nang': { lat: 16.054, lng: 108.202 }
};

async function loadProvinceOptions() {
  try {
    const res = await fetch('https://provinces.open-api.vn/api/?depth=2');
    provinces = await res.json() || [];
  } catch {
    provinces = [];
  }

  populateProvinceSelect('cf-cityOptions');
  populateProvinceSelect('ef-cityOptions');
}

function setupAutocompleteShell(inputId, listId) {
  const input = document.getElementById(inputId);
  const list = document.getElementById(listId);
  if (!input || !list) return;

  input.removeAttribute('list');
  const wrapper = input.parentElement;
  if (wrapper) wrapper.classList.add('autocomplete-field');

  if (list.tagName !== 'UL') {
    const dropdown = document.createElement('ul');
    dropdown.id = listId;
    dropdown.className = 'ac-dropdown';
    list.replaceWith(dropdown);
  } else {
    list.classList.add('ac-dropdown');
  }
}

function populateProvinceSelect(selectId) {
  const select = document.getElementById(selectId);
  if (!select) return;

  const inputId = selectId.replace('Options', '');
  const input = document.getElementById(inputId);
  const currentValue = input?.value || '';
  select.innerHTML = provinces.map(province => `
    <option value="${escapeHtml(province.name)}"></option>
  `).join('');

  if (input && currentValue && provinces.some(province => province.name === currentValue)) {
    input.value = currentValue;
  }

  populateDistrictSelect(selectId === 'cf-cityOptions' ? 'cf-districtOptions' : 'ef-districtOptions', input?.value || '');
}

function populateDistrictSelect(selectId, cityName, selectedDistrict = '') {
  const select = document.getElementById(selectId);
  if (!select) return;

  const province = provinces.find(item => item.name === cityName);
  const districts = province?.districts || [];
  select.innerHTML = districts.map(district => `
    <option value="${escapeHtml(district.name)}"></option>
  `).join('');

  const inputId = selectId.replace('Options', '');
  const input = document.getElementById(inputId);
  if (input && selectedDistrict && districts.some(district => district.name === selectedDistrict)) {
    input.value = selectedDistrict;
  }
}

function handleCreateCityChange() {
  populateDistrictSelect('cf-districtOptions', document.getElementById('cf-city').value);
  document.getElementById('cf-district').value = '';
  renderAutocompleteOptions('cf-city', 'cf-cityOptions', provinces.map(province => province.name), value => {
    document.getElementById('cf-city').value = value;
    document.getElementById('cf-district').value = '';
    populateDistrictSelect('cf-districtOptions', value);
  });
}

function handleEditCityChange() {
  populateDistrictSelect('ef-districtOptions', document.getElementById('ef-city').value);
  document.getElementById('ef-district').value = '';
  renderAutocompleteOptions('ef-city', 'ef-cityOptions', provinces.map(province => province.name), value => {
    document.getElementById('ef-city').value = value;
    document.getElementById('ef-district').value = '';
    populateDistrictSelect('ef-districtOptions', value);
  });
}

function handleCreateDistrictInput() {
  renderDistrictAutocomplete('cf-city', 'cf-district', 'cf-districtOptions');
}

function handleEditDistrictInput() {
  renderDistrictAutocomplete('ef-city', 'ef-district', 'ef-districtOptions');
}

function renderAutocompleteOptions(inputId, listId, options, onSelect) {
  const input = document.getElementById(inputId);
  const list = document.getElementById(listId);
  if (!input || !list) return;

  const keyword = input.value.trim().toLowerCase();
  const matches = options
    .filter(option => !keyword || option.toLowerCase().includes(keyword))
    .slice(0, 8);

  if (!matches.length) {
    list.classList.remove('show');
    list.innerHTML = '';
    return;
  }

  list.innerHTML = matches.map(option => `<li data-value="${escapeHtml(option)}">${escapeHtml(option)}</li>`).join('');
  list.classList.add('show');

  list.querySelectorAll('li').forEach(item => {
    item.addEventListener('mousedown', event => {
      event.preventDefault();
      const value = item.dataset.value || '';
      input.value = value;
      list.classList.remove('show');
      list.innerHTML = '';
      onSelect(value);
    });
  });
}

function renderDistrictAutocomplete(cityInputId, districtInputId, listId) {
  const city = document.getElementById(cityInputId)?.value || '';
  const province = provinces.find(item => item.name === city);
  const districtOptions = (province?.districts || []).map(district => district.name);
  renderAutocompleteOptions(districtInputId, listId, districtOptions, value => {
    document.getElementById(districtInputId).value = value;
  });
}

function setStatusToggle(toggleId, hiddenInputId, status) {
  const toggle = document.getElementById(toggleId);
  const hiddenInput = document.getElementById(hiddenInputId);
  if (!toggle || !hiddenInput) return;

  hiddenInput.value = String(status);
  toggle.querySelectorAll('.status-option').forEach((button, index) => {
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

function initMap() {
  const mapEl = document.getElementById('map');
  if (!mapEl) return;
  map = L.map('map').setView([10.776, 106.7], 11);
  L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    attribution: '&copy; CartoDB',
    maxZoom: 19
  }).addTo(map);
}

function debounceSearch() {
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(doSearch, 400);
}

async function doSearch() {
  const cityInput = document.getElementById('searchCity');
  const city = cityInput ? cityInput.value.trim() : '';
  const params = new URLSearchParams({ page: 1, pageSize: 20 });
  if (city) params.append('city', city);

  try {
    const res = await fetch(`${API_URL}?${params.toString()}`);
    const json = await res.json();
    currentJobs = json.data || [];
    renderJobs(currentJobs);
    focusJobFromQuery();
  } catch {
    currentJobs = [];
    renderJobs([]);
  }
}

function focusJobFromQuery() {
  if (!pendingFocusJobId) return;
  const idx = currentJobs.findIndex(job => String(job.id).toLowerCase() === pendingFocusJobId.toLowerCase());
  if (idx < 0) return;

  const card = document.querySelector(`.job-card[data-idx="${idx}"]`);
  if (card) {
    card.classList.add('active');
    card.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  setTimeout(() => {
    highlightOnMap(idx);
    openPreview(currentJobs[idx]);
  }, 180);
}

function renderJobs(jobs) {
  const list = document.getElementById('jobList');
  const resultCount = document.getElementById('resultCount');
  if (!list || !resultCount) return;

  resultCount.textContent = `${jobs.length} tin dang`;

  markers.forEach(item => {
    item.marker?.remove();
    item.circle?.remove();
  });
  markers = [];

  if (!jobs.length) {
    list.innerHTML = `
      <div class="text-center px-8 py-16">
        <div class="text-5xl mb-4">Tim</div>
        <h3 class="text-base font-bold text-gray-800 mb-2">Khong tim thay tin dang</h3>
        <p class="text-sm text-gray-500">Thu tim o thanh pho khac hoac doi tu khoa.</p>
      </div>`;
    return;
  }

  list.innerHTML = jobs.map((job, idx) => {
    const moderation = MODERATION_LABELS[job.moderationStatus] || 'Dang cap nhat';
    const skills = (job.skills || []).slice(0, 3).map(renderSkillTag).join('');
    const editBtn = IS_AUTH && job.isOwner
      ? `<button class="edit-btn flex items-center gap-1 px-2 py-1 rounded-lg bg-gray-50 hover:bg-orange-50 border border-gray-200 hover:border-orange-300 text-gray-500 hover:text-orange-600 text-xs font-bold transition-all" data-idx="${idx}">
          <span class="material-icons-round text-sm">edit</span>Sua
        </button>`
      : '';

      return `
      <div class="job-card bg-white border border-orange-100/70 rounded-[1.35rem] p-5 mb-4 shadow-[0_14px_32px_rgba(15,23,42,.06)] transition-all cursor-pointer" data-idx="${idx}">
         <div class="flex items-start justify-between gap-4">
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2 mb-2.5">
               <h3 class="text-[17px] leading-6 font-extrabold text-slate-900">${escapeHtml(job.title || 'Tin dang tim bao mau')}</h3>
               ${job.featuredBadge ? '<span class="px-2 py-0.5 rounded-full bg-amber-50 text-amber-700 text-[11px] font-bold border border-amber-200">Featured</span>' : ''}
              </div>
              <p class="text-[13px] text-slate-500 font-semibold">${escapeHtml(job.parentName || 'Nguoi dang')}</p>
            </div>
            <div class="flex items-center gap-2">${editBtn}</div>
          </div>
          <p class="text-[13px] font-bold text-orange-500 mt-2.5">${escapeHtml([job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chua cap nhat dia diem')}</p>
         <p class="text-[13px] text-slate-600 leading-6 mt-2.5 line-clamp-2">${escapeHtml(job.description || 'Khong co mo ta chi tiet.')}</p>
          <div class="flex flex-wrap gap-2 mt-4">
            <span class="px-2.5 py-1 rounded-xl bg-orange-50 text-orange-700 text-[11px] font-bold border border-orange-100">${JOB_TYPES[job.jobType] || 'Khac'}</span>
            <span class="px-2.5 py-1 rounded-xl bg-slate-100 text-slate-700 text-[11px] font-bold border border-slate-200">${moderation}</span>
            ${job.numberOfChildren ? `<span class="px-2.5 py-1 rounded-xl bg-blue-50 text-blue-700 text-[11px] font-bold border border-blue-100">${job.numberOfChildren} be</span>` : ''}
          </div>
          ${skills ? `<div class="flex flex-wrap gap-2 mt-4">${skills}</div>` : ''}
        </div>`;
  }).join('');

  document.querySelectorAll('.job-card').forEach(card => {
    card.addEventListener('click', () => {
      const idx = Number(card.dataset.idx);
      highlightOnMap(idx);
      openPreview(currentJobs[idx]);
    });
    card.addEventListener('mouseenter', () => {
      const idx = Number(card.dataset.idx);
      setMarkerHover(idx, true, false);
      card.classList.add('active');
    });
    card.addEventListener('mouseleave', () => {
      const idx = Number(card.dataset.idx);
      setMarkerHover(idx, false, false);
      card.classList.remove('active');
    });
  });

  document.querySelectorAll('.edit-btn').forEach(btn => {
    btn.addEventListener('click', event => {
      event.stopPropagation();
      const idx = Number(btn.dataset.idx);
      openEdit(currentJobs[idx]);
    });
  });

    jobs.forEach((job, idx) => {
      if (!map) return;
      const area = getAreaPresentation(job);
      const marker = L.marker(area.point, {
        icon: L.divIcon({
            className: 'job-map-marker',
            html: '<span class="job-map-marker__halo"></span><span class="job-map-marker__pin"><span class="job-map-marker__core"></span></span>',
            iconSize: [26, 34],
            iconAnchor: [13, 28]
        })
      }).addTo(map).bindPopup(`<strong>${escapeHtml(job.title || 'Tin dang')}</strong><br>Khu vuc gan dung`);
      const circle = L.circle(area.point, {
        radius: area.radius,
        color: '#f97316',
        weight: 1.5,
        opacity: .18,
        fillColor: '#fb923c',
        fillOpacity: .08,
        interactive: false
      }).addTo(map);
      marker.on('click', () => openPreview(job));
      markers.push({ marker, circle, element: marker.getElement() });
      if (idx === 0) map.setView(area.point, area.zoom);
    });
  }

function getAreaPresentation(job) {
  const hasDistrict = Boolean(job.district && String(job.district).trim());
  const hasCity = Boolean(job.city && String(job.city).trim());

  if (job.latitude && job.longitude) {
    if (hasDistrict) return { point: [job.latitude, job.longitude], radius: 2200, zoom: 12 };
    if (hasCity) return { point: [job.latitude, job.longitude], radius: 5200, zoom: 11 };
    return { point: [job.latitude, job.longitude], radius: 3800, zoom: 11 };
  }

  if (hasCity && GEO_FALLBACK[job.city]) {
    return { point: [GEO_FALLBACK[job.city].lat, GEO_FALLBACK[job.city].lng], radius: hasDistrict ? 2500 : 6500, zoom: hasDistrict ? 12 : 11 };
  }

  return { point: [10.776, 106.701], radius: 6000, zoom: 11 };
}

function highlightOnMap(idx) {
  if (!map) return;
  const job = currentJobs[idx];
  if (!job) return;
  const area = getAreaPresentation(job);
  map.flyTo(area.point, area.zoom, { animate: true, duration: 0.6 });
  markers.forEach((_, markerIdx) => setMarkerHover(markerIdx, markerIdx === idx, markerIdx === idx));
  if (markers[idx]) markers[idx].marker.openPopup();
}

function setMarkerHover(idx, active, shouldTogglePopup = false) {
  const markerItem = markers[idx];
  if (!markerItem) return;
  if (markerItem.element) {
    markerItem.element.classList.toggle('active', active);
  }
  markerItem.marker.setZIndexOffset(active ? 1000 : 0);
  if (markerItem.circle) {
    markerItem.circle.setStyle({
      opacity: active ? .42 : .18,
      fillOpacity: active ? .18 : .08,
      weight: active ? 2.5 : 1.5
    });
  }
  if (active && shouldTogglePopup) {
    markerItem.marker.openPopup();
  } else if (shouldTogglePopup) {
    markerItem.marker.closePopup();
  }
}

function renderSkillTag(skill) {
  return `<span class="px-2 py-1 rounded-full bg-orange-50 text-orange-700 text-[11px] font-bold border border-orange-100">${escapeHtml(skill)}</span>`;
}

function populateSkillOptions() {
  ['cf-skillSelect', 'ef-skillSelect'].forEach(id => {
    const select = document.getElementById(id);
    if (!select) return;
    const currentValue = select.value;
      const mappedSkills = availableSkills
        .map(skill => ({
        skillName: skill.skillName || skill.name || ''
        }))
        .filter(skill => skill.skillName);

    if (!mappedSkills.length) {
      const hasServerRenderedSkills = Array.from(select.options).some(option => option.value && option.value.trim());
      if (!hasServerRenderedSkills) {
        select.innerHTML = '<option value="">Chua co ky nang trong he thong</option>';
      }
      select.value = '';
      return;
    }

      const options = mappedSkills.map(skill => `
      <option value="${escapeHtml(skill.skillName)}">${escapeHtml(skill.skillName)}</option>`);
    select.innerHTML = `<option value="">Chon ky nang can yeu cau</option>${options.join('')}`;
    if (mappedSkills.some(skill => skill.skillName === currentValue)) {
      select.value = currentValue;
    } else {
      select.value = '';
    }
  });
}

async function loadSkillOptions() {
  try {
    const res = await fetch('/Search/Skills');
    const json = await res.json();
    const rawSkills = Array.isArray(json.data)
      ? json.data
      : Array.isArray(json)
        ? json
        : Array.isArray(json.raw?.data)
          ? json.raw.data
          : [];

      availableSkills = rawSkills.map(skill => ({
      skillName: skill.skillName || skill.SkillName || skill.name || skill.Name || ''
      })).filter(skill => skill.skillName);
  } catch {
    availableSkills = [];
  }
  populateSkillOptions();
}

function setProfileFields(prefix, profile) {
  document.getElementById(`${prefix}-characteristic`).value = profile?.characteristic || '';
  document.getElementById(`${prefix}-birthType`).value = profile?.birthTypeLabel || '';
  document.getElementById(`${prefix}-specialNeeds`).value = profile?.specialNeeds || '';
}

function populateChildOptions(prefix, children, selectedChildId) {
  const select = document.getElementById(`${prefix}-childProfileId`);
  if (!select) return;

  if (!Array.isArray(children) || !children.length) {
    select.innerHTML = '<option value="">Chua co Child Profile</option>';
    select.disabled = true;
    return;
  }

  select.disabled = false;
  select.innerHTML = children.map(child => `
    <option value="${child.id}">
      ${escapeHtml(child.label || 'Be')}
    </option>`).join('');

  const resolvedChildId = selectedChildId && children.some(child => child.id === selectedChildId)
    ? selectedChildId
    : children[0].id;
  select.value = resolvedChildId;
}

function getSelectedChild(prefix) {
  const select = document.getElementById(`${prefix}-childProfileId`);
  const selectedId = select?.value || '';
  const collection = prefix === 'cf' ? createChildren : editChildren;
  return collection.find(child => child.id === selectedId) || collection[0] || null;
}

function applyPrefill(prefix, data, selectedChildId) {
  const children = Array.isArray(data?.children) ? data.children : [];
  if (prefix === 'cf') createChildren = children;
  else editChildren = children;

  populateChildOptions(prefix, children, selectedChildId || data?.selectedChildProfileId || null);
  const selectedChild = getSelectedChild(prefix) || {
    characteristic: data?.characteristic,
    birthTypeLabel: data?.birthTypeLabel,
    specialNeeds: data?.specialNeeds
  };
  setProfileFields(prefix, selectedChild);
  const childrenInput = document.getElementById(`${prefix}-children`);
  if (childrenInput && (!childrenInput.value || Number(childrenInput.value) < 1)) {
    childrenInput.value = data?.numberOfChildren || 1;
  }
}

function handleCreateChildChange() {
  touchCreateFields('cf-childProfileId');
  setProfileFields('cf', getSelectedChild('cf'));
  updateCreateValidationUI();
}

function handleEditChildChange() {
  setProfileFields('ef', getSelectedChild('ef'));
}

function renderScheduleGrid(containerId, selected, onToggleName) {
  const container = document.getElementById(containerId);
  if (!container) return;
  const selectedSet = new Set(selected.map(slot => `${slot.dayOfWeek}-${slot.timeSlot}`));
  let html = '<div></div>';
  DAY_LABELS.forEach(label => {
    html += `<div class="schedule-col-label">${label}</div>`;
  });

  ROW_LABELS.forEach((rowLabel, timeSlot) => {
    html += `<div class="schedule-row-label">${rowLabel}</div>`;
    for (let dayOfWeek = 0; dayOfWeek < 7; dayOfWeek += 1) {
      const key = `${dayOfWeek}-${timeSlot}`;
      const active = selectedSet.has(key);
      html += `
        <button type="button"
                class="schedule-cell ${active ? 'active' : ''}"
                onclick="${onToggleName}(${dayOfWeek},${timeSlot})">
          <span class="schedule-check">${active ? '&#10003;' : ''}</span>
        </button>`;
    }
  });
  container.innerHTML = html;
}

function toggleCreateSchedule(dayOfWeek, timeSlot) {
  touchCreateFields('cf-schedule');
  toggleScheduleValue(createSchedule, dayOfWeek, timeSlot);
  renderScheduleGrid('cf-schedule', createSchedule, 'toggleCreateSchedule');
  updateCreateValidationUI();
}

function toggleEditSchedule(dayOfWeek, timeSlot) {
  toggleScheduleValue(editSchedule, dayOfWeek, timeSlot);
  renderScheduleGrid('ef-schedule', editSchedule, 'toggleEditSchedule');
}

function toggleScheduleValue(collection, dayOfWeek, timeSlot) {
  const idx = collection.findIndex(slot => slot.dayOfWeek === dayOfWeek && slot.timeSlot === timeSlot);
  if (idx >= 0) collection.splice(idx, 1);
  else collection.push({ dayOfWeek, timeSlot });
}

async function openCreate() {
  document.getElementById('createForm').reset();
  createSkills = [];
  createSchedule = [];
  createChildren = [];
  createValidationTouched = false;
  createTouchedFields = new Set();
  setCreateStatus(1);
  renderSkillCollection('cf-skills', createSkills, removeCreateSkill);
  renderScheduleGrid('cf-schedule', createSchedule, 'toggleCreateSchedule');
  applyPrefill('cf', {}, null);

  try {
    const res = await fetch('/Search/Prefill');
    const json = await res.json();
    if (json.success && json.data) applyPrefill('cf', json.data, json.data.selectedChildProfileId);
  } catch {
  }

  updateCreateValidationUI(true);
  document.getElementById('createModal').classList.add('show');
}

function closeCreate() {
  document.getElementById('createModal').classList.remove('show');
  isSubmittingCreate = false;
  createTouchedFields = new Set();
  updateCreateValidationUI(true);
}

function addCreateSkill() {
  touchCreateFields('cf-skillSelect');
  addSelectedSkillToCollection(document.getElementById('cf-skillSelect'), createSkills, 'cf-skills', removeCreateSkill);
  updateCreateValidationUI();
}

function addEditSkill() {
  addSelectedSkillToCollection(document.getElementById('ef-skillSelect'), editSkills, 'ef-skills', removeEditSkill);
}

function removeCreateSkill(value) {
  touchCreateFields('cf-skillSelect');
  createSkills = createSkills.filter(skill => skill !== value);
  renderSkillCollection('cf-skills', createSkills, removeCreateSkill);
  updateCreateValidationUI();
}

function removeEditSkill(value) {
  editSkills = editSkills.filter(skill => skill !== value);
  renderSkillCollection('ef-skills', editSkills, removeEditSkill);
}

function addSelectedSkillToCollection(select, collection, containerId, removeHandler) {
  const value = select.value.trim();
  if (!value || collection.includes(value)) return;
  collection.push(value);
  select.value = '';
  renderSkillCollection(containerId, collection, removeHandler);
}

function renderSkillCollection(containerId, collection, removeHandler) {
  const container = document.getElementById(containerId);
  if (!container) return;
  container.innerHTML = collection.map(skill => `
    <span class="skill-tag">${escapeHtml(skill)}
      <button type="button" onclick="${removeHandler.name}('${escapeJs(skill)}')">x</button>
    </span>`).join('');
}

async function submitCreate() {
  if (isSubmittingCreate) return;
  createValidationTouched = true;
  const payload = {
    title: document.getElementById('cf-title').value.trim(),
    description: document.getElementById('cf-desc').value.trim(),
    jobType: Number(document.getElementById('cf-type').value),
    numberOfChildren: Number(document.getElementById('cf-children').value || 1),
    childProfileId: document.getElementById('cf-childProfileId').value || null,
    salaryMin: readOptionalNumber('cf-salMin'),
    salaryMax: readOptionalNumber('cf-salMax'),
    salaryNegotiable: document.getElementById('cf-negotiable').checked,
    location: document.getElementById('cf-location').value.trim(),
    city: document.getElementById('cf-city').value.trim(),
    district: document.getElementById('cf-district').value.trim(),
    minNannyAge: readOptionalNumber('cf-minAge'),
    maxNannyAge: readOptionalNumber('cf-maxAge'),
    skills: createSkills,
    scheduleSlots: createSchedule,
    status: Number(document.getElementById('cf-status').value || 1)
  };

  const validationError = validateJobPayload(payload);
  updateCreateValidationUI();
  if (validationError) {
    showToast(validationError);
    return;
  }

  try {
    isSubmittingCreate = true;
    updateCreateValidationUI();
    const res = await fetch('/Search/CreateJob', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    const json = await res.json();
    if (!json.success) {
      isSubmittingCreate = false;
      updateCreateValidationUI();
      showToast(`Loi: ${getErrorMessage(json, 'Dang bai that bai')}`);
      return;
    }
    closeCreate();
    showToast('Bai dang da duoc tao va dang cho moderator duyet');
    window.dispatchEvent(new CustomEvent('nm:notifications-refresh'));
    doSearch();
  } catch {
    isSubmittingCreate = false;
    updateCreateValidationUI();
    showToast('Loi ket noi server');
  }
}

async function openEdit(job) {
  editingJobId = job.id;
  document.getElementById('ef-id').value = job.id;
  document.getElementById('ef-title').value = job.title || '';
  document.getElementById('ef-desc').value = job.description || '';
  document.getElementById('ef-type').value = job.jobType || 1;
  document.getElementById('ef-children').value = job.numberOfChildren || 1;
  document.getElementById('ef-salMin').value = job.salaryMin || '';
  document.getElementById('ef-salMax').value = job.salaryMax || '';
  document.getElementById('ef-negotiable').checked = !!job.salaryNegotiable;
  document.getElementById('ef-location').value = job.location || '';
  document.getElementById('ef-minAge').value = job.minNannyAge || '';
  document.getElementById('ef-maxAge').value = job.maxNannyAge || '';
  setEditStatus(Number(job.status) || 1);
  document.getElementById('ef-city').value = job.city || '';
  populateDistrictSelect('ef-districtOptions', job.city || '', job.district || '');
  editChildren = [];
  editSkills = Array.isArray(job.skills) ? [...job.skills] : [];
  editSchedule = Array.isArray(job.scheduleSlots)
    ? job.scheduleSlots.map(slot => ({ dayOfWeek: slot.dayOfWeek, timeSlot: slot.timeSlot }))
    : [];
  renderSkillCollection('ef-skills', editSkills, removeEditSkill);
  renderScheduleGrid('ef-schedule', editSchedule, 'toggleEditSchedule');

  try {
    const res = await fetch('/Search/Prefill');
    const json = await res.json();
    if (json.success && json.data) {
      applyPrefill('ef', json.data, job.childProfileId || json.data.selectedChildProfileId);
    } else {
      setReadonlyProfile('ef', job, job.numberOfChildren);
    }
  } catch {
    setReadonlyProfile('ef', job, job.numberOfChildren);
  }

  document.getElementById('editModal').classList.add('show');
}

function closeEdit() {
  document.getElementById('editModal').classList.remove('show');
}

async function submitEdit() {
  if (!editingJobId) return;
  const payload = {
    title: document.getElementById('ef-title').value.trim(),
    description: document.getElementById('ef-desc').value.trim(),
    jobType: Number(document.getElementById('ef-type').value),
    numberOfChildren: Number(document.getElementById('ef-children').value || 1),
    childProfileId: document.getElementById('ef-childProfileId').value || null,
    salaryMin: readOptionalNumber('ef-salMin'),
    salaryMax: readOptionalNumber('ef-salMax'),
    salaryNegotiable: document.getElementById('ef-negotiable').checked,
    location: document.getElementById('ef-location').value.trim(),
    city: document.getElementById('ef-city').value.trim(),
    district: document.getElementById('ef-district').value.trim(),
    minNannyAge: readOptionalNumber('ef-minAge'),
    maxNannyAge: readOptionalNumber('ef-maxAge'),
    skills: editSkills,
    scheduleSlots: editSchedule,
    status: Number(document.getElementById('ef-status').value || 1)
  };

  const validationError = validateJobPayload(payload);
  if (validationError) {
    showToast(validationError);
    return;
  }

  try {
    const res = await fetch(`/Search/UpdateJob/${editingJobId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    const json = await res.json();
    if (!json.success) {
      showToast(`Loi: ${getErrorMessage(json, 'Cap nhat that bai')}`);
      return;
    }
    closeEdit();
    showToast('Bai dang da cap nhat va quay ve trang thai cho duyet');
    doSearch();
    if (document.getElementById('historyModal')?.classList.contains('show')) loadHistory();
  } catch {
    showToast('Loi ket noi server');
  }
}

async function deleteJob() {
  if (!editingJobId || !confirm('Ban co chac muon xoa bai dang nay?')) return;
  try {
    const res = await fetch(`/Search/DeleteJob/${editingJobId}`, { method: 'DELETE' });
    const json = await res.json();
    if (!json.success) {
      showToast(`Loi: ${getErrorMessage(json, 'Xoa that bai')}`);
      return;
    }
    closeEdit();
    showToast('Da xoa bai dang');
    doSearch();
    if (document.getElementById('historyModal')?.classList.contains('show')) loadHistory();
  } catch {
    showToast('Loi ket noi server');
  }
}

function openPreview(job) {
  document.getElementById('pv-title').textContent = job.title || 'Tin dang tim bao mau';
  document.getElementById('pv-parentName').textContent = `Dang boi ${job.parentName || 'Phu huynh'}`;
  document.getElementById('pv-type').textContent = JOB_TYPES[job.jobType] || 'Khac';
  document.getElementById('pv-sal').textContent = job.salaryNegotiable ? 'Thuong luong' : formatSalaryRange(job.salaryMin, job.salaryMax);
  document.getElementById('pv-status').textContent = POST_STATUS_LABELS[Number(job.status)] || 'Dang cap nhat';
  document.getElementById('pv-loc').textContent = [job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chua cap nhat';
  document.getElementById('pv-kids').textContent = job.numberOfChildren ? `${job.numberOfChildren} be` : 'Chua cap nhat';
  document.getElementById('pv-characteristic').textContent = job.characteristic || 'Chua cap nhat';
  document.getElementById('pv-birthType').textContent = job.birthTypeLabel || 'Chua cap nhat';
  document.getElementById('pv-specialNeeds').textContent = job.specialNeeds || 'Khong co';
  document.getElementById('pv-ageRange').textContent = formatAgeRange(job.minNannyAge, job.maxNannyAge);
  document.getElementById('pv-coords').textContent = job.latitude && job.longitude
    ? `${Number(job.latitude).toFixed(5)}, ${Number(job.longitude).toFixed(5)}`
    : 'Chua co toa do';
  document.getElementById('pv-distance').textContent = typeof job.distanceKm === 'number' ? `${job.distanceKm.toFixed(1)} km` : 'Chua xac dinh';
  document.getElementById('pv-desc').textContent = job.description || 'Khong co mo ta chi tiet.';
  document.getElementById('pv-moderation').textContent = MODERATION_LABELS[job.moderationStatus] || 'Dang cap nhat';

  const note = document.getElementById('pv-note');
  if (job.moderationNote) {
    note.textContent = `Ghi chu moderator: ${job.moderationNote}`;
    note.classList.remove('hidden');
  } else {
    note.textContent = '';
    note.classList.add('hidden');
  }

  document.getElementById('pv-skills').innerHTML = (job.skills || []).length
    ? job.skills.map(renderSkillTag).join('')
    : '<span class="text-sm text-gray-400">Chua co ky nang yeu cau.</span>';
  document.getElementById('pv-schedule').innerHTML = renderScheduleSummary(job.scheduleSlots || []);

  const btn = document.getElementById('pv-parentBtn');
  if (job.parentProfileId) {
    btn.href = `/ParentProfile/Detail/${job.parentProfileId}`;
    btn.style.display = 'inline-flex';
  } else {
    btn.style.display = 'none';
  }

  document.getElementById('previewModal').classList.add('show');
}

function closePreview() {
  document.getElementById('previewModal').classList.remove('show');
}

async function openHistory() {
  window.location.href = '/Search/History';
}

function closeHistory() {
  const modal = document.getElementById('historyModal');
  if (modal) modal.classList.remove('show');
}

async function loadHistory() {
  const body = document.getElementById('historyBody');
  body.innerHTML = '<div class="text-sm text-gray-500">Dang tai du lieu...</div>';
  try {
    const res = await fetch('/Search/MyJobs');
    const json = await res.json();
    historyJobs = json.data || [];
    if (!historyJobs.length) {
      body.innerHTML = '<div class="text-sm text-gray-500">Ban chua co bai dang nao.</div>';
      return;
    }

    body.innerHTML = historyJobs.map((job, idx) => `
      <div class="rounded-2xl border border-gray-100 bg-gray-50 p-4">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="text-sm font-bold text-gray-900">${escapeHtml(job.title || 'Tin dang')}</h3>
            <p class="text-xs text-gray-500 mt-1">${escapeHtml([job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chua cap nhat dia diem')}</p>
          </div>
          <button class="px-3 py-1.5 rounded-lg bg-white border border-gray-200 text-xs font-bold text-orange-600 hover:bg-orange-50" onclick="openHistoryPreview(${idx})">Xem chi tiet</button>
        </div>
        <div class="flex flex-wrap gap-2 mt-3">
          <span class="px-2 py-0.5 rounded-full bg-slate-100 text-slate-700 text-[11px] font-bold border border-slate-200">${MODERATION_LABELS[job.moderationStatus] || 'Dang cap nhat'}</span>
          <span class="px-2 py-0.5 rounded-full bg-orange-50 text-orange-700 text-[11px] font-bold border border-orange-100">${POST_STATUS_LABELS[Number(job.status)] || 'Dang cap nhat'}</span>
          ${job.skills && job.skills.length ? `<span class="px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 text-[11px] font-bold border border-blue-100">${job.skills.length} ky nang</span>` : ''}
        </div>
        <div class="mt-3">${renderScheduleSummary(job.scheduleSlots || [])}</div>
        ${job.moderationNote ? `<p class="text-xs text-gray-600 mt-3"><span class="font-bold">Ghi chu moderator:</span> ${escapeHtml(job.moderationNote)}</p>` : '<p class="text-xs text-gray-400 mt-3">Chua co ghi chu moderator.</p>'}
      </div>`).join('');
  } catch {
    body.innerHTML = '<div class="text-sm text-red-500">Khong tai duoc lich su bai dang.</div>';
  }
}

function openHistoryPreview(idx) {
  const job = historyJobs[idx];
  if (!job) return;
  closeHistory();
  openPreview(job);
}

function openPremium() {
  window.location.href = '/Subscription';
}

function renderScheduleSummary(slots) {
  if (!slots.length) return '<span class="text-sm text-gray-400">Chua chon lich cu the.</span>';
  const grouped = ROW_LABELS.map((rowLabel, timeSlot) => {
    const days = slots
      .filter(slot => slot.timeSlot === timeSlot)
      .sort((a, b) => a.dayOfWeek - b.dayOfWeek)
      .map(slot => DAY_LABELS[slot.dayOfWeek]);
    return { rowLabel, days };
  }).filter(item => item.days.length);

  return `<div class="schedule-summary">${grouped.map(item => `
    <div class="schedule-summary-card">
      <h4>${item.rowLabel}</h4>
      <p>${item.days.join(', ')}</p>
    </div>`).join('')}</div>`;
}

function showToast(message) {
  const toast = document.getElementById('toast');
  if (!toast) return;
  toast.textContent = message;
  toast.classList.add('show');
  setTimeout(() => toast.classList.remove('show'), 3000);
}

function readOptionalNumber(id) {
  const element = document.getElementById(id);
  const value = element ? element.value.trim() : '';
  return value ? Number(value) : null;
}

function formatSalary(value) {
  if (!value) return 'Khong xac dinh';
  return `${(value / 1000).toFixed(0)}k/gio`;
}

function formatSalaryRange(min, max) {
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

function getErrorMessage(json, fallback) {
  if (!json) return fallback;
  const errorSources = [json.errors, json.raw?.errors].filter(Boolean);
  for (const errors of errorSources) {
    const firstKey = Object.keys(errors)[0];
    const firstValue = firstKey ? errors[firstKey] : null;
    if (Array.isArray(firstValue) && firstValue[0]) return firstValue[0];
    if (typeof firstValue === 'string' && firstValue) return firstValue;
  }
  return json.message || json.title || fallback;
}

function validateJobPayload(payload) {
    if (!payload.title || payload.title.length < 5) return 'Tieu de bai dang phai tu 5 ky tu tro len.';
    if (payload.title.length > 200) return 'Tieu de bai dang khong duoc vuot qua 200 ky tu.';
    if (!payload.description || payload.description.length < 10) return 'Mo ta chi tiet phai tu 10 ky tu tro len.';
    if (payload.description.length > 3000) return 'Mo ta chi tiet khong duoc vuot qua 3000 ky tu.';
    if (!payload.jobType || payload.jobType < 1 || payload.jobType > 3) return 'Loai cong viec khong hop le.';
    if (!payload.numberOfChildren || payload.numberOfChildren < 1 || payload.numberOfChildren > 10) return 'So tre can cham phai tu 1 den 10.';
    if (!payload.childProfileId) return 'Vui long chon tre tu Child Profile.';
    if (!payload.location || payload.location.length < 3) return 'Vui long nhap dia chi chi tiet.';
    if (!payload.city) return 'Vui long chon thanh pho.';
    if (payload.city && !provinces.some(province => province.name.toLowerCase() === payload.city.toLowerCase())) {
      return 'Thanh pho khong nam trong danh sach goi y.';
    }
    const province = provinces.find(item => item.name.toLowerCase() === payload.city.toLowerCase());
  if (!payload.district) return 'Vui long chon quan/huyen.';
  if (province && !province.districts.some(district => district.name.toLowerCase() === payload.district.toLowerCase())) {
    return 'Quan/huyen khong thuoc thanh pho da chon.';
  }
  if (!payload.salaryNegotiable && (payload.salaryMin === null || Number(payload.salaryMin) < 0)) {
    return 'Vui long nhap luong toi thieu hoac bat luong thuong luong.';
  }
  if (!payload.salaryNegotiable && Number(payload.salaryMin) === 0) return 'Luong toi thieu phai lon hon 0 hoac chon luong thuong luong.';
  if (payload.salaryMin !== null && Number(payload.salaryMin) > 1000000000) return 'Luong toi thieu khong hop le.';
  if (payload.salaryMax !== null && Number(payload.salaryMax) > 1000000000) return 'Luong toi da khong hop le.';
  if (payload.salaryMin !== null && payload.salaryMax !== null && Number(payload.salaryMin) > Number(payload.salaryMax)) {
    return 'Luong toi thieu khong duoc lon hon luong toi da.';
  }
  if (payload.minNannyAge !== null && (payload.minNannyAge < 18 || payload.minNannyAge > 80)) return 'Do tuoi bao mau tu phai trong khoang 18 den 80.';
  if (payload.maxNannyAge !== null && (payload.maxNannyAge < 18 || payload.maxNannyAge > 80)) return 'Do tuoi bao mau den phai trong khoang 18 den 80.';
    if (payload.minNannyAge !== null && payload.maxNannyAge !== null && payload.minNannyAge > payload.maxNannyAge) {
      return 'Do tuoi bao mau tu khong duoc lon hon do tuoi bao mau den.';
    }
    if (![1, 2].includes(Number(payload.status))) return 'Trang thai bai dang khong hop le.';
    if (!Array.isArray(payload.skills) || payload.skills.length === 0) return 'Vui long chon it nhat 1 ky nang yeu cau.';
    if (!Array.isArray(payload.scheduleSlots) || payload.scheduleSlots.length === 0) return 'Vui long chon it nhat 1 khung lich muon tuyen.';
    return '';
  }

function getCreatePayload() {
  return {
    title: document.getElementById('cf-title')?.value.trim() || '',
    description: document.getElementById('cf-desc')?.value.trim() || '',
    jobType: Number(document.getElementById('cf-type')?.value || 0),
    numberOfChildren: Number(document.getElementById('cf-children')?.value || 0),
    childProfileId: document.getElementById('cf-childProfileId')?.value || null,
    salaryMin: readOptionalNumber('cf-salMin'),
    salaryMax: readOptionalNumber('cf-salMax'),
    salaryNegotiable: !!document.getElementById('cf-negotiable')?.checked,
    location: document.getElementById('cf-location')?.value.trim() || '',
    city: document.getElementById('cf-city')?.value.trim() || '',
    district: document.getElementById('cf-district')?.value.trim() || '',
    minNannyAge: readOptionalNumber('cf-minAge'),
    maxNannyAge: readOptionalNumber('cf-maxAge'),
    skills: createSkills,
    scheduleSlots: createSchedule,
    status: Number(document.getElementById('cf-status')?.value || 1)
  };
}

function ensureCreateValidationBox() {
  const form = document.getElementById('createForm');
  if (!form) return null;

  let box = document.getElementById('createValidationBox');
  if (box) return box;

  box = document.createElement('div');
  box.id = 'createValidationBox';
  box.className = 'create-validation-box hidden';
  const firstSection = form.querySelector('.section-card');
  if (firstSection) firstSection.prepend(box);
  else form.prepend(box);
  return box;
}

function setFieldInvalid(id, invalid) {
  const element = document.getElementById(id);
  if (!element) return;
  element.classList.toggle('is-invalid', invalid);
}

function touchCreateFields(...ids) {
  ids.filter(Boolean).forEach(id => createTouchedFields.add(id));
}

function updateCreateValidationUI(forceHide = false) {
  const payload = getCreatePayload();
  const error = validateJobPayload(payload);
  const box = ensureCreateValidationBox();
  const submitBtn = document.querySelector('#createModal .modal-btn-primary');
  const province = provinces.find(item => item.name.toLowerCase() === payload.city.toLowerCase());

  const invalidMap = {
    'cf-title': !payload.title || payload.title.length < 5 || payload.title.length > 200,
    'cf-desc': !payload.description || payload.description.length < 10 || payload.description.length > 3000,
    'cf-type': !payload.jobType || payload.jobType < 1 || payload.jobType > 3,
    'cf-children': !payload.numberOfChildren || payload.numberOfChildren < 1 || payload.numberOfChildren > 10,
    'cf-childProfileId': !payload.childProfileId,
    'cf-location': !payload.location || payload.location.length < 3,
    'cf-city': !payload.city || !provinces.some(item => item.name.toLowerCase() === payload.city.toLowerCase()),
    'cf-district': !payload.district || !(province?.districts || []).some(item => item.name.toLowerCase() === payload.district.toLowerCase()),
    'cf-salMin': !payload.salaryNegotiable && (payload.salaryMin === null || Number(payload.salaryMin) <= 0 || Number(payload.salaryMin) > 1000000000),
    'cf-salMax': payload.salaryMax !== null && (Number(payload.salaryMax) > 1000000000 || (payload.salaryMin !== null && Number(payload.salaryMin) > Number(payload.salaryMax))),
    'cf-minAge': payload.minNannyAge !== null && (payload.minNannyAge < 18 || payload.minNannyAge > 80 || (payload.maxNannyAge !== null && payload.minNannyAge > payload.maxNannyAge)),
    'cf-maxAge': payload.maxNannyAge !== null && (payload.maxNannyAge < 18 || payload.maxNannyAge > 80 || (payload.minNannyAge !== null && payload.minNannyAge > payload.maxNannyAge)),
    'cf-skillSelect': !Array.isArray(payload.skills) || payload.skills.length === 0,
    'cf-schedule': !Array.isArray(payload.scheduleSlots) || payload.scheduleSlots.length === 0
  };

  const fieldMessages = {
    'cf-title': 'Tieu de bai dang phai tu 5 ky tu tro len.',
    'cf-desc': 'Mo ta chi tiet phai tu 10 ky tu tro len.',
    'cf-type': 'Loai cong viec khong hop le.',
    'cf-children': 'So tre can cham phai tu 1 den 10.',
    'cf-childProfileId': 'Vui long chon tre tu Child Profile.',
    'cf-location': 'Dia chi chi tiet phai tu 3 ky tu tro len.',
    'cf-city': 'Vui long chon thanh pho hop le.',
    'cf-district': 'Vui long chon quan/huyen hop le.',
    'cf-salMin': 'Luong toi thieu phai lon hon 0.',
    'cf-salMax': 'Luong toi da phai lon hon hoac bang luong toi thieu.',
    'cf-minAge': 'Do tuoi bao mau phai nam trong khoang 18 den 80.',
    'cf-maxAge': 'Do tuoi bao mau phai nam trong khoang 18 den 80.',
    'cf-skillSelect': 'Vui long chon it nhat 1 ky nang.',
    'cf-schedule': 'Vui long chon it nhat 1 khung lich.'
  };

  const touchedError = Object.entries(fieldMessages).find(([id]) =>
    createTouchedFields.has(id) && invalidMap[id]
  )?.[1] || '';

  Object.entries(invalidMap).forEach(([id, invalid]) => {
    setFieldInvalid(id, Boolean(invalid) && (createValidationTouched || createTouchedFields.has(id)));
  });

  if (box) {
    if (forceHide || (!createValidationTouched && !touchedError)) {
      box.classList.add('hidden');
      box.textContent = '';
    } else if (createValidationTouched && error) {
      box.classList.remove('hidden');
      box.textContent = error;
    } else if (touchedError) {
      box.classList.remove('hidden');
      box.textContent = touchedError;
    } else {
      box.classList.add('hidden');
      box.textContent = '';
    }
  }

  if (submitBtn) {
    const disableSubmit = Boolean(error) || isSubmittingCreate;
    submitBtn.disabled = disableSubmit;
    submitBtn.classList.toggle('is-disabled', disableSubmit);
    submitBtn.textContent = isSubmittingCreate ? 'Dang gui...' : 'Dang bai';
  }
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

function escapeJs(value) {
  return String(value).replaceAll('\\', '\\\\').replaceAll("'", "\\'");
}

document.addEventListener('DOMContentLoaded', () => {
  document.querySelector('button[onclick="openHistory()"]')?.remove();
  document.getElementById('historyModal')?.remove();
  ['cf-children', 'ef-children'].forEach(id => {
    const input = document.getElementById(id);
    if (!input) return;
    input.removeAttribute('readonly');
    input.classList.remove('bg-gray-50');
    input.setAttribute('step', '1');
  });
  setupAutocompleteShell('cf-city', 'cf-cityOptions');
  setupAutocompleteShell('cf-district', 'cf-districtOptions');
  setupAutocompleteShell('ef-city', 'ef-cityOptions');
  setupAutocompleteShell('ef-district', 'ef-districtOptions');
  initMap();
  loadProvinceOptions();
  loadSkillOptions();
  const createSkillSelect = document.getElementById('cf-skillSelect');
  const editSkillSelect = document.getElementById('ef-skillSelect');
  if (createSkillSelect) createSkillSelect.value = '';
  if (editSkillSelect) editSkillSelect.value = '';
  doSearch();
  renderScheduleGrid('cf-schedule', createSchedule, 'toggleCreateSchedule');
  renderScheduleGrid('ef-schedule', editSchedule, 'toggleEditSchedule');
  [
    'cf-title',
    'cf-desc',
    'cf-type',
    'cf-children',
    'cf-salMin',
    'cf-salMax',
    'cf-location',
    'cf-city',
    'cf-district',
    'cf-minAge',
    'cf-maxAge',
    'cf-childProfileId',
    'cf-negotiable'
  ].forEach(id => {
    const element = document.getElementById(id);
    if (!element) return;
    const inputEvent = element.tagName === 'SELECT' || element.type === 'checkbox' ? 'change' : 'input';
    element.addEventListener(inputEvent, () => {
      touchCreateFields(id);
      updateCreateValidationUI();
    });
    if (inputEvent !== 'change') {
      element.addEventListener('change', () => {
        touchCreateFields(id);
        updateCreateValidationUI();
      });
    }
  });

  ['cf-city', 'ef-city'].forEach(id => {
    const input = document.getElementById(id);
    if (!input) return;
    input.addEventListener('focus', () => {
      renderAutocompleteOptions(id, `${id}Options`, provinces.map(province => province.name), value => {
        input.value = value;
        const districtInputId = id === 'cf-city' ? 'cf-district' : 'ef-district';
        document.getElementById(districtInputId).value = '';
        populateDistrictSelect(id === 'cf-city' ? 'cf-districtOptions' : 'ef-districtOptions', value);
      });
    });
  });

  ['cf-district', 'ef-district'].forEach(id => {
    const input = document.getElementById(id);
    if (!input) return;
    input.addEventListener('focus', () => {
      if (id === 'cf-district') handleCreateDistrictInput();
      else handleEditDistrictInput();
    });
  });

  document.addEventListener('click', event => {
    if (!event.target.closest('.autocomplete-field')) {
      document.querySelectorAll('.ac-dropdown').forEach(dropdown => {
        dropdown.classList.remove('show');
        dropdown.innerHTML = '';
      });
    }
  });
  updateCreateValidationUI(true);
});
