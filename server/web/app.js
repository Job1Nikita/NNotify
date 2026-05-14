const strings = {
  ru: {
    tagline: 'Напоминания без пропусков',
    signinTitle: 'Вход в синхронизацию',
    serverHint: 'Используйте учетную запись сервера NNotify',
    username: 'Логин',
    password: 'Пароль',
    signin: 'Войти',
    addReminder: 'Добавить напоминание',
    upcoming: 'Ближайшие и активные',
    upcomingHint: 'Синхронизируются с вашими устройствами',
    empty: 'Активных напоминаний нет',
    history: 'История',
    newReminder: 'Добавить напоминание',
    editReminder: 'Изменить напоминание',
    textLabel: 'Текст напоминания',
    date: 'Дата',
    time: 'Время',
    priority: 'Приоритет',
    high: 'Важно',
    medium: 'Средне',
    low: 'Низко',
    cancel: 'Отмена',
    save: 'Сохранить',
    ack: 'Подтвердить',
    snooze5: 'Отложить 5м',
    snooze15: 'Отложить 15м',
    snoozeAction: 'Отложить',
    snooze5Action: 'На 5 минут',
    snooze15Action: 'На 15 минут',
    edit: 'Изменить',
    editReminderAction: 'Изменить напоминание',
    delete: 'Удалить',
    deleteReminderAction: 'Удалить напоминание',
    settings: 'Аккаунт',
    signedInAs: 'Вы вошли как',
    theme: 'Тема',
    themeSystem: 'Системная',
    themeLight: 'Светлая',
    themeDark: 'Тёмная',
    signout: 'Выйти',
    loginFailed: 'Не удалось войти. Проверьте логин, пароль и доступность сервера.',
    requestFailed: 'Не удалось выполнить действие. Попробуйте ещё раз.',
    networkFailed: 'Нет связи с сервером. Проверьте интернет и попробуйте ещё раз.',
    sessionExpired: 'Сессия истекла. Войдите снова.',
    futureRequired: 'Похоже, выбранное время уже прошло. Укажите время в будущем.',
    confirmDelete: 'Удалить напоминание?',
    duplicate: 'Похоже на дубль',
    syncOk: 'Синхронизация',
    syncFail: 'Нет связи',
    discardTitle: 'Закрыть без сохранения?',
    discardHint: 'Текст напоминания уже введён. Черновик будет потерян.',
    continueDraft: 'Продолжить добавление',
    discardDraft: 'Закрыть без сохранения'
  },
  en: {
    tagline: 'No-miss reminders', signinTitle: 'Sync sign in', serverHint: 'Use your NNotify server account', username: 'Username', password: 'Password', signin: 'Sign in', addReminder: 'Add reminder', upcoming: 'Upcoming', upcomingHint: 'Synced with your devices', empty: 'No active reminders', history: 'History', newReminder: 'New reminder', editReminder: 'Edit reminder', textLabel: 'Reminder text', date: 'Date', time: 'Time', priority: 'Priority', high: 'High', medium: 'Medium', low: 'Low', cancel: 'Cancel', save: 'Save', ack: 'Confirm', snooze5: 'Snooze 5m', snooze15: 'Snooze 15m', snoozeAction: 'Snooze', snooze5Action: 'For 5 minutes', snooze15Action: 'For 15 minutes', edit: 'Edit', editReminderAction: 'Edit reminder', delete: 'Delete', deleteReminderAction: 'Delete reminder', settings: 'Account', signedInAs: 'Signed in as', theme: 'Theme', themeSystem: 'System', themeLight: 'Light', themeDark: 'Dark', signout: 'Sign out', loginFailed: 'Could not sign in. Check username, password and server availability.', requestFailed: 'Action failed. Try again.', networkFailed: 'No connection to the server. Check your internet and try again.', sessionExpired: 'Session expired. Sign in again.', futureRequired: 'Selected time is in the past. Choose a future time.', confirmDelete: 'Delete reminder?', duplicate: 'Possible duplicate', syncOk: 'Sync', syncFail: 'Offline', discardTitle: 'Close without saving?', discardHint: 'You have already typed reminder text. The draft will be lost.', continueDraft: 'Keep editing', discardDraft: 'Close without saving'
  },
  de: {
    tagline: 'Erinnerungen ohne Lücken', signinTitle: 'Sync-Anmeldung', serverHint: 'Verwende dein NNotify-Serverkonto', username: 'Login', password: 'Passwort', signin: 'Anmelden', addReminder: 'Erinnerung hinzufügen', upcoming: 'Nächste', upcomingHint: 'Mit deinen Geräten synchronisiert', empty: 'Keine aktiven Erinnerungen', history: 'Historie', newReminder: 'Neue Erinnerung', editReminder: 'Erinnerung bearbeiten', textLabel: 'Text der Erinnerung', date: 'Datum', time: 'Uhrzeit', priority: 'Priorität', high: 'Wichtig', medium: 'Mittel', low: 'Niedrig', cancel: 'Abbrechen', save: 'Speichern', ack: 'Bestätigen', snooze5: '5m später', snooze15: '15m später', snoozeAction: 'Später erinnern', snooze5Action: 'In 5 Minuten', snooze15Action: 'In 15 Minuten', edit: 'Ändern', editReminderAction: 'Erinnerung ändern', delete: 'Löschen', deleteReminderAction: 'Erinnerung löschen', settings: 'Konto', signedInAs: 'Angemeldet als', theme: 'Theme', themeSystem: 'System', themeLight: 'Hell', themeDark: 'Dunkel', signout: 'Abmelden', loginFailed: 'Anmeldung fehlgeschlagen. Prüfe Login, Passwort und Server.', requestFailed: 'Aktion fehlgeschlagen. Bitte erneut versuchen.', networkFailed: 'Keine Verbindung zum Server. Prüfe das Internet und versuche es erneut.', sessionExpired: 'Sitzung abgelaufen. Bitte erneut anmelden.', futureRequired: 'Die gewählte Zeit liegt in der Vergangenheit. Wähle eine zukünftige Zeit.', confirmDelete: 'Erinnerung löschen?', duplicate: 'Mögliches Duplikat', syncOk: 'Sync', syncFail: 'Offline', discardTitle: 'Ohne Speichern schließen?', discardHint: 'Du hast bereits Erinnerungstext eingegeben. Der Entwurf geht verloren.', continueDraft: 'Weiter bearbeiten', discardDraft: 'Ohne Speichern schließen'
  }
};

const lang = (() => {
  const primary = (navigator.language || 'ru').slice(0, 2).toLowerCase();
  return strings[primary] ? primary : 'en';
})();
const t = (key) => strings[lang][key] || strings.en[key] || key;

const state = {
  authenticated: false,
  username: '',
  reminders: [],
  selected: null,
  editing: null,
  priority: 1,
  pollTimer: null,
  busy: new Set(),
  theme: localStorage.getItem('nnotify-theme') || 'system',
  renderedReminderIds: new Set(),
  closingSheet: false,
  lockedScrollY: 0,
  datePicker: {
    open: false,
    viewYear: null,
    viewMonth: null
  },
  timePicker: {
    open: false
  },
  suppressNextPickerFocus: null
};
let sessionRefreshPromise = null;

const $ = (id) => document.getElementById(id);

function syncViewportHeight() {
  const visualHeight = Math.ceil(window.visualViewport?.height || 0);
  const innerHeight = Math.ceil(window.innerHeight || 0);
  const screenHeight = Math.ceil(window.screen?.height || 0);
  const standalone = window.matchMedia?.('(display-mode: standalone)').matches || window.navigator.standalone === true;
  const height = standalone
    ? Math.max(visualHeight, innerHeight, screenHeight)
    : Math.max(visualHeight, innerHeight);
  if (height > 0) {
    document.documentElement.style.setProperty('--app-height', `${height}px`);
  }
}

