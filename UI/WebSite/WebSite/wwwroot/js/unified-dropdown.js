(() => {
  const SKIP_SELECT_CLASSES = new Set([
    'form-select-native-hidden',
    'nanny-filter--native'
  ]);

  function normalizeText(value) {
    return String(value ?? '')
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[đĐ]/g, 'd')
      .toLowerCase()
      .trim();
  }

  function isTransformableSelect(select) {
    if (!select || select.dataset.nmUnifiedReady === 'true') return false;
    if (select.dataset.nmUnifiedSkip === 'true') return false;
    if (select.multiple) return false;
    if (select.hasAttribute('hidden')) return false;
    if (select.closest('[data-nm-unified-skip="true"]')) return false;
    if (select.size && Number(select.size) > 1) return false;
    for (const className of SKIP_SELECT_CLASSES) {
      if (select.classList.contains(className)) return false;
    }
    return true;
  }

  function isTransformableListInput(input) {
    if (!input || input.dataset.nmUnifiedReady === 'true') return false;
    if (input.dataset.nmUnifiedSkip === 'true') return false;
    if (input.closest('[data-nm-unified-skip="true"]')) return false;
    return !!input.getAttribute('list');
  }

  function ensureFieldWrapper(control) {
    const parent = control.parentElement;
    if (!parent) return null;

    if (parent.classList.contains('nm-unified-field')) return parent;

    const wrapper = document.createElement('div');
    wrapper.className = 'nm-unified-field';
    control.insertAdjacentElement('beforebegin', wrapper);
    wrapper.appendChild(control);
    return wrapper;
  }

  function hideMenu(menu) {
    if (!menu) return;
    menu.classList.remove('show');
  }

  function renderMenu(menu, options, onSelect) {
    if (!menu) return;

    if (!options.length) {
      menu.innerHTML = '<li class="nm-unified-empty">Không có dữ liệu phù hợp</li>';
      menu.classList.add('show');
      return;
    }

    menu.innerHTML = options.map((option) =>
      `<li data-value="${escapeHtml(option.value)}" data-label="${escapeHtml(option.label)}">${escapeHtml(option.label)}</li>`
    ).join('');
    menu.classList.add('show');

    menu.querySelectorAll('li[data-value]').forEach((item) => {
      item.addEventListener('mousedown', (event) => {
        event.preventDefault();
        onSelect(item.dataset.value || '', item.dataset.label || '');
      });
    });
  }

  function escapeHtml(value) {
    return String(value ?? '')
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }

  function initSelect(select) {
    if (!isTransformableSelect(select)) return;

    const selectStyle = window.getComputedStyle(select);
    const inheritedPaddingLeft = selectStyle.paddingLeft;
    const inheritedPaddingRight = selectStyle.paddingRight;
    const inheritedMinHeight = selectStyle.minHeight;

    const wrapper = ensureFieldWrapper(select);
    if (!wrapper) return;

    const input = document.createElement('input');
    input.type = 'text';
    input.className = 'nm-unified-input';
    input.autocomplete = 'off';
    input.setAttribute('aria-haspopup', 'listbox');
    input.setAttribute('aria-expanded', 'false');

    const menu = document.createElement('ul');
    menu.className = 'nm-unified-menu';
    menu.setAttribute('role', 'listbox');

    if (inheritedPaddingLeft && inheritedPaddingLeft !== '0px') {
      input.style.paddingLeft = inheritedPaddingLeft;
    }
    if (inheritedPaddingRight && inheritedPaddingRight !== '0px') {
      const rightValue = parseFloat(inheritedPaddingRight);
      input.style.paddingRight = `${Math.max(38, Number.isFinite(rightValue) ? rightValue + 12 : 38)}px`;
    }
    if (inheritedMinHeight && inheritedMinHeight !== '0px' && inheritedMinHeight !== 'auto') {
      input.style.minHeight = inheritedMinHeight;
    }

    select.dataset.nmUnifiedReady = 'true';
    const isRequired = select.required;
    if (isRequired) {
      select.required = false;
      input.required = true;
    }

    select.classList.add('nm-unified-native');
    wrapper.appendChild(input);
    wrapper.appendChild(menu);

    const getPlaceholder = () => {
      if (select.dataset.placeholder) return select.dataset.placeholder;
      const emptyOption = Array.from(select.options).find((option) => String(option.value ?? '').trim() === '');
      return emptyOption ? emptyOption.text.trim() : '';
    };

    const buildOptions = (query) => {
      const normalizedQuery = normalizeText(query);
      return Array.from(select.options)
        .map((option) => ({
          value: String(option.value ?? ''),
          label: String(option.text ?? '').trim()
        }))
        .filter((option) => option.label.length > 0)
        .filter((option) => !normalizedQuery || normalizeText(option.label).includes(normalizedQuery))
        .slice(0, 80);
    };

    const syncFromSelect = () => {
      const selected = select.options[select.selectedIndex] || null;
      const selectedValue = String(select.value ?? '').trim();
      const placeholder = getPlaceholder();

      if (selected && selectedValue) {
        input.value = selected.text.trim();
      } else {
        input.value = '';
      }

      input.placeholder = placeholder;
      input.disabled = !!select.disabled;
      input.setAttribute('aria-expanded', 'false');
    };

    const showOptions = (query, preserveText) => {
      const activeQuery = typeof query === 'string' ? query : input.value.trim();
      const options = buildOptions(activeQuery);

      renderMenu(menu, options, (value, label) => {
        select.value = value;
        if (select.value !== value) {
          const fallback = Array.from(select.options).find((option) => String(option.value ?? '') === value);
          if (fallback) fallback.selected = true;
        }
        select.dispatchEvent(new Event('change', { bubbles: true }));
        if (preserveText === true) {
          input.value = label;
        }
        syncFromSelect();
        hideMenu(menu);
      });

      input.setAttribute('aria-expanded', 'true');
    };

    input.addEventListener('focus', () => showOptions(''));
    input.addEventListener('click', () => showOptions(input.value.trim()));
    input.addEventListener('input', () => showOptions(input.value.trim(), true));
    input.addEventListener('blur', () => {
      window.setTimeout(() => {
        hideMenu(menu);
        syncFromSelect();
      }, 120);
    });

    select.addEventListener('change', syncFromSelect);

    const observer = new MutationObserver(() => {
      syncFromSelect();
      if (menu.classList.contains('show')) {
        showOptions(input.value.trim());
      }
    });
    observer.observe(select, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['disabled']
    });

    const form = select.form;
    if (form) {
      form.addEventListener('reset', () => {
        window.setTimeout(syncFromSelect, 0);
      });
    }

    syncFromSelect();
  }

  function initListInput(input) {
    if (!isTransformableListInput(input)) return;

    const listId = input.getAttribute('list');
    const datalist = listId ? document.getElementById(listId) : null;
    if (!datalist) return;

    const wrapper = ensureFieldWrapper(input);
    if (!wrapper) return;

    input.dataset.nmUnifiedReady = 'true';
    input.removeAttribute('list');
    input.classList.add('nm-unified-input');
    datalist.hidden = true;

    const menu = document.createElement('ul');
    menu.className = 'nm-unified-menu';
    menu.setAttribute('role', 'listbox');
    wrapper.appendChild(menu);

    const buildOptions = (query) => {
      const normalizedQuery = normalizeText(query);
      return Array.from(datalist.options)
        .map((option) => String(option.value ?? '').trim())
        .filter((value) => !!value)
        .filter((value, index, arr) => arr.findIndex((v) => normalizeText(v) === normalizeText(value)) === index)
        .filter((value) => !normalizedQuery || normalizeText(value).includes(normalizedQuery))
        .slice(0, 80)
        .map((value) => ({ value, label: value }));
    };

    const showOptions = (query) => {
      const options = buildOptions(query);
      renderMenu(menu, options, (value) => {
        input.value = value;
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
        hideMenu(menu);
      });
      input.setAttribute('aria-expanded', 'true');
    };

    input.addEventListener('focus', () => showOptions(input.value.trim()));
    input.addEventListener('click', () => showOptions(input.value.trim()));
    input.addEventListener('input', () => showOptions(input.value.trim()));
    input.addEventListener('blur', () => {
      window.setTimeout(() => hideMenu(menu), 120);
    });

    const observer = new MutationObserver(() => {
      if (menu.classList.contains('show')) {
        showOptions(input.value.trim());
      }
    });
    observer.observe(datalist, { childList: true, subtree: true });
  }

  function initUnifiedDropdowns(root = document) {
    root.querySelectorAll('select').forEach(initSelect);
    root.querySelectorAll('input[list]').forEach(initListInput);
  }

  const domObserver = new MutationObserver((records) => {
    for (const record of records) {
      record.addedNodes.forEach((node) => {
        if (!(node instanceof HTMLElement)) return;
        if (node.matches?.('select, input[list]')) {
          initUnifiedDropdowns(node.parentElement || node);
          return;
        }
        if (node.querySelector?.('select, input[list]')) {
          initUnifiedDropdowns(node);
        }
      });
    }
  });

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
      initUnifiedDropdowns(document);
      domObserver.observe(document.body, { childList: true, subtree: true });
    }, { once: true });
  } else {
    initUnifiedDropdowns(document);
    domObserver.observe(document.body, { childList: true, subtree: true });
  }

  window.NMUnifiedDropdown = {
    refresh(root) {
      initUnifiedDropdowns(root || document);
    }
  };
})();
