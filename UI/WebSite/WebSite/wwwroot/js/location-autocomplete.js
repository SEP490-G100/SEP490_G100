(() => {
  const addressSuggestionCache = new Map();
  const districtOptionsCache = new Map();

  let provinces = [];
  let locationDataPromise = null;

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

  function getProvinceOptions() {
    return provinces.map((province) => province?.name).filter(Boolean);
  }

  function getDistrictOptions(cityName) {
    const normalizedCity = normalizeAdministrativeName(cityName);
    const selectedProvince = provinces.find((province) =>
      normalizeAdministrativeName(province?.name) === normalizedCity
    );
    return (selectedProvince?.districts || []).map((district) => district?.name).filter(Boolean);
  }

  function uniqueNormalizedValues(values, query, limit = 40) {
    const normalizedQuery = normalizeText(query);
    const seen = new Set();
    return (values || [])
      .filter((value) => !!value)
      .filter((value) => !normalizedQuery || normalizeText(value).includes(normalizedQuery))
      .filter((value) => {
        const key = normalizeText(value);
        if (!key || seen.has(key)) return false;
        seen.add(key);
        return true;
      })
      .slice(0, limit);
  }

  async function fetchAddressSuggestions(query) {
    const normalized = String(query ?? '').trim();
    if (normalized.length < 2) return [];

    const cacheKey = normalizeText(normalized);
    if (addressSuggestionCache.has(cacheKey)) {
      return addressSuggestionCache.get(cacheKey) || [];
    }

    try {
      const response = await fetch(`/Address/Suggest?q=${encodeURIComponent(normalized)}&limit=10`, {
        credentials: 'same-origin'
      });
      if (!response.ok) return [];
      const json = await response.json();
      const items = Array.isArray(json) ? json : [];
      addressSuggestionCache.set(cacheKey, items);
      return items;
    } catch {
      return [];
    }
  }

  async function fetchDistrictOptionsByCity(cityName) {
    const normalizedCity = String(cityName ?? '').trim();
    if (!normalizedCity) return [];

    const cached = getCachedDistrictOptions(normalizedCity);
    if (cached.length) return cached;

    return cacheDistrictOptions(normalizedCity, getDistrictOptions(normalizedCity));
  }

  async function getProvinceOptionsAsync(query) {
    return uniqueNormalizedValues(getProvinceOptions(), query);
  }

  async function getDistrictOptionsAsync(cityName, query) {
    const normalizedCity = String(cityName ?? '').trim();
    if (!normalizedCity) return [];

    const normalizedCityKey = normalizeAdministrativeName(normalizedCity);
    const localValues = uniqueNormalizedValues(await fetchDistrictOptionsByCity(normalizedCity), query);
    if (localValues.length) return localValues;

    if (!String(query ?? '').trim()) return [];

    const compositeQuery = [query, normalizedCity].filter(Boolean).join(', ');
    const suggestions = await fetchAddressSuggestions(compositeQuery);

    return uniqueNormalizedValues(
      suggestions
        .filter((item) => {
          const itemCity = String(item?.city ?? '').trim();
          if (!itemCity) return true;
          return normalizeAdministrativeName(itemCity).includes(normalizedCityKey);
        })
        .map((item) => item?.district),
      query
    );
  }

  function escapeHtml(value) {
    return String(value ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }

  function createDropdown(input) {
    const wrapper = input.parentElement;
    if (!wrapper) return null;

    wrapper.classList.add('autocomplete-field');

    const dropdown = document.createElement('ul');
    dropdown.className = 'ac-dropdown';
    input.insertAdjacentElement('afterend', dropdown);
    return dropdown;
  }

  function renderDropdown(dropdown, options, onSelect) {
    if (!dropdown) return;

    if (!options.length) {
      dropdown.innerHTML = '';
      dropdown.classList.remove('show');
      return;
    }

    dropdown.innerHTML = options
      .map((option) => `<li data-value="${escapeHtml(option)}">${escapeHtml(option)}</li>`)
      .join('');
    dropdown.classList.add('show');

    dropdown.querySelectorAll('li').forEach((item) => {
      item.addEventListener('mousedown', (event) => {
        event.preventDefault();
        onSelect(item.dataset.value || '');
      });
    });
  }

  function attachAutocomplete(input, optionGetter, onSelect) {
    if (!input || input.dataset.locationAutocompleteReady === 'true') return;
    input.dataset.locationAutocompleteReady = 'true';

    const dropdown = createDropdown(input);
    if (!dropdown) return;

    let requestToken = 0;
    let isComposing = false;

    const hide = () => dropdown.classList.remove('show');
    const showForQuery = async () => {
      const currentToken = ++requestToken;
      const query = String(input.value ?? '').trim();
      const options = await Promise.resolve(optionGetter(query));
      if (currentToken !== requestToken) return;
      renderDropdown(dropdown, options, (value) => {
        input.value = value;
        onSelect(value);
        hide();
      });
    };

    input.addEventListener('focus', showForQuery);
    input.addEventListener('click', showForQuery);
    input.addEventListener('compositionstart', () => { isComposing = true; });
    input.addEventListener('compositionend', () => {
      isComposing = false;
      showForQuery();
    });
    input.addEventListener('input', () => {
      if (isComposing) return;
      showForQuery();
    });
    input.addEventListener('blur', () => {
      window.setTimeout(hide, 120);
    });
  }

  async function loadLocationData() {
    if (locationDataPromise) return locationDataPromise;

    locationDataPromise = fetch('/Address/LocationTree', { credentials: 'same-origin' })
      .then((response) => (response.ok ? response.json() : []))
      .then((data) => {
        provinces = Array.isArray(data) ? data : [];
        return provinces;
      })
      .catch((error) => {
        provinces = [];
        throw error;
      });

    return locationDataPromise;
  }

  async function initPair(options) {
    const cityInput = document.getElementById(options?.cityInputId || '');
    const districtInput = document.getElementById(options?.districtInputId || '');
    if (!cityInput || !districtInput) return;

    const onLocationChange = typeof options?.onLocationChange === 'function'
      ? options.onLocationChange
      : () => {};

    await loadLocationData();

    const handleCityChange = (explicitCityValue, options = {}) => {
      const preserveDistrict = options.preserveDistrict !== false;
      const cityValue = String(explicitCityValue ?? cityInput.value ?? '').trim();
      const districtValue = String(districtInput.value ?? '').trim();
      const allowedDistricts = getCachedDistrictOptions(cityValue);
      const districtKey = normalizeAdministrativeName(districtValue);

      if (
        !preserveDistrict &&
        districtValue &&
        allowedDistricts.length &&
        !allowedDistricts.some((value) => normalizeAdministrativeName(value) === districtKey)
      ) {
        districtInput.value = '';
      }

      if (cityValue) {
        fetchDistrictOptionsByCity(cityValue);
      }

      onLocationChange({ city: cityInput.value.trim(), district: districtInput.value.trim() });
    };

    const handleDistrictChange = () => {
      onLocationChange({ city: cityInput.value.trim(), district: districtInput.value.trim() });
    };

    attachAutocomplete(
      cityInput,
      (query) => getProvinceOptionsAsync(query),
      (value) => {
        cityInput.value = value;
        districtInput.value = '';
        handleCityChange(value, { preserveDistrict: false });
      }
    );

    attachAutocomplete(
      districtInput,
      (query) => getDistrictOptionsAsync(cityInput.value.trim(), query),
      (value) => {
        districtInput.value = value;
        handleDistrictChange();
      }
    );

    cityInput.addEventListener('input', () => handleCityChange(undefined, { preserveDistrict: true }));
    cityInput.addEventListener('change', () => handleCityChange(undefined, { preserveDistrict: true }));
    districtInput.addEventListener('change', handleDistrictChange);
    districtInput.addEventListener('input', handleDistrictChange);

    if (cityInput.value.trim()) {
      await fetchDistrictOptionsByCity(cityInput.value.trim());
      handleCityChange(cityInput.value.trim(), { preserveDistrict: true });
    } else {
      onLocationChange({ city: '', district: districtInput.value.trim() });
    }
  }

  window.NMLocationAutocomplete = {
    initPair
  };
})();