syncViewportHeight();
window.addEventListener('resize', syncViewportHeight, { passive: true });
window.addEventListener('orientationchange', () => setTimeout(syncViewportHeight, 80), { passive: true });
window.visualViewport?.addEventListener('resize', syncViewportHeight, { passive: true });
window.addEventListener('load', () => {
  syncViewportHeight();
  setTimeout(syncViewportHeight, 120);
  setTimeout(syncViewportHeight, 420);
}, { once: true });

function applyTranslations() {
  document.documentElement.lang = lang;
  document.querySelectorAll('[data-i18n]').forEach((node) => {
    const key = node.getAttribute('data-i18n');
    node.textContent = t(key);
  });
}

function setTheme(theme) {
  state.theme = ['system', 'light', 'dark'].includes(theme) ? theme : 'system';
  localStorage.setItem('nnotify-theme', state.theme);

  if (state.theme === 'system') {
    document.documentElement.removeAttribute('data-theme');
  } else {
    document.documentElement.dataset.theme = state.theme;
  }

  document.querySelectorAll('[data-theme-choice]').forEach((button) => {
    const active = button.dataset.themeChoice === state.theme;
    button.classList.toggle('active', active);
    button.setAttribute('aria-checked', active ? 'true' : 'false');
  });

  const themeColor = document.querySelector('meta[name="theme-color"]');
  if (themeColor) {
    const darkSystem = window.matchMedia?.('(prefers-color-scheme: dark)').matches;
    const effectiveDark = state.theme === 'dark' || (state.theme === 'system' && darkSystem);
    themeColor.setAttribute('content', effectiveDark ? '#101a2a' : '#eef6ff');
  }
}

function getCookie(name) {
  const prefix = `${name}=`;
  return document.cookie.split(';').map((part) => part.trim()).find((part) => part.startsWith(prefix))?.slice(prefix.length) || '';
}

async function refreshWebSession() {
  if (!sessionRefreshPromise) {
    sessionRefreshPromise = fetch('/v1/web/session', { credentials: 'include' })
      .then(async (response) => {
        if (!response.ok) throw new Error('Session refresh failed');
        return response.json();
      })
      .finally(() => {
        sessionRefreshPromise = null;
      });
  }
  return sessionRefreshPromise;
}

function localizedNetworkError() {
  const error = new Error(t('networkFailed'));
  error.status = 0;
  return error;
}

function localizedSessionError() {
  const error = new Error(t('sessionExpired'));
  error.status = 401;
  return error;
}

async function api(path, options = {}, retry = true) {
  const method = options.method || 'GET';
  const headers = { ...(options.headers || {}) };
  if (options.body && !headers['Content-Type']) {
    headers['Content-Type'] = 'application/json';
  }
  if (!['GET', 'HEAD', 'OPTIONS'].includes(method.toUpperCase())) {
    headers['X-CSRF-Token'] = decodeURIComponent(getCookie('nn_csrf'));
  }

  let response;
  try {
    response = await fetch(path, {
      credentials: 'include',
      ...options,
      method,
      headers
    });
  } catch {
    throw localizedNetworkError();
  }

  let body = null;
  try {
    body = await response.json();
  } catch {
    body = null;
  }

  if (!response.ok) {
    if (response.status === 401 && retry && !path.startsWith('/v1/web/session') && !path.startsWith('/v1/web/login')) {
      try {
        await refreshWebSession();
      } catch {
        throw localizedSessionError();
      }
      return api(path, options, false);
    }

    const error = new Error(body?.message || t('requestFailed'));
    error.status = response.status;
    throw error;
  }

  return body;
}

function setButtonBusy(button, busy) {
  if (!button) {
    return;
  }

  button.disabled = busy;
  button.setAttribute('aria-busy', busy ? 'true' : 'false');
}

async function runExclusive(key, button, task) {
  if (state.busy.has(key)) {
    return;
  }

  state.busy.add(key);
  setButtonBusy(button, true);
  try {
    return await task();
  } finally {
    state.busy.delete(key);
    setButtonBusy(button, false);
  }
}

async function handleActionError(error, targetElement = null) {
  if (error?.status === 401) {
    await showLogin();
    $('loginError').textContent = t('sessionExpired');
    return;
  }

  setSync(false);
  if (targetElement) {
    targetElement.textContent = error?.message || t('requestFailed');
  }
}

function show(view) {
  $('loginView').classList.toggle('hidden', view !== 'login');
  $('mainView').classList.toggle('hidden', view !== 'main');
}

function setSync(ok) {
  const el = $('syncState');
  el.textContent = ok ? t('syncOk') : t('syncFail');
  el.style.opacity = ok ? '1' : '0.72';
}

function pad(value) {
  return String(value).padStart(2, '0');
}

