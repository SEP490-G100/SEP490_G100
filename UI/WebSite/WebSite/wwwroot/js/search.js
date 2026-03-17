// search.js — NannyMatch Search Page
// Biến IS_AUTH, API_URL, GRADS, JOB_TYPES được khai báo inline trong Index.cshtml

// ─── State ─────────────────────────────────────────────────
const filters = { type: null, salary: null, district: null };
let map, markers = [], markerPositions = [], debounceTimer;
let currentJobs = [];

const REGION_STYLE = {
  color: '#f97316',
  weight: 2,
  fillColor: '#fb923c',
  fillOpacity: 0.14
};

const REGION_HOVER_STYLE = {
  color: '#ea580c',
  weight: 3,
  fillColor: '#f97316',
  fillOpacity: 0.22
};

// ─── Toạ độ trung tâm Quận/Huyện (Fallback) ────────────────
const GEO_FALLBACK = {
  'Thành phố Hồ Chí Minh': { lat: 10.776, lng: 106.701 },
  'Quận 1': { lat: 10.775, lng: 106.701 },
  'Quận 2': { lat: 10.787, lng: 106.740 },
  'Quận 3': { lat: 10.783, lng: 106.685 },
  'Quận 4': { lat: 10.758, lng: 106.701 },
  'Quận 5': { lat: 10.753, lng: 106.666 },
  'Quận 7': { lat: 10.733, lng: 106.721 },
  'Bình Thạnh': { lat: 10.810, lng: 106.709 },
  'Gò Vấp': { lat: 10.835, lng: 106.666 },
  'Tân Bình': { lat: 10.801, lng: 106.652 },
  'Thủ Đức': { lat: 10.849, lng: 106.753 },
  'Hà Nội': { lat: 21.028, lng: 105.854 },
  'Ba Đình': { lat: 21.034, lng: 105.823 },
  'Hoàn Kiếm': { lat: 21.028, lng: 105.852 },
  'Cầu Giấy': { lat: 21.030, lng: 105.795 },
  'Đống Đa': { lat: 21.014, lng: 105.826 },
  'Tây Hồ': { lat: 21.066, lng: 105.821 },
  'Đà Nẵng': { lat: 16.054, lng: 108.202 },
  'Hải Châu': { lat: 16.055, lng: 108.220 },
  'Hải Phòng': { lat: 20.844, lng: 106.688 },
  'Cần Thơ': { lat: 10.045, lng: 105.746 }
};

// ─── Map init ───────────────────────────────────────────────
function initMap() {
  map = L.map('map').setView([10.776, 106.7], 11);
  L.tileLayer('https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png', {
    attribution: '&copy; CartoDB', maxZoom: 19
  }).addTo(map);
}

// ─── Skeleton ──────────────────────────────────────────────
function showSkeleton() {
  document.getElementById('skeletons').innerHTML = [1, 2, 3, 4].map(() => `
    <div class="flex gap-3 p-4 border-b border-gray-100">
      <div class="sk w-14 h-14 rounded-2xl flex-shrink-0"></div>
      <div class="flex-1 flex flex-col gap-2 pt-1">
        <div class="sk h-3 rounded" style="width:55%"></div>
        <div class="sk h-3 rounded" style="width:35%"></div>
        <div class="sk h-3 rounded" style="width:85%"></div>
        <div class="sk h-3 rounded" style="width:60%"></div>
      </div>
    </div>`).join('');
  document.getElementById('jobList').innerHTML = '';
}

// ─── Fetch ─────────────────────────────────────────────────
async function doSearch() {
  showSkeleton(); updateResetBtn();
  const city = document.getElementById('searchCity').value.trim();
  const p = new URLSearchParams({ page: 1, pageSize: 20 });
  if (city) p.append('city', city);
  if (filters.district) p.append('district', filters.district);
  if (filters.type) p.append('jobType', filters.type);
  if (filters.salary) p.append('salaryMin', filters.salary);
  try {
    const res = await fetch(`${API_URL}?${p}`);
    const json = await res.json();
    currentJobs = json.data || [];
    renderJobs(currentJobs);
  } catch { renderJobs([]); }
}

