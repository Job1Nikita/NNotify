# NNotify

[Русская версия](#ru) | [English Version](#en)

---

<a id="ru"></a>
## Русская версия

**Быстрые ссылки:**
[Описание](#ru-overview) | [Скриншоты](#ru-screenshots) | [Возможности](#ru-features) | [Требования](#ru-requirements) | [Запуск](#ru-run) | [Сборка](#ru-build) | [Telegram](#ru-telegram) | [Безопасность](#ru-security)

<a id="ru-overview"></a>
### Описание

NNotify — десктопное приложение напоминаний для Windows (WPF).  
Приложение помогает не пропускать задачи: хранит их локально, показывает оверлей-уведомления и при необходимости отправляет эскалацию в Telegram.

<a id="ru-screenshots"></a>
### Скриншоты

**Главное окно**  
![Главное окно](docs/images/Main_Windows.png)

**Добавление напоминания**  
![Окно добавления напоминания](docs/images/Add_Notification.png)

**Настройки**  
![Окно настроек](docs/images/Panel_Settings.png)

<a id="ru-features"></a>
### Возможности

- Локальные напоминания с датой, временем и приоритетом.
- Быстрые кнопки времени: `+3`, `+5`, `+10` минут.
- Разделы: ближайшие, пропущенные при запуске, история.
- Оверлей с подтверждением/переносом/удалением.
- Эскалация в Telegram, если напоминание пропущено на ПК.
- Глобальная горячая клавиша.
- Автозапуск вместе с Windows.
- Светлая и темная темы.

<a id="ru-requirements"></a>
### Требования

- Windows 10/11 x64
- .NET Desktop Runtime 8.0

<a id="ru-run"></a>
### Быстрый запуск (из исходников)

```powershell
cd C:\NNotify_v3\NNotify
dotnet restore
dotnet run
```

<a id="ru-build"></a>
### Сборка и публикация

```powershell
cd C:\NNotify_v3\NNotify
dotnet build NNotify.csproj -c Release
.\Publish-SingleExe.ps1
```

Результат:
- `artifacts\singlefile\NNotify.exe`
- обычно около `2.8–3.0 MB`

<a id="ru-telegram"></a>
### Настройка Telegram

1. Откройте `Настройки`.
2. Укажите `Токен бота`.
3. Заполните только одно поле: `ID чата` или `ID пользователя`.
4. Нажмите `Тест Telegram`.

<a id="ru-security"></a>
### Хранение данных и безопасность

- Настройки: `%LOCALAPPDATA%\NNotify\settings.json`
- Чувствительные значения (например, Telegram token) защищаются DPAPI (`CurrentUser`).
- Логи: `%LOCALAPPDATA%\NNotify\log.txt`

---

<a id="en"></a>
## English Version

**Quick Links:**
[Overview](#en-overview) | [Screenshots](#en-screenshots) | [Features](#en-features) | [Requirements](#en-requirements) | [Run](#en-run) | [Build](#en-build) | [Telegram](#en-telegram) | [Security](#en-security)

<a id="en-overview"></a>
### Overview

NNotify is a desktop reminder app for Windows (WPF).  
It helps you avoid missed tasks with local scheduling, overlay reminders, and optional Telegram escalation.

<a id="en-screenshots"></a>
### Screenshots

**Main window**  
![Main window](docs/images/Main_Windows.png)

**Add reminder**  
![Add reminder window](docs/images/Add_Notification.png)

**Settings**  
![Settings window](docs/images/Panel_Settings.png)

<a id="en-features"></a>
### Features

- Local reminders with date, time, and priority.
- Quick time buttons: `+3`, `+5`, `+10` minutes.
- Sections: upcoming, missed on startup, history.
- Overlay actions: confirm, snooze, delete.
- Telegram escalation when reminders are missed.
- Global hotkey support.
- Windows startup integration.
- Light and dark themes.

<a id="en-requirements"></a>
### Requirements

- Windows 10/11 x64
- .NET Desktop Runtime 8.0

<a id="en-run"></a>
### Quick Start (from source)

```powershell
cd C:\NNotify_v3\NNotify
dotnet restore
dotnet run
```

<a id="en-build"></a>
### Build and Publish

```powershell
cd C:\NNotify_v3\NNotify
dotnet build NNotify.csproj -c Release
.\Publish-SingleExe.ps1
```

Output:
- `artifacts\singlefile\NNotify.exe`
- typically around `2.8–3.0 MB`

<a id="en-telegram"></a>
### Telegram Setup

1. Open `Settings`.
2. Enter your bot token.
3. Fill only one target: `Chat ID` or `User ID`.
4. Click `Test Telegram`.

<a id="en-security"></a>
### Data Storage and Security

- Settings file: `%LOCALAPPDATA%\NNotify\settings.json`
- Sensitive values (e.g., Telegram token) are protected with DPAPI (`CurrentUser`).
- Logs: `%LOCALAPPDATA%\NNotify\log.txt`