function dateInputValue(date) {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

function timeInputValue(date) {
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function isDesktopDatePickerEnabled() {
  return window.matchMedia?.('(hover: hover) and (pointer: fine) and (min-width: 769px)').matches === true;
}

function isDesktopTimePickerEnabled() {
  return window.matchMedia?.('(hover: hover) and (pointer: fine) and (min-width: 769px)').matches === true;
}

function normalizeTimeValue(value) {
  const raw = String(value || '').trim();
  const match = raw.match(/^(\d{1,2}):(\d{1,2})$/);
  if (!match) return '';
  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (!Number.isInteger(hours) || !Number.isInteger(minutes) || hours < 0 || hours > 23 || minutes < 0 || minutes > 59) {
    return '';
  }
  return `${pad(hours)}:${pad(minutes)}`;
}

function getReminderTimeValue() {
  return normalizeTimeValue($('reminderTime')?.value || '');
}

function setReminderTimeValue(timeValue) {
  const input = $('reminderTime');
  if (!input) return;
  input.value = normalizeTimeValue(timeValue) || timeInputValue(new Date());
}

function timePickerLabel(key) {
  const labels = {
    ru: { now: 'Сейчас', done: 'Готово', hours: 'Часы', minutes: 'Минуты', time: 'Время', choose: 'Выберите время' },
    de: { now: 'Jetzt', done: 'Fertig', hours: 'Stunden', minutes: 'Minuten', time: 'Zeit', choose: 'Uhrzeit wählen' },
    en: { now: 'Now', done: 'Done', hours: 'Hours', minutes: 'Minutes', time: 'Time', choose: 'Choose time' }
  };
  return labels[lang]?.[key] || labels.en[key] || key;
}

function normalizeIsoDate(value) {
  const raw = String(value || '').trim();
  if (!raw) return '';
  if (/^\d{4}-\d{2}-\d{2}$/.test(raw)) return raw;
  const dotted = raw.match(/^(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{4})$/);
  if (dotted) {
    return `${dotted[3]}-${pad(dotted[2])}-${pad(dotted[1])}`;
  }
  const parsed = new Date(raw);
  return Number.isNaN(parsed.getTime()) ? '' : dateInputValue(parsed);
}

function formatDateForDisplay(isoDate) {
  const iso = normalizeIsoDate(isoDate);
  if (!iso) return '';
  const [year, month, day] = iso.split('-');
  return `${day}.${month}.${year}`;
}

function getReminderDateValue() {
  const input = $('reminderDate');
  return normalizeIsoDate(input?.dataset.isoValue || input?.value || '');
}

function setReminderDateValue(isoDate) {
  const input = $('reminderDate');
  if (!input) return;
  const iso = normalizeIsoDate(isoDate);
  input.dataset.isoValue = iso;
  input.value = input.type === 'text' && isDesktopDatePickerEnabled()
    ? formatDateForDisplay(iso)
    : iso;
}

function setReminderDateMin(isoDate) {
  const input = $('reminderDate');
  if (!input) return;
  const iso = normalizeIsoDate(isoDate);
  input.dataset.minValue = iso;
  if (input.type === 'date') {
    input.min = iso;
  }
}

function todayIsoDate() {
  return dateInputValue(new Date());
}

function createLocalDateFromIso(isoDate) {
  const iso = normalizeIsoDate(isoDate) || todayIsoDate();
  const [year, month, day] = iso.split('-').map(Number);
  return new Date(year, month - 1, day);
}

function monthTitle(year, month) {
  const value = new Intl.DateTimeFormat(lang === 'ru' ? 'ru-RU' : lang === 'de' ? 'de-DE' : 'en-US', {
    month: 'long',
    year: 'numeric'
  }).format(new Date(year, month, 1));
  return value.charAt(0).toUpperCase() + value.slice(1);
}

function ensureDesktopDatePicker() {
  let picker = $('desktopDatePicker');
  if (picker) return picker;

  picker = document.createElement('div');
  picker.id = 'desktopDatePicker';
  picker.className = 'desktop-date-picker hidden';
  picker.setAttribute('role', 'dialog');
  picker.setAttribute('aria-label', t('date'));
  $('desktopPickerArea')?.appendChild(picker);

  picker.addEventListener('click', (event) => {
    const navButton = event.target.closest('[data-calendar-nav]');
    if (navButton) {
      moveDesktopDatePickerMonth(Number(navButton.dataset.calendarNav));
      return;
    }

    if (event.target.closest('[data-calendar-today]')) {
      const today = createLocalDateFromIso(todayIsoDate());
      state.datePicker.viewYear = today.getFullYear();
      state.datePicker.viewMonth = today.getMonth();
      setReminderDateValue(todayIsoDate());
      hideDesktopDatePicker();
      return;
    }

    const dateButton = event.target.closest('[data-calendar-date]');
    if (dateButton && !dateButton.disabled) {
      setReminderDateValue(dateButton.dataset.calendarDate);
      hideDesktopDatePicker();
    }
  });

  let calendarWheelLocked = false;
  picker.addEventListener('wheel', (event) => {
    if (!state.datePicker.open) return;
    event.preventDefault();
    const delta = Math.abs(event.deltaY) >= Math.abs(event.deltaX) ? event.deltaY : event.deltaX;
    if (!delta || calendarWheelLocked) return;
    moveDesktopDatePickerMonth(delta > 0 ? 1 : -1);
    calendarWheelLocked = true;
    window.setTimeout(() => {
      calendarWheelLocked = false;
    }, 180);
  }, { passive: false });

  return picker;
}

function moveDesktopDatePickerMonth(direction) {
  const baseYear = Number.isInteger(state.datePicker.viewYear) ? state.datePicker.viewYear : createLocalDateFromIso(getReminderDateValue()).getFullYear();
  const baseMonth = Number.isInteger(state.datePicker.viewMonth) ? state.datePicker.viewMonth : createLocalDateFromIso(getReminderDateValue()).getMonth();
  const next = new Date(baseYear, baseMonth + Number(direction || 0), 1);
  state.datePicker.viewYear = next.getFullYear();
  state.datePicker.viewMonth = next.getMonth();
  renderDesktopDatePicker();
}

function renderDesktopDatePicker() {
  const picker = ensureDesktopDatePicker();
  const selectedIso = getReminderDateValue();
  const minIso = $('reminderDate')?.dataset.minValue || '';
  const todayIso = todayIsoDate();
  const year = state.datePicker.viewYear ?? createLocalDateFromIso(selectedIso).getFullYear();
  const month = state.datePicker.viewMonth ?? createLocalDateFromIso(selectedIso).getMonth();
  const firstOfMonth = new Date(year, month, 1);
  const startOffset = (firstOfMonth.getDay() + 6) % 7;
  const start = new Date(year, month, 1 - startOffset);
  const weekdays = lang === 'ru'
    ? ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс']
    : lang === 'de'
      ? ['Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa', 'So']
      : ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

  const days = Array.from({ length: 42 }, (_, index) => {
    const current = new Date(start);
    current.setDate(start.getDate() + index);
    const iso = dateInputValue(current);
    const outside = current.getMonth() !== month;
    const selected = iso === selectedIso;
    const today = iso === todayIso;
    const disabled = minIso && iso < minIso;
    const classes = [outside ? 'is-outside' : '', selected ? 'is-selected' : '', today ? 'is-today' : '']
      .filter(Boolean)
      .join(' ');
    return `<button type="button" class="${classes}" data-calendar-date="${iso}" ${disabled ? 'disabled' : ''}>${current.getDate()}</button>`;
  }).join('');

  picker.innerHTML = `
    <div class="calendar-head">
      <button type="button" data-calendar-nav="-1" aria-label="Предыдущий месяц">‹</button>
      <strong>${monthTitle(year, month)}</strong>
      <button type="button" data-calendar-nav="1" aria-label="Следующий месяц">›</button>
    </div>
    <div class="calendar-weekdays">${weekdays.map((day) => `<span>${day}</span>`).join('')}</div>
    <div class="calendar-days">${days}</div>
    <div class="calendar-footer">
      <button type="button" data-calendar-today>${lang === 'ru' ? 'Сегодня' : lang === 'de' ? 'Heute' : 'Today'}</button>
    </div>
  `;
}

function revealDesktopPicker(picker) {
  if (!picker) return;

  if (picker._hideTimer) {
    window.clearTimeout(picker._hideTimer);
    picker._hideTimer = 0;
  }

  if (!picker.classList.contains('hidden') && picker.classList.contains('is-open') && !picker.classList.contains('is-hiding')) {
    return;
  }

  picker.classList.remove('is-open', 'is-hiding', 'is-measuring');
  picker.classList.remove('hidden');
  picker.getBoundingClientRect();
  requestAnimationFrame(() => {
    if (picker.classList.contains('hidden')) return;
    picker.classList.add('is-open');
  });
}

function hideDesktopPickerElement(picker) {
  if (!picker || picker.classList.contains('hidden')) return;
  if (picker._hideTimer) {
    window.clearTimeout(picker._hideTimer);
    picker._hideTimer = 0;
  }
  picker.classList.remove('is-measuring', 'is-open');
  picker.classList.add('is-hiding');
  picker._hideTimer = window.setTimeout(() => {
    if (!picker.classList.contains('is-open')) {
      picker.classList.add('hidden');
      picker.classList.remove('is-hiding');
    }
    picker._hideTimer = 0;
  }, 230);
}

function positionDesktopDatePicker() {
  // Inline picker: layout is handled by the form flow.
}

function showDesktopDatePicker() {
  if (!isDesktopDatePickerEnabled()) return;
  hideDesktopTimePicker();
  const input = $('reminderDate');
  const picker = ensureDesktopDatePicker();
  input?.classList.add('selector-active');

  if (state.datePicker.open && !picker.classList.contains('hidden') && !picker.classList.contains('is-hiding')) {
    return;
  }

  const selected = createLocalDateFromIso(getReminderDateValue() || todayIsoDate());
  state.datePicker.viewYear = selected.getFullYear();
  state.datePicker.viewMonth = selected.getMonth();
  input.dataset.isoValue = getReminderDateValue() || todayIsoDate();
  renderDesktopDatePicker();
  state.datePicker.open = true;
  revealDesktopPicker(picker);
}

function hideDesktopDatePicker() {
  const picker = $('desktopDatePicker');
  $('reminderDate')?.classList.remove('selector-active');
  hideDesktopPickerElement(picker);
  state.datePicker.open = false;
}

function applyDateInputMode() {
  const input = $('reminderDate');
  if (!input) return;
  const iso = getReminderDateValue() || normalizeIsoDate(input.value) || todayIsoDate();
  const desktopPicker = isDesktopDatePickerEnabled();

  if (desktopPicker) {
    if (input.type !== 'text') input.type = 'text';
    input.readOnly = true;
    input.inputMode = 'none';
    input.classList.add('uses-custom-date-picker');
    setReminderDateValue(iso);
    return;
  }

  hideDesktopDatePicker();
  if (input.type !== 'date') input.type = 'date';
  input.readOnly = false;
  input.removeAttribute('inputmode');
  input.classList.remove('uses-custom-date-picker');
  if (input.dataset.minValue) input.min = input.dataset.minValue;
  setReminderDateValue(iso);
}

function timePickerValues(count) {
  return Array.from({ length: count }, (_, index) => index);
}

function ensureDesktopTimePicker() {
  let picker = $('desktopTimePicker');
  if (picker) return picker;

  picker = document.createElement('div');
  picker.id = 'desktopTimePicker';
  picker.className = 'desktop-time-picker hidden';
  picker.setAttribute('role', 'dialog');
  picker.setAttribute('aria-label', timePickerLabel('choose'));
  $('desktopPickerArea')?.appendChild(picker);

  picker.addEventListener('click', (event) => {
    if (event.target === picker) {
      hideDesktopTimePicker();
      return;
    }

    const item = event.target.closest('[data-time-value]');
    if (item) {
      setDesktopTimePart(item.dataset.timePart, Number(item.dataset.timeValue), true);
      return;
    }
  });

  return picker;
}

function setDesktopTimePart(part, value, shouldAlign = false) {
  const current = getReminderTimeValue() || timeInputValue(new Date());
  let [hours, minutes] = current.split(':').map(Number);

  if (part === 'hours') {
    hours = ((Number(value) % 24) + 24) % 24;
  } else if (part === 'minutes') {
    minutes = ((Number(value) % 60) + 60) % 60;
  } else {
    return;
  }

  setReminderTimeValue(`${pad(hours)}:${pad(minutes)}`);
  updateDesktopTimePickerVisuals();
  if (shouldAlign) {
    scrollDesktopTimeWheelToValue(part, part === 'hours' ? hours : minutes, 'smooth');
  }
}

function desktopTimePickerWheelMarkup(part, count, selected) {
  return timePickerValues(count).map((value) => {
    const active = value === selected ? ' active' : '';
    return `<button type="button" class="time-wheel-item${active}" data-time-part="${part}" data-time-value="${value}" aria-selected="${active ? 'true' : 'false'}">${pad(value)}</button>`;
  }).join('');
}

function renderDesktopTimePicker() {
  const picker = ensureDesktopTimePicker();
  const value = getReminderTimeValue() || timeInputValue(new Date());
  const [hours, minutes] = value.split(':').map(Number);

  picker.innerHTML = `
    <div class="time-picker-card">
      <div class="time-picker-head">
        <strong>${timePickerLabel('time')}</strong>
        <div class="time-picker-display" aria-live="polite">
          <span data-time-display="hours">${pad(hours)}</span><span class="time-picker-colon">:</span><span data-time-display="minutes">${pad(minutes)}</span>
        </div>
      </div>
      <div class="time-picker-wheels">
        <section class="time-wheel-frame">
          <h3>${timePickerLabel('hours')}</h3>
          <div class="time-wheel" data-time-wheel="hours" tabindex="0">
            ${desktopTimePickerWheelMarkup('hours', 24, hours)}
          </div>
        </section>
        <section class="time-wheel-frame">
          <h3>${timePickerLabel('minutes')}</h3>
          <div class="time-wheel" data-time-wheel="minutes" tabindex="0">
            ${desktopTimePickerWheelMarkup('minutes', 60, minutes)}
          </div>
        </section>
      </div>
    </div>
  `;

  bindDesktopTimeWheelEvents();
}

function getDesktopTimeWheel(part) {
  return $(`desktopTimePicker`)?.querySelector(`[data-time-wheel="${part}"]`);
}

function scrollDesktopTimeWheelToValue(part, value, behavior = 'auto') {
  const wheel = getDesktopTimeWheel(part);
  if (!wheel) return;
  const item = wheel.querySelector(`[data-time-value="${Number(value)}"]`);
  if (!item) return;
  const top = item.offsetTop - ((wheel.clientHeight - item.offsetHeight) / 2);
  wheel.scrollTo({ top: Math.max(0, top), behavior });
}

function syncDesktopTimePickerToValue(behavior = 'auto') {
  updateDesktopTimePickerVisuals();
  const value = getReminderTimeValue() || timeInputValue(new Date());
  const [hours, minutes] = value.split(':').map(Number);
  requestAnimationFrame(() => {
    scrollDesktopTimeWheelToValue('hours', hours, behavior);
    scrollDesktopTimeWheelToValue('minutes', minutes, behavior);
  });
}

function updateDesktopTimePickerVisuals() {
  const picker = $('desktopTimePicker');
  if (!picker) return;
  const value = getReminderTimeValue() || timeInputValue(new Date());
  const [hours, minutes] = value.split(':').map(Number);
  const displayHours = picker.querySelector('[data-time-display="hours"]');
  const displayMinutes = picker.querySelector('[data-time-display="minutes"]');
  if (displayHours) displayHours.textContent = pad(hours);
  if (displayMinutes) displayMinutes.textContent = pad(minutes);

  picker.querySelectorAll('[data-time-value]').forEach((item) => {
    const active = (item.dataset.timePart === 'hours' && Number(item.dataset.timeValue) === hours)
      || (item.dataset.timePart === 'minutes' && Number(item.dataset.timeValue) === minutes);
    item.classList.toggle('active', active);
    item.setAttribute('aria-selected', active ? 'true' : 'false');
  });
}

function syncTimeWheelFromScroll(wheel) {
  if (!wheel || state.timePicker.syncing) return;
  const items = Array.from(wheel.querySelectorAll('[data-time-value]'));
  if (!items.length) return;

  const center = wheel.scrollTop + (wheel.clientHeight / 2);
  let closest = items[0];
  let closestDistance = Infinity;

  items.forEach((item) => {
    const itemCenter = item.offsetTop + (item.offsetHeight / 2);
    const distance = Math.abs(itemCenter - center);
    if (distance < closestDistance) {
      closest = item;
      closestDistance = distance;
    }
  });

  if (closest) {
    setDesktopTimePart(closest.dataset.timePart, Number(closest.dataset.timeValue), false);
  }
}

function bindDesktopTimeWheelEvents() {
  const picker = $('desktopTimePicker');
  if (!picker) return;

  picker.querySelectorAll('.time-wheel').forEach((wheel) => {
    let raf = 0;
    let snapTimer = 0;

    const scheduleSync = () => {
      if (raf) return;
      raf = requestAnimationFrame(() => {
        raf = 0;
        syncTimeWheelFromScroll(wheel);
      });
    };

    wheel.addEventListener('scroll', () => {
      scheduleSync();
      clearTimeout(snapTimer);
      snapTimer = setTimeout(() => {
        const active = wheel.querySelector('.time-wheel-item.active');
        if (active) {
          const part = wheel.dataset.timeWheel;
          scrollDesktopTimeWheelToValue(part, Number(active.dataset.timeValue), 'smooth');
        }
      }, 120);
    }, { passive: true });

    wheel.addEventListener('keydown', (event) => {
      const part = wheel.dataset.timeWheel;
      const current = Number(wheel.querySelector('.time-wheel-item.active')?.dataset.timeValue || 0);
      const max = part === 'hours' ? 23 : 59;
      if (event.key === 'ArrowDown') {
        event.preventDefault();
        setDesktopTimePart(part, current >= max ? 0 : current + 1, true);
      } else if (event.key === 'ArrowUp') {
        event.preventDefault();
        setDesktopTimePart(part, current <= 0 ? max : current - 1, true);
      }
    });
  });
}

function positionDesktopTimePicker() {
  // Inline picker: layout is handled by the form flow.
}

function showDesktopTimePicker() {
  if (!isDesktopTimePickerEnabled()) return;
  hideDesktopDatePicker();
  const input = $('reminderTime');
  const picker = ensureDesktopTimePicker();
  input?.classList.add('selector-active');

  if (state.timePicker.open && !picker.classList.contains('hidden') && !picker.classList.contains('is-hiding')) {
    return;
  }

  setReminderTimeValue(getReminderTimeValue() || timeInputValue(new Date()));
  renderDesktopTimePicker();
  state.timePicker.open = true;
  revealDesktopPicker(picker);
  requestAnimationFrame(() => syncDesktopTimePickerToValue('auto'));
}

function hideDesktopTimePicker() {
  const picker = $('desktopTimePicker');
  $('reminderTime')?.classList.remove('selector-active');
  hideDesktopPickerElement(picker);
  state.timePicker.open = false;
}

function applyTimeInputMode() {
  const input = $('reminderTime');
  if (!input) return;
  const value = getReminderTimeValue() || normalizeTimeValue(input.value) || timeInputValue(new Date());
  const desktopPicker = isDesktopTimePickerEnabled();

  if (desktopPicker) {
    if (input.type !== 'text') input.type = 'text';
    input.readOnly = true;
    input.inputMode = 'none';
    input.classList.add('uses-custom-time-picker');
    setReminderTimeValue(value);
    return;
  }

  hideDesktopTimePicker();
  if (input.type !== 'time') input.type = 'time';
  input.readOnly = false;
  input.removeAttribute('inputmode');
  input.classList.remove('uses-custom-time-picker');
  setReminderTimeValue(value);
}

function hideDesktopPickers() {
  hideDesktopDatePicker();
  hideDesktopTimePicker();
}

function syncQuickMinuteLabels() {
  const desktopPicker = isDesktopTimePickerEnabled();
  document.querySelectorAll('[data-add-minutes]').forEach((button) => {
    const minutes = button.dataset.addMinutes;
    button.textContent = desktopPicker
      ? `+${minutes} ${lang === 'en' ? 'min' : 'мин'}`
      : `+${minutes}`;
  });
}

function formatDateTime(ms) {
  return new Intl.DateTimeFormat(lang === 'ru' ? 'ru-RU' : lang === 'de' ? 'de-DE' : 'en-GB', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
  }).format(new Date(ms));
}

function formatCardDate(ms) {
  return new Intl.DateTimeFormat(lang === 'ru' ? 'ru-RU' : lang === 'de' ? 'de-DE' : 'en-GB', {
    day: '2-digit', month: '2-digit', year: 'numeric'
  }).format(new Date(ms));
}

function formatCardTime(ms) {
  return new Intl.DateTimeFormat(lang === 'ru' ? 'ru-RU' : lang === 'de' ? 'de-DE' : 'en-GB', {
    hour: '2-digit', minute: '2-digit'
  }).format(new Date(ms));
}

function iconSvg(name) {
  const icons = {
    check: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12.5l4.2 4.2L19 7"/></svg>',
    pill: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M10.2 20.2l10-10a4 4 0 0 0-5.7-5.7l-10 10a4 4 0 0 0 5.7 5.7Z"/><path d="M8.5 10.5l5 5"/></svg>',
    drop: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 3.5s6 6.4 6 11a6 6 0 0 1-12 0c0-4.6 6-11 6-11Z"/></svg>',
    calendar: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 3v3M17 3v3M4.5 9h15M6 5h12a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Z"/></svg>',
    clock: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z"/><path d="M12 7v5l3 2"/></svg>',
    phone: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 4h10a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z"/><path d="M11 17h2"/></svg>',
    cart: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 5h2l2 10h9l2-7H7"/><path d="M9 20h.01M17 20h.01"/></svg>',
    work: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 7V5.8A1.8 1.8 0 0 1 10.8 4h2.4A1.8 1.8 0 0 1 15 5.8V7"/><path d="M5 7h14a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V9a2 2 0 0 1 2-2Z"/><path d="M3 12h18"/></svg>',
    note: '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 4h7l4 4v12H7a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z"/><path d="M14 4v5h5"/><path d="M8 13h8M8 17h6"/></svg>'
  };
  return icons[name] || icons.check;
}

function reminderIcon(title = '') {
  const text = title.toLowerCase();
  if (/(вода|воды|пить|выпить|hydration|water|drink)/i.test(text)) return iconSvg('drop');
  if (/(лекар|таблет|капсул|витамин|pill|medicine|medication)/i.test(text)) return iconSvg('pill');
  if (/(встреч|созвон|звонок|команд|митинг|meeting|call|team)/i.test(text)) return iconSvg('check');
  if (/(купить|магазин|заказ|продукт|shop|buy|order)/i.test(text)) return iconSvg('cart');
  if (/(работ|проект|задач|отчет|отчёт|work|project|task|report)/i.test(text)) return iconSvg('work');
  if (/(позвон|телефон|phone)/i.test(text)) return iconSvg('phone');
  return iconSvg('note');
}

function priorityLabel(priority) {
  if (priority === 0) return t('high');
  if (priority === 2) return t('low');
  return t('medium');
}

function reminderEffectiveDue(reminder) {
  return reminder.snoozeUntilUtc || reminder.dueAtUtc;
}

function activeReminders() {
  return state.reminders
    .filter((r) => !r.deletedAtUtc && ['scheduled', 'fired', 'snoozed'].includes(r.status))
    .sort((a, b) => reminderEffectiveDue(a) - reminderEffectiveDue(b));
}

function historyReminders() {
  return state.reminders
    .filter((r) => !r.deletedAtUtc && (r.status === 'acked' || r.status === 'cancelled' || r.status === 'missed'))
    .sort((a, b) => (b.updatedAtUtc || 0) - (a.updatedAtUtc || 0))
    .slice(0, 20);
}

function render() {
  const list = $('reminderList');
  list.innerHTML = '';
  const active = activeReminders();
  $('emptyState').classList.toggle('hidden', active.length > 0);
  const nextRenderedIds = new Set();

  active.forEach((reminder, index) => {
    const isNew = !state.renderedReminderIds.has(reminder.id);
    nextRenderedIds.add(reminder.id);
    const card = document.createElement('article');
    card.className = `reminder-card${isNew ? ' is-new' : ''}`;
    card.tabIndex = 0;
    card.setAttribute('role', 'button');
    card.style.setProperty('--i', index);
    const due = reminderEffectiveDue(reminder);
    card.innerHTML = `
      <div class="card-icon" aria-hidden="true">${reminderIcon(reminder.title || '')}</div>
      <div class="card-main">
        <div class="card-title"></div>
        <div class="card-meta">
          <span>${iconSvg('calendar')}<span class="card-date"></span></span>
          <span>${iconSvg('clock')}<span class="card-clock"></span></span>
        </div>
      </div>
      <span class="priority-pill priority-${reminder.priority}"></span>
    `;
    card.querySelector('.card-title').textContent = reminder.title || 'NNotify';
    card.querySelector('.card-date').textContent = formatCardDate(due);
    card.querySelector('.card-clock').textContent = formatCardTime(due);
    card.querySelector('.priority-pill').textContent = priorityLabel(reminder.priority);
    if (reminder.duplicateCandidate) {
      const badge = document.createElement('span');
      badge.className = 'status-pill';
      badge.textContent = t('duplicate');
      card.appendChild(badge);
    }
    card.addEventListener('click', () => openActionSheet(reminder));
    card.addEventListener('keydown', (event) => {
      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        openActionSheet(reminder);
      }
    });
    list.appendChild(card);
  });
  state.renderedReminderIds = nextRenderedIds;

  const history = historyReminders();
  $('historyCount').textContent = history.length;
  const historyList = $('historyList');
  historyList.innerHTML = '';
  history.forEach((reminder) => {
    const item = document.createElement('div');
    item.className = 'history-item';
    item.innerHTML = '<strong></strong><span></span>';
    item.querySelector('strong').textContent = reminder.title || 'NNotify';
    item.querySelector('span').textContent = `${formatDateTime(reminderEffectiveDue(reminder))} · ${priorityLabel(reminder.priority)}`;
    historyList.appendChild(item);
  });
}