// ─── Render ────────────────────────────────────────────────
function renderJobs(jobs) {
  document.getElementById('skeletons').innerHTML = '';
  document.getElementById('resultCount').textContent = jobs.length ? `${jobs.length} tin đăng` : '0 kết quả';
  markers.forEach(m => {
    if (m.circle) m.circle.remove();
    if (m.center) m.center.remove();
  });
  markers = [];
  markerPositions = [];

  if (!jobs.length) {
    document.getElementById('jobList').innerHTML = `
      <div class="text-center px-8 py-16">
        <div class="text-5xl mb-4">🔍</div>
        <h3 class="text-base font-bold text-gray-800 mb-2">Không tìm thấy tin đăng</h3>
        <p class="text-sm text-gray-500">Thử thay đổi bộ lọc hoặc tìm ở khu vực khác.</p>
      </div>`; return;
  }

  document.getElementById('jobList').innerHTML = jobs.map((j, i) => cardHTML(j, i)).join('');

  jobs.forEach((j, i) => {
    const region = getRegionMeta(j);
    markerPositions.push(region.center);

    const circle = L.circle(region.center, {
      ...REGION_STYLE,
      radius: region.radius
    }).addTo(map);

    const center = L.circleMarker(region.center, {
      radius: 6,
      color: '#ffffff',
      weight: 2,
      fillColor: '#f97316',
      fillOpacity: 1
    }).addTo(map);

    const popupHtml = `
      <b style="font-size:.82rem;font-family:'Quicksand',sans-serif">${j.title}</b><br>
      <span style="font-size:.72rem;color:#9ca3af">${region.label}</span><br>
      <span style="font-size:.72rem;color:#f97316">Khu vực hiển thị gần đúng</span>`;

    circle.bindPopup(popupHtml);
    center.bindPopup(popupHtml);

    circle.on('click', () => highlightCard(i));
    center.on('click', () => highlightCard(i));
    markers.push({ circle, center, region });
  });
  if (markers.length) {
    map.fitBounds(L.featureGroup(markers.map(m => m.circle)).getBounds().pad(.18));
  }

  // Card click & hover → Zoom Map, focus region & Mở Preview
  document.querySelectorAll('.job-card').forEach(c => {
    c.addEventListener('click', e => {
      if (e.target.closest('.edit-btn') || e.target.closest('.fav-btn') || e.target.closest('.profile-btn')) return;
      const i = +c.dataset.idx;
      highlightCard(i);
      
      if (markers[i] && markerPositions[i]) {
        const region = markers[i].region;
        map.flyTo(markerPositions[i], region.zoom, { animate: true, duration: 0.8 });
        setTimeout(() => markers[i].circle.openPopup(), 850);
      }
      
      openPreview(currentJobs[i]); 
    });

    // Hover card -> vùng được nhấn mạnh thay vì nháy điểm địa chỉ
    c.addEventListener('mouseenter', () => {
      const i = +c.dataset.idx;
      if (markers[i]) {
          markers[i].circle.setStyle(REGION_HOVER_STYLE);
          markers[i].center.setStyle({ radius: 7.5, fillColor: '#ea580c' });
          markers[i].circle.bringToFront();
          markers[i].center.bringToFront();
      }
    });

    c.addEventListener('mouseleave', () => {
        const i = +c.dataset.idx;
        if (markers[i]) {
             markers[i].circle.setStyle(REGION_STYLE);
             markers[i].center.setStyle({ radius: 6, fillColor: '#f97316' });
        }
    });
  });

  document.querySelectorAll('.fav-btn').forEach(b => {
    b.addEventListener('click', e => {
      e.stopPropagation();
      b.classList.toggle('on');
      b.querySelector('span').textContent = b.classList.contains('on') ? 'favorite' : 'favorite_border';
      showToast(b.classList.contains('on') ? '❤️ Đã lưu yêu thích!' : 'Đã bỏ lưu');
    });
  });

  document.querySelectorAll('.edit-btn').forEach(btn => {
    btn.addEventListener('click', e => {
      e.stopPropagation();
      openEdit(currentJobs[+btn.dataset.idx]);
    });
  });
}

