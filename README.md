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

public class HelloCommand : IBotCommand
{
    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct)
    {
        await context.MessagingPlatform.SendTextAsync(context.ChatId, "Привет, мир!", ct: ct);
    }
}
```

Затем зарегистрируйте команду в DI контейнере с нужным ключом (например, `"hello"` для обработки `/hello`):

```csharp
// Program.cs
builder.Services.AddKeyedScoped<IBotCommand, HelloCommand>("hello");
```

## Работа с диалогами (Состояния)

Для построения диалога используйте сервис сессий из `BotContext`. Пример команды, которая спрашивает имя и ждет ответа:

```csharp
public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct)
{
    var state = await context.Sessions.GetStateAsync(context.UserId, ct);
    
    if (state == null)
    {
        // Начало диалога
        await context.MessagingPlatform.SendTextAsync(context.ChatId, "Как тебя зовут?", ct: ct);
        
        // Переводим пользователя в состояние ожидания ввода
        // Следующее сообщение будет отправлено в ЭТУ ЖЕ команду
        await context.Sessions.SetStateAsync(context.UserId, new UserDialogState
        {
            UserId = context.UserId,
            AwaitingInputFor = "ask_name" // Ключ этой команды
        }, ct);
    }
    else
    {
        // Продолжение диалога
        string name = message.Text;
        await context.MessagingPlatform.SendTextAsync(context.ChatId, $"Приятно познакомиться, {name}!", ct: ct);
        
        // Очищаем состояние
        await context.Sessions.ClearStateAsync(context.UserId, ct);
    }
}
```

## Запуск в Docker (с поддержкой сертификатов Минцифры РФ)

Проект полностью готов к деплою через Docker. В корне репозитория находятся файлы `.env.example`, `docker-compose.example.yml` и `Dockerfile` в проекте `BotEngine.Example`.

### Доверенные сертификаты
В `Dockerfile` **уже вшита автоматическая установка корневых сертификатов Минцифры РФ**. Это гарантирует, что при обращении к российским государственным API, платформе Max или другим защищенным отечественным сервисам из контейнера по HTTPS не возникнет ошибок недоверенного сертификата (SSL Certificate Validation).

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