async function loadReminders() {
  try {
    const data = await api('/v1/web/reminders');
    state.reminders = data.reminders || [];
    setSync(true);
    render();
  } catch (error) {
    if (error.status === 401) {
      await showLogin();
      return;
    }
    setSync(false);
  }
}

async function showLogin() {
  state.authenticated = false;
  clearInterval(state.pollTimer);
  state.pollTimer = null;
  closeSheets(true);
  show('login');
}

async function showMain(username) {
  state.authenticated = true;
  state.username = username || '';
  $('currentUser').textContent = state.username;
  show('main');
  await loadReminders();
  clearInterval(state.pollTimer);
  state.pollTimer = setInterval(loadReminders, 5000);
}

function setDefaultReminderTime() {
  const date = new Date(Date.now() + 5 * 60 * 1000);
  setReminderDateMin(dateInputValue(new Date()));
  setReminderDateValue(dateInputValue(date));
  setReminderTimeValue(timeInputValue(date));
}

function setPriority(priority) {
  state.priority = priority;
  document.querySelectorAll('[data-priority]').forEach((button) => {
    button.classList.toggle('active', Number(button.dataset.priority) === priority);
  });
}

function blurActiveElement() {
  const active = document.activeElement;
  if (active && typeof active.blur === 'function' && active !== document.body) {
    active.blur();
  }
}