function highlightCard(i) {
  document.querySelectorAll('.job-card').forEach(c => c.classList.remove('active'));
  const c = document.querySelector(`.job-card[data-idx="${i}"]`);
  if (c) { c.classList.add('active'); c.scrollIntoView({ block: 'nearest', behavior: 'smooth' }); }
}

function getRegionMeta(job) {
  const districtKey = job.district && GEO_FALLBACK[job.district] ? job.district : null;
  const cityKey = job.city && GEO_FALLBACK[job.city] ? job.city : null;

  if (districtKey) {
    return {
      center: [GEO_FALLBACK[districtKey].lat, GEO_FALLBACK[districtKey].lng],
      radius: 2200,
      zoom: 13,
      label: `${districtKey}${job.city ? `, ${job.city}` : ''}`
    };
  }

  if (cityKey) {
    return {
      center: [GEO_FALLBACK[cityKey].lat, GEO_FALLBACK[cityKey].lng],
      radius: 6500,
      zoom: 11,
      label: cityKey
    };
  }

  if (job.latitude && job.longitude) {
    return {
      center: [job.latitude, job.longitude],
      radius: 3000,
      zoom: 12,
      label: [job.district, job.city].filter(Boolean).join(', ') || 'Khu vực gần đúng'
    };
  }

  return {
    center: [10.776, 106.701],
    radius: 8000,
    zoom: 11,
    label: [job.district, job.city].filter(Boolean).join(', ') || 'Khu vực gần đúng'
  };
}

function cardHTML(j, i) {
  const g = GRADS[i % GRADS.length];
  const parentName = j.parentName || 'Người dùng ẩn';
  const parentProfileId = j.parentProfileId;
  const init = parentName.split(' ').map(w => w[0]).join('').slice(0, 2).toUpperCase();
  const type = JOB_TYPES[j.jobType] || 'Toàn thời gian';
  const sal = j.salaryNegotiable ? 'Thương lượng' : j.salaryMin ? `${(j.salaryMin / 1000).toFixed(0)}k/giờ` : '—';
  const loc = [j.location, j.district, j.city].filter(Boolean).join(', ') || 'Chưa xác định';
  const editBtn = IS_AUTH && j.isOwner ? `
    <button class="edit-btn flex items-center gap-1 px-2 py-1 rounded-lg bg-gray-50 hover:bg-orange-50 border border-gray-200 hover:border-orange-300 text-gray-400 hover:text-orange-600 text-xs font-bold transition-all" data-idx="${i}" title="Chỉnh sửa">
      <span class="material-icons-round text-sm">edit</span>Sửa
    </button>` : '';

  const viewProfileBtn = parentProfileId ? `
    <a href="/ParentProfile/Detail/${parentProfileId}" target="_blank" class="profile-btn inline-flex items-center gap-1 mt-2.5 text-[13px] font-bold text-blue-600 hover:text-blue-800 transition-colors bg-blue-50 hover:bg-blue-100 px-3 py-1.5 rounded-lg w-fit" onclick="event.stopPropagation()">
      <span class="material-icons-round text-[16px]">person</span>Xem chi tiết hồ sơ
    </a>` : '';

  return `
    <div class="job-card bg-white border-b border-gray-100 hover:bg-gray-50 p-4 transition-colors cursor-pointer flex gap-3" data-idx="${i}">
      <div class="w-12 h-12 rounded-full flex-shrink-0 flex items-center justify-center text-white font-bold text-lg shadow-sm mt-1" style="background:${g}" title="${parentName}">${init}</div>
      <div class="flex-1 min-w-0 flex flex-col">
        <div class="flex items-start justify-between gap-2">
          <div class="min-w-0 pr-2">
            <h3 class="text-[15px] font-bold text-gray-900 truncate tracking-tight">${j.title}</h3>
            <p class="text-xs text-gray-500 font-medium tracking-wide mt-0.5 truncate">${parentName}</p>
          </div>
          <div class="flex items-center gap-1.5 flex-shrink-0">
            ${editBtn}
            <button class="fav-btn text-gray-400 hover:text-red-500 transition-colors"><span class="material-icons-round text-xl leading-none">favorite_border</span></button>
          </div>
        </div>
        <p class="text-xs font-semibold text-orange-500 mt-1.5 flex items-center gap-1 opacity-90">
          <span class="material-icons-round text-[14px]">location_on</span>${loc}
        </p>
        <p class="text-xs text-gray-600 leading-relaxed mt-2 line-clamp-2">${j.description || 'Không có mô tả chi tiết.'}</p>
        
        <div class="flex items-center justify-between mt-3 mb-1">
          <div class="flex items-center gap-1.5 flex-wrap">
            <span class="px-2 py-0.5 rounded-md bg-orange-50 text-orange-700 text-[11px] font-bold border border-orange-100">${type}</span>
            ${j.numberOfChildren ? `<span class="px-2 py-0.5 rounded-md bg-blue-50 text-blue-700 text-[11px] font-bold border border-blue-100">${j.numberOfChildren} bé</span>` : ''}
          </div>
          <span class="text-sm font-black text-orange-600 ml-auto whitespace-nowrap">${sal}</span>
        </div>
        ${viewProfileBtn}
      </div>
    </div>`;
}

