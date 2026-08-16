# BotEngine

BotEngine — это современный, расширяемый и модульный C# фреймворк для создания ботов для различных мессенджеров (Telegram, Max и других). Построен на базе .NET 10 с использованием паттерна Command Dispatcher и Keyed Dependency Injection.

## Особенности архитектуры

- **Платформонезависимость**: Бизнес-логика бота пишется один раз и работает сразу на всех подключенных платформах (благодаря абстракциям `IMessagingPlatform` и `BotContext`).
- **Модульность**: Ядро (`BotEngine.Core`) не зависит от конкретных мессенджеров. Адаптеры (например, `BotEngine.Telegram`) подключаются отдельно.
- **Маршрутизация команд**: Встроенный диспетчер команд автоматически обрабатывает входящие текстовые сообщения, нажатия кнопок (callbacks) и поддерживает состояния (User Sessions).
- **Машина состояний (FSM)**: Встроенный механизм пользовательских сессий (`IUserSessionStore`) для построения многошаговых диалогов (ожидание ответа).
- **Middleware**: Поддержка промежуточного ПО для логирования, обработки ошибок или ограничения запросов (Rate Limiting) на уровне пайплайна.

## Структура решения

- **BotEngine.Core**: Ядро фреймворка. Содержит интерфейсы команд, диспетчер, модели и хранилище сессий.
- **BotEngine.Telegram**: Адаптер для работы с Telegram API (на базе `Telegram.Bot`).
- **BotEngine.Max**: Адаптер для работы с платформой Max.
- **BotEngine.Example**: Пример Worker Service приложения, показывающий, как инициализировать бота и писать команды.

## Быстрый старт

### 1. Настройка

В проекте `BotEngine.Example` откройте файл `appsettings.json` и добавьте свой токен Telegram-бота (полученный у [@BotFather](https://t.me/BotFather)):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Telegram": {
    "Token": "ВАШ_ТОКЕН_ЗДЕСЬ"
  }
}
```

### 2. Запуск примера

Перейдите в папку с примером и запустите его:

```bash
cd BotEngine.Example
dotnet run
```

Бот начнет опрашивать сервера Telegram. Зайдите в Telegram, найдите своего бота и отправьте ему `/start`.

### 3. Как написать свою команду

Команды реализуют интерфейс `IBotCommand`. Создайте класс вашей команды:

```csharp
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

public sealed class HelloCommand : IBotCommand
{
    public string Name => "hello";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        await context.ReplyAsync("Привет, мир!", ct: ct);
    }
}
```

Затем зарегистрируйте команду в DI контейнере с нужным ключом (например, `"hello"` для обработки `/hello`):

```csharp
// Program.cs
builder.Services.AddKeyedScoped<IBotCommand, HelloCommand>("hello");
```

## Работа с диалогами (Состояния)

Для построения диалога используйте методы сессий из `BotContext`. Пример команды, которая спрашивает имя и ждет ответа:

```csharp
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

public sealed class AskNameCommand : IBotCommand
{
    public string Name => "ask_name";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        var state = await context.GetSessionAsync(ct);

        if (state is null)
        {
            // Начало диалога: переводим пользователя в состояние ожидания ввода
            await context.ReplyAsync("Как тебя зовут?", ct: ct);
            await context.SetSessionAsync("ask_name", ct: ct);
        }
        else
        {
            // Продолжение диалога: обрабатываем ввод и очищаем сессию
            var name = message.Text;
            await context.ReplyAsync($"Приятно познакомиться, {name}!", ct: ct);
            await context.ClearSessionAsync(ct);
        }
    }
}
```

## Архитектурные решения (ADR)

Ключевые технические решения задокументированы в формате [Architecture Decision Records](docs/decisions/):

- [ADR-001: Platform-Agnostic Architecture (Ports & Adapters)](docs/decisions/ADR-001-Platform-Agnostic-Architecture.md)
- [ADR-002: Keyed Scoped DI and Command Dispatcher Pipeline](docs/decisions/ADR-002-Keyed-DI-Command-Routing.md)
- [ADR-003: Finite State Machine (FSM) and User Session Storage](docs/decisions/ADR-003-Finite-State-Machine-Session-Storage.md)

## Запуск в Docker (с поддержкой сертификатов Минцифры РФ)

Проект полностью готов к деплою через Docker. В корне репозитория находятся файлы `.env.example`, `docker-compose.example.yml` и `Dockerfile` в проекте `BotEngine.Example`.

### Доверенные сертификаты (Для платформы MAX)

Если вы планируете **подключать MAX Bot**, вам необходимо обеспечить доверие к российским сертификатам внутри контейнера. В предоставленном `Dockerfile` примера эта настройка уже сделана.

Если вы пишете свой `Dockerfile`, **обязательно вставьте следующий код** на этапе сборки базового образа (например, после `FROM mcr.microsoft.com/dotnet/runtime:...`):

```dockerfile
# Установка доверенных корневых сертификатов Минцифры РФ (для работы по защищенному SSL)
RUN apt-get update && \
    apt-get install -y --no-install-recommends ca-certificates curl && \
    curl -fsSL -o /usr/local/share/ca-certificates/russian_trusted_root_ca.crt \
      https://gu-st.ru/content/lending/russian_trusted_root_ca_pem.crt && \
    curl -fsSL -o /usr/local/share/ca-certificates/russian_trusted_sub_ca.crt \
      https://gu-st.ru/content/lending/russian_trusted_sub_ca_pem.crt && \
    update-ca-certificates && \
    apt-get purge -y curl && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*
```

Без этого скрипта обращения к API платформы MAX будут падать с ошибкой проверки SSL-сертификата.

### Как запустить:
1. Скопируйте `.env.example` в `.env` и впишите туда ваши токены:
   ```bash
   cp .env.example .env
   ```
2. Скопируйте `docker-compose.example.yml` в `docker-compose.yml`:
   ```bash
   cp docker-compose.example.yml docker-compose.yml
   ```
3. Запустите контейнеры:
   ```bash
   docker-compose up -d --build
   ```

## Лицензия

MIT License.