function shouldAutoFocusSheet() {
  return window.matchMedia?.('(pointer: fine)').matches && !window.matchMedia?.('(max-width: 768px)').matches;
}

function lockPageScroll() {
  if (document.body.classList.contains('sheet-open')) return;
  state.lockedScrollY = window.scrollY || document.documentElement.scrollTop || 0;
  document.documentElement.classList.add('sheet-open');
  document.body.classList.add('sheet-open');
}

function unlockPageScroll() {
  if (!document.body.classList.contains('sheet-open')) return;
  document.documentElement.classList.remove('sheet-open');
  document.body.classList.remove('sheet-open');
  state.lockedScrollY = 0;
}

function openBackdrop() {
  syncViewportHeight();
  lockPageScroll();
  const backdrop = $('sheetBackdrop');
  backdrop.classList.remove('hidden', 'is-closing');
}

function getOpenSheets() {
  return ['reminderSheet', 'actionSheet', 'settingsSheet', 'discardSheet']
    .map((id) => $(id))
    .filter((sheet) => sheet && !sheet.classList.contains('hidden'));
}

function resetSheetDragState(sheet) {
  const content = sheet?.querySelector('.sheet-content');
  if (!content) return;
  sheet.classList.remove('is-dragging', 'is-drag-closing');
  content.style.removeProperty('transform');
  content.style.removeProperty('transition');
  content.style.removeProperty('animation');
  $('sheetBackdrop').style.removeProperty('opacity');
}