// ─── Filters ───────────────────────────────────────────────
function toggleDrop(id, chipId) {
  document.querySelectorAll('.fdrop').forEach(d => { if (d.id !== id) d.classList.add('hidden'); });
  const d = document.getElementById(id);
  d.classList.toggle('hidden');
  if (!d.classList.contains('hidden')) {
    const r = document.getElementById(chipId).getBoundingClientRect();
    d.style.top = (r.bottom + 6) + 'px'; d.style.left = r.left + 'px';
  }
}
function pickOpt(el, key) {
  el.closest('.fdrop').querySelectorAll('.fd-opt').forEach(o => o.classList.remove('chosen'));
  el.classList.add('chosen'); filters[key] = el.dataset.val;
}
function applyDrop(id) { document.getElementById(id).classList.add('hidden'); updateChips(); doSearch(); }
function clearFilter(key, dropId, chipId) {
  filters[key] = null;
  document.getElementById(dropId).querySelectorAll('.fd-opt').forEach(o => o.classList.remove('chosen'));
  document.getElementById(chipId).classList.remove('on');
  document.getElementById(dropId).classList.add('hidden');
  updateResetBtn(); doSearch();
}
function updateChips() {
  const m = { type: 'chipType', salary: 'chipSalary', district: 'chipDistrict' };
  Object.entries(m).forEach(([k, id]) => document.getElementById(id).classList.toggle('on', !!filters[k]));
  updateResetBtn();
}
function updateResetBtn() {
  const any = Object.values(filters).some(Boolean) || document.getElementById('searchCity').value.trim();
  document.getElementById('chipReset').style.display = any ? 'flex' : 'none';
}
function resetAll() {
  filters.type = filters.salary = filters.district = null;
  document.getElementById('searchCity').value = '';
  document.querySelectorAll('.fd-opt').forEach(o => o.classList.remove('chosen'));
  ['chipType', 'chipSalary', 'chipDistrict'].forEach(id => document.getElementById(id).classList.remove('on'));
  updateResetBtn(); doSearch();
}
document.addEventListener('click', e => {
  if (!e.target.closest('.fdrop') && !e.target.closest('.chip'))
    document.querySelectorAll('.fdrop').forEach(d => d.classList.add('hidden'));
});
function debounce() { clearTimeout(debounceTimer); debounceTimer = setTimeout(doSearch, 500); }

// ─── CREATE modal ───────────────────────────────────────────
const cfSkills = [];
function openCreate() {
  document.getElementById('createForm').reset();
  cfSkills.length = 0;
  document.getElementById('cf-skills').innerHTML = '';
  // Reset visibility toggle về ON (public)
  const vis = document.getElementById('cf-visibility');
  vis.classList.add('on'); vis.classList.remove('off');
  document.getElementById('createModal').classList.add('show');
}
function closeCreate() { document.getElementById('createModal').classList.remove('show'); }