function finishClose(sheet) {
  if (!sheet) return;
  if (sheet.id === 'reminderSheet') hideDesktopPickers();
  resetSheetDragState(sheet);
  sheet.classList.add('hidden');
  sheet.classList.remove('is-closing');
}


function isSheetOpen(id) {
  const sheet = $(id);
  return !!sheet && !sheet.classList.contains('hidden');
}

function hasUnsavedReminderDraft() {
  if (!isSheetOpen('reminderSheet') || isSheetOpen('discardSheet')) return false;
  const currentTitle = $('reminderTitle').value.trim();
  if (!currentTitle) return false;

  if (!state.editing) return true;

  const originalTitle = (state.editing.title || '').trim();
  const originalDue = new Date(reminderEffectiveDue(state.editing));
  return currentTitle !== originalTitle
    || getReminderDateValue() !== dateInputValue(originalDue)
    || getReminderTimeValue() !== timeInputValue(originalDue)
    || state.priority !== state.editing.priority;
}

function shouldAskBeforeClosing(sheet = null) {
  const isReminderClose = !sheet || sheet.id === 'reminderSheet';
  return isReminderClose && hasUnsavedReminderDraft();
}

function openDiscardConfirm() {
  hideDesktopPickers();
  openBackdrop();
  $('discardSheet').classList.remove('hidden');
}

function hideDiscardConfirm() {
  const sheet = $('discardSheet');
  resetSheetDragState(sheet);
  sheet.classList.add('hidden');
  sheet.classList.remove('is-closing', 'is-drag-closing', 'is-dragging');
}

function snapSheetBack(sheet) {
  const content = sheet?.querySelector('.sheet-content');
  if (!content) return;
  sheet.classList.remove('is-dragging', 'is-drag-closing');
  content.style.transition = 'transform 220ms cubic-bezier(0.2, 0.8, 0.2, 1)';
  content.style.transform = 'translate3d(0, 0, 0)';
  $('sheetBackdrop').style.removeProperty('opacity');
  setTimeout(() => {
    if (!sheet.classList.contains('hidden')) {
      content.style.removeProperty('transform');
      content.style.removeProperty('transition');
    }
  }, 240);
}

function requestCloseSheets(sheet = null) {
  hideDesktopPickers();
  if (isSheetOpen('discardSheet')) {
    hideDiscardConfirm();
    return;
  }
  if (shouldAskBeforeClosing(sheet)) {
    openDiscardConfirm();
    return;
  }
  closeSheets();
}

function requestCloseSheetFromDrag(sheet, distance = 0) {
  if (shouldAskBeforeClosing(sheet)) {
    snapSheetBack(sheet);
    openDiscardConfirm();
    return;
  }
  closeSheetFromDrag(sheet, distance);
}

function closeSheetFromDrag(sheet, distance = 0) {
  if (!sheet || state.closingSheet) return;
  const content = sheet.querySelector('.sheet-content');
  const backdrop = $('sheetBackdrop');
  if (!content) {
    closeSheets();
    return;
  }

  blurActiveElement();
  state.selected = null;
  state.closingSheet = true;

  const startY = Math.max(0, distance);
  const targetY = Math.max(startY + 260, content.getBoundingClientRect().height + 80);

  sheet.classList.remove('is-dragging', 'is-closing');
  sheet.classList.add('is-drag-closing');
  backdrop.classList.add('is-closing');

  content.style.animation = 'none';
  content.style.transition = 'none';
  content.style.transform = `translate3d(0, ${startY}px, 0)`;
  backdrop.style.transition = 'none';
  backdrop.style.opacity = String(Math.max(0, 1 - startY / 320));

  // Force layout before enabling transition. Without this, iOS PWA may briefly
  // replay the CSS close animation from translateY(0), which looks like a hiccup.
  content.getBoundingClientRect();

  requestAnimationFrame(() => {
    content.style.transition = 'transform 240ms cubic-bezier(0.22, 1, 0.36, 1)';
    content.style.transform = `translate3d(0, ${targetY}px, 0)`;
    backdrop.style.transition = 'opacity 180ms ease';
    backdrop.style.opacity = '0';
  });

  setTimeout(() => {
    sheet.classList.remove('is-drag-closing');
    finishClose(sheet);
    content.style.removeProperty('opacity');
    content.style.removeProperty('animation');
    backdrop.classList.add('hidden');
    backdrop.classList.remove('is-closing');
    backdrop.style.removeProperty('opacity');
    backdrop.style.removeProperty('transition');
    state.closingSheet = false;
    unlockPageScroll();
  }, 270);
}

function closeSheets(immediate = false) {
  blurActiveElement();
  const forceImmediate = immediate === true;
  if (state.closingSheet && !forceImmediate) return;
  const sheets = getOpenSheets();
  const backdrop = $('sheetBackdrop');

  if (!sheets.length) {
    backdrop.classList.add('hidden');
    backdrop.classList.remove('is-closing');
    backdrop.style.removeProperty('opacity');
    state.selected = null;
    unlockPageScroll();
    return;
  }

  state.selected = null;

  if (forceImmediate) {
    backdrop.classList.add('hidden');
    backdrop.classList.remove('is-closing');
    backdrop.style.removeProperty('opacity');
    sheets.forEach((sheet) => finishClose(sheet));
    state.closingSheet = false;
    unlockPageScroll();
    return;
  }

  state.closingSheet = true;
  backdrop.classList.add('is-closing');
  const animations = sheets.map((sheet) => new Promise((resolve) => {
    sheet.classList.remove('is-dragging');
    sheet.classList.add('is-closing');
    const content = sheet.querySelector('.sheet-content');
    content?.style.removeProperty('transition');
    content?.style.removeProperty('transform');
    let done = false;
    const finish = () => {
      if (done) return;
      done = true;
      finishClose(sheet);
      resolve();
    };
    content?.addEventListener('animationend', finish, { once: true });
    setTimeout(finish, 330);
  }));

  Promise.all(animations).finally(() => {
    backdrop.classList.add('hidden');
    backdrop.classList.remove('is-closing');
    backdrop.style.removeProperty('opacity');
    state.closingSheet = false;
    unlockPageScroll();
  });
}

function openReminderSheet(reminder = null) {
  closeSheets(true);
  applyDateInputMode();
  applyTimeInputMode();
  state.editing = reminder;
  $('sheetTitle').textContent = reminder ? t('editReminder') : t('newReminder');
  $('sheetError').textContent = '';
  $('reminderTitle').value = reminder?.title || '';
  if (reminder) {
    const due = new Date(reminderEffectiveDue(reminder));
    setReminderDateMin(dateInputValue(new Date()));
    setReminderDateValue(dateInputValue(due));
    setReminderTimeValue(timeInputValue(due));
    setPriority(reminder.priority);
  } else {
    setDefaultReminderTime();
    setPriority(1);
  }
  openBackdrop();
  $('reminderSheet').classList.remove('hidden');
  if (shouldAutoFocusSheet()) {
    setTimeout(() => $('reminderTitle').focus(), 80);
  }
}

function openActionSheet(reminder) {
  closeSheets(true);
  state.selected = reminder;
  $('actionTitle').textContent = reminder.title || 'NNotify';
  $('actionMeta').textContent = `${formatDateTime(reminderEffectiveDue(reminder))} · ${priorityLabel(reminder.priority)}`;
  openBackdrop();
  $('actionSheet').classList.remove('hidden');
}

function openSettings() {
  closeSheets(true);
  $('currentUser').textContent = state.username;
  openBackdrop();
  $('settingsSheet').classList.remove('hidden');
}

function formDueUtc() {
  const date = getReminderDateValue();
  const time = getReminderTimeValue();
  if (!date || !time) return NaN;
  return new Date(`${date}T${time}:00`).getTime();
}

async function saveReminder(event) {
  event.preventDefault();
  const submitButton = event.submitter || $('reminderForm').querySelector('button[type="submit"]');
  const dueAtUtc = formDueUtc();
  if (!Number.isFinite(dueAtUtc) || dueAtUtc <= Date.now()) {
    $('sheetError').textContent = t('futureRequired');
    return;
  }

  const payload = {
    title: $('reminderTitle').value.trim(),
    dueAtUtc,
    priority: state.priority
  };

  await runExclusive('save-reminder', submitButton, async () => {
    try {
      if (state.editing) {
        await api(`/v1/web/reminders/${encodeURIComponent(state.editing.id)}`, { method: 'PATCH', body: JSON.stringify(payload) });
      } else {
        await api('/v1/web/reminders', { method: 'POST', body: JSON.stringify(payload) });
      }
      closeSheets();
      await loadReminders();
    } catch (error) {
      await handleActionError(error, $('sheetError'));
    }
  });
}

async function ackReminder(id, button = null) {
  await runExclusive(`ack-${id}`, button, async () => {
    try {
      await api(`/v1/web/reminders/${encodeURIComponent(id)}/ack`, { method: 'POST', body: '{}' });
      closeSheets();
      await loadReminders();
    } catch (error) {
      await handleActionError(error);
    }
  });
}

async function snoozeReminder(id, minutes, button = null) {
  await runExclusive(`snooze-${id}-${minutes}`, button, async () => {
    try {
      await api(`/v1/web/reminders/${encodeURIComponent(id)}/snooze`, { method: 'POST', body: JSON.stringify({ minutes }) });
      closeSheets();
      await loadReminders();
    } catch (error) {
      await handleActionError(error);
    }
  });
}

async function deleteReminder(id, button = null) {
  if (!confirm(t('confirmDelete'))) return;
  await runExclusive(`delete-${id}`, button, async () => {
    try {
      await api(`/v1/web/reminders/${encodeURIComponent(id)}`, { method: 'DELETE' });
      closeSheets();
      await loadReminders();
    } catch (error) {
      await handleActionError(error);
    }
  });
}

function addMinutesToForm(minutes) {
  const base = Number.isFinite(formDueUtc()) ? new Date(formDueUtc()) : new Date();
  const next = new Date(Math.max(base.getTime(), Date.now()) + minutes * 60 * 1000);
  setReminderDateValue(dateInputValue(next));
  setReminderTimeValue(timeInputValue(next));
}

function isSheetDragGestureEnabled() {
  return window.matchMedia?.('(hover: none), (pointer: coarse)').matches || window.innerWidth <= 768;
}

function bindSheetGestures() {
  document.querySelectorAll('.sheet').forEach((sheet) => {
    const content = sheet.querySelector('.sheet-content');
    const dragHandle = sheet.querySelector('.sheet-handle');
    const dragHeader = sheet.querySelector('.sheet-header');
    const dragTitle = sheet.querySelector('.action-content > h2');
    if (!content || !dragHandle) return;

    const gesture = {
      active: false,
      pointerId: null,
      startY: 0,
      offsetY: 0,
      startedAt: 0
    };

    const beginDrag = (event) => {
      if (!isSheetDragGestureEnabled()) return;
      if (sheet.classList.contains('hidden') || state.closingSheet) return;
      if (event.pointerType === 'mouse' && event.button !== 0) return;
      if (event.target.closest('button, input, textarea, select')) return;
      event.preventDefault();
      gesture.active = true;
      gesture.pointerId = event.pointerId;
      gesture.startY = event.clientY;
      gesture.offsetY = 0;
      gesture.startedAt = performance.now();
      sheet.classList.add('is-dragging');
      content.style.transition = 'none';
      content.setPointerCapture?.(event.pointerId);
    };

    const moveDrag = (event) => {
      if (!gesture.active || gesture.pointerId !== event.pointerId) return;
      event.preventDefault();
      const delta = Math.max(0, event.clientY - gesture.startY);
      gesture.offsetY = delta;
      content.style.transform = `translate3d(0, ${delta}px, 0)`;
      $('sheetBackdrop').style.opacity = String(Math.max(0, 1 - delta / 320));
    };

    const endDrag = (event) => {
      if (!gesture.active || (event && gesture.pointerId !== event.pointerId)) return;
      event?.preventDefault?.();
      const distance = gesture.offsetY;
      const elapsed = Math.max(1, performance.now() - gesture.startedAt);
      const velocity = distance / elapsed;
      gesture.active = false;
      gesture.pointerId = null;
      content.releasePointerCapture?.(event?.pointerId);

      if (distance > 110 || velocity > 0.55) {
        requestCloseSheetFromDrag(sheet, distance);
        return;
      }

      sheet.classList.remove('is-dragging');
      content.style.transition = 'transform 240ms cubic-bezier(0.2, 0.8, 0.2, 1)';
      content.style.transform = 'translate3d(0, 0, 0)';
      $('sheetBackdrop').style.removeProperty('opacity');
      setTimeout(() => {
        if (!sheet.classList.contains('hidden')) {
          content.style.removeProperty('transform');
          content.style.removeProperty('transition');
        }
      }, 250);
    };

    [dragHandle, dragHeader, dragTitle].filter(Boolean).forEach((target) => {
      target.addEventListener('pointerdown', beginDrag);
    });
    content.addEventListener('pointermove', moveDrag);
    content.addEventListener('pointerup', endDrag);
    content.addEventListener('pointercancel', endDrag);
  });
}