function addSkill() {
  const inp = document.getElementById('cf-skillInput');
  const v = inp.value.trim();
  if (!v || cfSkills.includes(v)) return;
  cfSkills.push(v);
  const tag = document.createElement('span');
  tag.className = 'skill-tag';
  tag.innerHTML = `${v}<button onclick="removeSkill('${v}',this.parentElement)">×</button>`;
  document.getElementById('cf-skills').appendChild(tag);
  inp.value = '';
}
function removeSkill(val, el) { cfSkills.splice(cfSkills.indexOf(val), 1); el.remove(); }

async function submitCreate() {
  const form = document.getElementById('createForm');
  if (!form.checkValidity()) { form.reportValidity(); return; }
  const title = document.getElementById('cf-title').value.trim();
  const description = document.getElementById('cf-desc').value.trim();
  const location = document.getElementById('cf-location').value.trim();
  const city = document.getElementById('cf-city').value.trim();
  const district = document.getElementById('cf-district').value.trim();
  const jobType = Number(document.getElementById('cf-type').value);
  const numberOfChildren = Number(document.getElementById('cf-children').value || 1);
  const salaryMinRaw = document.getElementById('cf-salMin').value.trim();
  const salaryMin = salaryMinRaw ? Number(salaryMinRaw) : null;
  if (title.length < 5) return showToast('❌ Tiêu đề phải từ 5 ký tự trở lên');
  if (description.length < 10) return showToast('❌ Mô tả phải từ 10 ký tự trở lên');
  if (location.length > 300) return showToast('❌ Địa chỉ chi tiết tối đa 300 ký tự');
  if (city.length > 100) return showToast('❌ Thành phố tối đa 100 ký tự');
  if (district.length > 100) return showToast('❌ Quận/Huyện tối đa 100 ký tự');
  if (!Number.isInteger(jobType) || jobType < 1 || jobType > 3) return showToast('❌ Vui lòng chọn loại công việc hợp lệ');
  if (!Number.isInteger(numberOfChildren) || numberOfChildren < 1 || numberOfChildren > 10) return showToast('❌ Số trẻ cần chăm phải từ 1 đến 10');
  if (salaryMin !== null && Number.isNaN(salaryMin)) return showToast('❌ Mức lương tối thiểu không hợp lệ');
  const cfVis = document.getElementById('cf-visibility').classList.contains('on') ? 1 : 0;
  const data = {
    title,
    description,
    jobType,
    numberOfChildren,
    salaryMin,
    salaryNegotiable: document.getElementById('cf-negotiable').checked,
    location,
    city,
    district,
    status: cfVis,
  };
  try {
    const res = await fetch('/Search/CreateJob', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
    const json = await res.json();
    if (json.success) { closeCreate(); showToast('✅ Đã đăng bài thành công!'); doSearch(); }
    else showToast('❌ ' + getErrorMessage(json, 'Đăng bài thất bại'));
  } catch { showToast('❌ Lỗi kết nối server'); }
}

// ─── EDIT modal ────────────────────────────────────────────
let editingJobId = null;
function openEdit(job) {
  editingJobId = job.id;
  document.getElementById('ef-id').value = job.id;
  document.getElementById('ef-title').value = job.title || '';
  document.getElementById('ef-desc').value = job.description || '';
  document.getElementById('ef-type').value = job.jobType || 1;
  document.getElementById('ef-children').value = job.numberOfChildren || 1;
  document.getElementById('ef-salMin').value = job.salaryMin || '';
  document.getElementById('ef-negotiable').checked = job.salaryNegotiable || false;
  document.getElementById('ef-location').value = job.location || '';
  document.getElementById('ef-city').value = job.city || '';
  document.getElementById('ef-district').value = job.district || '';
  const efVis = document.getElementById('ef-visibility');
  if (Number(job.status) === 0) { efVis.classList.remove('on'); efVis.classList.add('off'); }
  else { efVis.classList.add('on'); efVis.classList.remove('off'); }
  document.getElementById('editModal').classList.add('show');
}
function closeEdit() { document.getElementById('editModal').classList.remove('show'); }

async function submitEdit() {
  if (!editingJobId) return;
  const title = document.getElementById('ef-title').value.trim();
  const description = document.getElementById('ef-desc').value.trim();
  const location = document.getElementById('ef-location').value.trim();
  const city = document.getElementById('ef-city').value.trim();
  const district = document.getElementById('ef-district').value.trim();
  const jobType = Number(document.getElementById('ef-type').value);
  const numberOfChildren = Number(document.getElementById('ef-children').value || 1);
  const salaryMinRaw = document.getElementById('ef-salMin').value.trim();
  const salaryMin = salaryMinRaw ? Number(salaryMinRaw) : null;
  if (title.length < 5) return showToast('❌ Tiêu đề phải từ 5 ký tự trở lên');
  if (description.length < 10) return showToast('❌ Mô tả phải từ 10 ký tự trở lên');
  if (location.length > 300) return showToast('❌ Địa chỉ chi tiết tối đa 300 ký tự');
  if (city.length > 100) return showToast('❌ Thành phố tối đa 100 ký tự');
  if (district.length > 100) return showToast('❌ Quận/Huyện tối đa 100 ký tự');
  if (!Number.isInteger(jobType) || jobType < 1 || jobType > 3) return showToast('❌ Vui lòng chọn loại công việc hợp lệ');
  if (!Number.isInteger(numberOfChildren) || numberOfChildren < 1 || numberOfChildren > 10) return showToast('❌ Số trẻ cần chăm phải từ 1 đến 10');
  if (salaryMin !== null && Number.isNaN(salaryMin)) return showToast('❌ Mức lương tối thiểu không hợp lệ');
  const efVis = document.getElementById('ef-visibility').classList.contains('on') ? 1 : 0;
  const data = {
    title,
    description,
    jobType,
    numberOfChildren,
    salaryMin,
    salaryNegotiable: document.getElementById('ef-negotiable').checked,
    location,
    city,
    district,
    status: efVis,
  };
  try {
    const res = await fetch(`/Search/UpdateJob/${editingJobId}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
    const json = await res.json();
    if (json.success) { closeEdit(); showToast('✅ Đã cập nhật bài đăng!'); doSearch(); }
    else showToast('❌ ' + getErrorMessage(json, 'Cập nhật thất bại'));
  } catch { showToast('❌ Lỗi kết nối server'); }
}

async function deleteJob() {
  if (!editingJobId || !confirm('Bạn có chắc muốn xoá bài đăng này?')) return;
  try {
    const res = await fetch(`/Search/DeleteJob/${editingJobId}`, { method: 'DELETE' });
    const json = await res.json();
    if (json.success) { closeEdit(); showToast('🗑 Đã xoá bài đăng'); doSearch(); }
    else showToast('❌ ' + getErrorMessage(json, 'Xoá thất bại'));
  } catch { showToast('❌ Lỗi kết nối server'); }
}

// ─── PREVIEW modal (Xem chi tiết & Hồ sơ người đăng) ───────
function openPreview(job) {
    const parentName = job.parentName || 'Phụ huynh chưa cập nhật tên';
    const location = [job.location, job.district, job.city].filter(Boolean).join(', ') || 'Chưa xác định';
    const salary = job.salaryNegotiable ? 'Thương lượng' : job.salaryMin ? `${(job.salaryMin / 1000).toFixed(0)}k/giờ` : '—';
    const status = Number(job.status) === 0 ? 'Riêng tư' : 'Công khai';
    const kids = job.numberOfChildren ? `${job.numberOfChildren} bé` : 'Chưa xác định';
    const coords = (job.latitude && job.longitude)
      ? `${Number(job.latitude).toFixed(5)}, ${Number(job.longitude).toFixed(5)}`
      : 'Chưa có tọa độ';
    const distance = typeof job.distanceKm === 'number'
      ? `${job.distanceKm.toFixed(1)} km`
      : 'Chưa xác định';

    document.getElementById('pv-title').textContent = job.title || 'Tin đăng tìm bảo mẫu';
    document.getElementById('pv-parentName').textContent = `Đăng bởi ${parentName}`;
    document.getElementById('pv-type').textContent = JOB_TYPES[job.jobType] || 'Toàn thời gian';
    document.getElementById('pv-sal').textContent = salary;
    document.getElementById('pv-status').textContent = status;
    document.getElementById('pv-loc').textContent = location;
    document.getElementById('pv-kids').textContent = kids;
    document.getElementById('pv-coords').textContent = coords;
    document.getElementById('pv-distance').textContent = distance;
    document.getElementById('pv-desc').textContent = job.description || 'Không có mô tả chi tiết.';

    const btnParent = document.getElementById('pv-parentBtn');
    const btnParentText = document.getElementById('pv-parentBtnText');
    if (job.parentProfileId) {
      btnParent.href = `/ParentProfile/Detail/${job.parentProfileId}`;
      btnParent.style.display = 'inline-flex';
      btnParentText.textContent = `View Detail Profile Người Đăng - ${parentName}`;
      btnParent.setAttribute('aria-label', `View detail profile của ${parentName}`);
    } else {
      btnParent.removeAttribute('href');
      btnParent.style.display = 'none';
      btnParentText.textContent = 'View Detail Profile Người Đăng';
    }

    document.getElementById('previewModal').classList.add('show');
}
function closePreview() { document.getElementById('previewModal').classList.remove('show'); }

// ─── Visibility Toggle ─────────────────────────────────────
function toggleVisibility(id) {
  const btn = document.getElementById(id);
  btn.classList.toggle('on'); btn.classList.toggle('off');
  showToast(btn.classList.contains('on') ? '🌍 Bài đăng công khai — tất cả xem được' : '🔒 Chỉ Nanny xem được');
}

// ─── Premium ───────────────────────────────────────────────
function openPremium() { window.location.href = '/Subscription'; }
function closePremium() { document.getElementById('premiumModal').classList.remove('show'); }
function selectPlan(el) { document.querySelectorAll('.plan-card').forEach(c => c.classList.remove('selected')); el.classList.add('selected'); }

// ─── Toast ─────────────────────────────────────────────────
function showToast(msg) {
  const t = document.getElementById('toast');
  t.textContent = msg; t.classList.add('show');
  setTimeout(() => t.classList.remove('show'), 2800);
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
  if (json.message) return json.message;
  if (json.title) return json.title;
  if (typeof json.raw === 'string' && json.raw) return json.raw;
  return fallback;
}

// ─── Autocomplete: 63 Tỉnh/Thành VN ──────────────────────
const VN_PROVINCES = [
  'An Giang','Bà Rịa – Vũng Tàu','Bắc Giang','Bắc Kạn','Bạc Liêu','Bắc Ninh',
  'Bến Tre','Bình Định','Bình Dương','Bình Phước','Bình Thuận','Cà Mau',
  'Cần Thơ','Cao Bằng','Đắk Lắk','Đắk Nông','Điện Biên','Đồng Nai','Đồng Tháp',
  'Gia Lai','Hà Giang','Hà Nam','Hà Nội','Hà Tĩnh','Hải Dương','Hải Phòng',
  'Hậu Giang','Hòa Bình','Hưng Yên','Khánh Hòa','Kiên Giang','Kon Tum',
  'Lai Châu','Lâm Đồng','Lạng Sơn','Lào Cai','Long An','Nam Định',
  'Nghệ An','Ninh Bình','Ninh Thuận','Phú Thọ','Phú Yên','Quảng Bình',
  'Quảng Nam','Quảng Ngãi','Quảng Ninh','Quảng Trị','Sóc Trăng','Sơn La',
  'Tây Ninh','Thái Bình','Thái Nguyên','Thanh Hóa','Thừa Thiên Huế','Tiền Giang',
  'Thành phố Hồ Chí Minh','Trà Vinh','Tuyên Quang','Vĩnh Long','Vĩnh Phúc','Yên Bái',
  'Đà Nẵng'
];

const DISTRICT_MAP = {
  'Thành phố Hồ Chí Minh': ['Quận 1','Quận 3','Quận 4','Quận 5','Quận 6','Quận 7','Quận 8','Quận 10','Quận 11','Quận 12','Bình Thạnh','Gò Vấp','Phú Nhuận','Tân Bình','Tân Phú','Bình Tân','Thủ Đức','Bình Chánh','Củ Chi','Hóc Môn','Nhà Bè','Cần Giờ'],
  'Hà Nội': ['Ba Đình','Hoàn Kiếm','Hoàng Mai','Cầu Giấy','Tây Hồ','Long Biên','Đống Đa','Hai Bà Trưng','Thanh Xuân','Hà Đông','Nam Từ Liêm','Bắc Từ Liêm','Sơn Tây','Hoài Đức','Đông Anh','Gia Lâm','Thanh Trì','Chương Mỹ','Thường Tín','Mê Linh'],
  'Đà Nẵng': ['Hải Châu','Thanh Khê','Liên Chiểu','Ngũ Hành Sơn','Sơn Trà','Cẩm Lệ','Hòa Vang'],
  'Hải Phòng': ['Hồng Bàng','Lê Chân','Ngô Quyền','Kiến An','Hải An','Dương Kinh'],
  'Cần Thơ': ['Ninh Kiều','Bình Thuỷ','Cái Răng','Ô Môn','Thốt Nốt'],
  'Đồng Nai': ['Biên Hòa','Long Khánh','Nhơn Trạch','Trảng Bom','Long Thành','Vĩnh Cửu'],
  'Bình Dương': ['Thủ Dầu Một','Dĩ An','Thuận An','Tân Uyên','Bến Cát','Bàu Bàng'],
  'Khánh Hòa': ['Nha Trang','Cam Ranh','Ninh Hòa','Diên Khánh','Vạn Ninh'],
  'Lâm Đồng': ['Đà Lạt','Bảo Lộc','Đức Trọng','Lâm Hà','Đơn Dương'],
  'Quảng Ninh': ['Hạ Long','Cẩm Phả','Uông Bí','Móng Cái','Quảng Yên'],
  'Nghệ An': ['Vinh','Cửa Lò','Diễn Châu','Nghi Lộc','Hưng Nguyên'],
  'Thanh Hóa': ['Thanh Hóa','Bỉm Sơn','Sầm Sơn','Đông Sơn','Hoằng Hóa'],
  'Thừa Thiên Huế': ['Huế','Hương Thủy','Hương Trà','Phong Điền','Phú Vang'],
  'Bà Rịa – Vũng Tàu': ['Vũng Tàu','Bà Rịa','Phú Mỹ','Long Điền','Xuyên Mộc'],
};

const ALL_DISTRICTS = [...new Set(Object.values(DISTRICT_MAP).flat())];

function acSuggest(inp, listId) {
  const q = inp.value.trim().toLowerCase();
  const ul = document.getElementById(listId);
  if (!q) { ul.style.display = 'none'; return; }
  const matches = VN_PROVINCES.filter(p => p.toLowerCase().includes(q)).slice(0, 6);
  if (!matches.length) { ul.style.display = 'none'; return; }
  ul.innerHTML = matches.map(p => `<li onclick="pickProvince('${p}','${inp.id}','${listId}')">${p}</li>`).join('');
  ul.style.display = 'block';
}

function acDistrict(inp, listId, city) {
  const q = inp.value.trim().toLowerCase();
  const ul = document.getElementById(listId);
  const cityPool = DISTRICT_MAP[city] || [];
  const pool = cityPool.length ? cityPool : ALL_DISTRICTS;
  if (!q && !pool.length) { ul.style.display = 'none'; return; }
  const matches = pool
    .filter(d => !q || d.toLowerCase().includes(q))
    .slice(0, cityPool.length ? 10 : 12);
  if (!matches.length) { ul.style.display = 'none'; return; }
  ul.innerHTML = matches.map(d => `<li onclick="pickDistrict('${d}','${inp.id}','${listId}')">${d}</li>`).join('');
  ul.style.display = 'block';
}

function pickProvince(val, inputId, listId) {
  document.getElementById(inputId).value = val;
  document.getElementById(listId).style.display = 'none';
}
function pickDistrict(val, inputId, listId) {
  document.getElementById(inputId).value = val;
  document.getElementById(listId).style.display = 'none';
}

// ─── Init ──────────────────────────────────────────────────
initMap();
doSearch();