function bindEvents() {
  $('loginForm').addEventListener('submit', async (event) => {
    event.preventDefault();
    const submitButton = event.submitter || $('loginForm').querySelector('button[type="submit"]');
    $('loginError').textContent = '';
    await runExclusive('login', submitButton, async () => {
      try {
        const data = await api('/v1/web/login', {
          method: 'POST',
          body: JSON.stringify({ username: $('loginUsername').value, password: $('loginPassword').value })
        });
        $('loginPassword').value = '';
        await showMain(data.username);
      } catch (error) {
        $('loginError').textContent = error?.status === 0 ? t('networkFailed') : t('loginFailed');
      }
    });
  });

  $('addButton').addEventListener('click', () => openReminderSheet());
  $('settingsButton').addEventListener('click', openSettings);
  $('sheetBackdrop').addEventListener('click', (event) => { if (event.target === event.currentTarget) requestCloseSheets(); });
  $('closeSheetButton').addEventListener('click', (event) => { event.preventDefault(); requestCloseSheets($('reminderSheet')); });
  $('cancelSheetButton').addEventListener('click', (event) => { event.preventDefault(); requestCloseSheets($('reminderSheet')); });
  $('closeSettingsButton')?.addEventListener('click', (event) => { event.preventDefault(); requestCloseSheets($('settingsSheet')); });
  $('closeDiscardButton')?.addEventListener('click', (event) => { event.preventDefault(); hideDiscardConfirm(); });
  $('continueDraftButton')?.addEventListener('click', (event) => { event.preventDefault(); hideDiscardConfirm(); });
  $('discardDraftButton')?.addEventListener('click', (event) => { event.preventDefault(); closeSheets(); });
  $('reminderForm').addEventListener('submit', saveReminder);
  $('reminderDate').addEventListener('change', () => {
    const iso = normalizeIsoDate($('reminderDate').value);
    if (iso) $('reminderDate').dataset.isoValue = iso;
  });
  $('reminderDate').addEventListener('pointerdown', (event) => {
    if (!isDesktopDatePickerEnabled()) return;
    event.preventDefault();
    if (state.datePicker.open) {
      state.suppressNextPickerFocus = 'date';
      hideDesktopDatePicker();
    } else {
      state.suppressNextPickerFocus = null;
      showDesktopDatePicker();
    }
  });
  $('reminderDate').addEventListener('focus', () => {
    if (!isDesktopDatePickerEnabled()) return;
    if (state.suppressNextPickerFocus === 'date') {
      state.suppressNextPickerFocus = null;
      return;
    }
    showDesktopDatePicker();
  });
  $('reminderTime').addEventListener('pointerdown', (event) => {
    if (!isDesktopTimePickerEnabled()) return;
    event.preventDefault();
    if (state.timePicker.open) {
      state.suppressNextPickerFocus = 'time';
      hideDesktopTimePicker();
    } else {
      state.suppressNextPickerFocus = null;
      showDesktopTimePicker();
    }
  });
  $('reminderTime').addEventListener('focus', () => {
    if (!isDesktopTimePickerEnabled()) return;
    if (state.suppressNextPickerFocus === 'time') {
      state.suppressNextPickerFocus = null;
      return;
    }
    showDesktopTimePicker();
  });
  document.addEventListener('pointerdown', (event) => {
    const reminderForm = $('reminderForm');
    const datePicker = $('desktopDatePicker');
    const timePicker = $('desktopTimePicker');
    const dateInput = $('reminderDate');
    const timeInput = $('reminderTime');
    const target = event.target;
    const dateOpen = datePicker && !datePicker.classList.contains('hidden');
    const timeOpen = timePicker && !timePicker.classList.contains('hidden');

    if (!dateOpen && !timeOpen) return;

    const isDateControl = target === dateInput || datePicker?.contains(target);
    const isTimeControl = target === timeInput || timePicker?.contains(target);

    if (isDateControl || isTimeControl) return;

    if (reminderForm?.contains(target)) {
      hideDesktopPickers();
      return;
    }

    hideDesktopPickers();
  }, true);
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') hideDesktopPickers();
  });
  window.addEventListener('resize', () => {
    applyDateInputMode();
    applyTimeInputMode();
    syncQuickMinuteLabels();
    positionDesktopDatePicker();
    positionDesktopTimePicker();
  }, { passive: true });
  $('historyToggle').addEventListener('click', () => $('historyList').classList.toggle('hidden'));

  document.querySelectorAll('[data-add-minutes]').forEach((button) => {
    button.addEventListener('click', () => addMinutesToForm(Number(button.dataset.addMinutes)));
  });
  document.querySelectorAll('[data-priority]').forEach((button) => {
    button.addEventListener('click', () => setPriority(Number(button.dataset.priority)));
  });
  document.querySelectorAll('[data-theme-choice]').forEach((button) => {
    button.addEventListener('click', () => setTheme(button.dataset.themeChoice));
  });
  window.matchMedia?.('(prefers-color-scheme: dark)').addEventListener?.('change', () => {
    if (state.theme === 'system') {
      setTheme('system');
    }
  });

  $('ackButton').addEventListener('click', (event) => state.selected && ackReminder(state.selected.id, event.currentTarget));
  $('snooze5Button').addEventListener('click', (event) => state.selected && snoozeReminder(state.selected.id, 5, event.currentTarget));
  $('snooze15Button').addEventListener('click', (event) => state.selected && snoozeReminder(state.selected.id, 15, event.currentTarget));
  $('editButton').addEventListener('click', () => state.selected && openReminderSheet(state.selected));
  $('deleteButton').addEventListener('click', (event) => state.selected && deleteReminder(state.selected.id, event.currentTarget));
  $('logoutButton').addEventListener('click', async (event) => {
    await runExclusive('logout', event.currentTarget, async () => {
      await api('/v1/web/logout', { method: 'POST', body: '{}' }).catch(() => null);
      closeSheets();
      await showLogin();
    });
  });
}

async function boot() {
  syncViewportHeight();
  applyTranslations();
  setTheme(state.theme);
  bindEvents();
  bindSheetGestures();
  applyDateInputMode();
  applyTimeInputMode();
  syncQuickMinuteLabels();
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js').catch(() => null);
  }
  try {
    const session = await api('/v1/web/session');
    await showMain(session.username);
  } catch {
    await showLogin();
  }
}

boot();
