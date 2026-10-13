# Архитектура

BotEngine использует разделение на ядро и платформенные адаптеры. Команды и
состояния диалога не зависят от типов Telegram или MAX SDK.

```text
Telegram update ─► TelegramPlatformAdapter ─┐
                                            ├─► CommandDispatcher ─► IBotCommand
MAX update ──────► MaxPlatformAdapter ──────┘          │
                                                       ├─► IBotMiddleware
                                                       └─► IUserSessionStore
```

## Границы модулей

- `BotEngine.Core` содержит платформонезависимый API и не ссылается на адаптеры.
- `BotEngine.Telegram` преобразует модели Telegram Bot API в `IncomingMessage`.
- `BotEngine.Max` выполняет то же для MAX SDK.
- хост-приложение выбирает адаптеры и регистрирует команды.

Ключевые контракты:

- `IBotCommand` — обработчик команды;
- `ICommandDispatcher` — маршрутизация входящего сообщения;
- `IMessagingPlatform` — отправка и изменение сообщений и файлов;
- `IBotMiddleware` — сквозная обработка до выполнения команды;
- `IUserSessionStore` — состояние многошагового диалога.

## Жизненный цикл сообщения

Polling worker получает обновление, адаптер создаёт `IncomingMessage`, после чего
для обработки создаётся DI scope. В текущей реализации диспетчер проверяет:

1. `CallbackData`: часть до первого `:` используется как ключ команды.
2. Явную slash-команду, включая Telegram-форму `/command@bot_name`.
3. Активную сессию: сообщение получает команда из `AwaitingInputFor`.
4. Команду `ask`, затем `default` как fallback.
5. Если обработчик не найден, пользователю отправляется стандартный ответ.

Этот порядок позволяет `/start`, `/help` или другой зарегистрированной команде
прервать активный диалог. Неизвестная slash-команда не уничтожает сессию и может
быть обработана текущим сценарием.

## Команды и DI

Команды регистрируются как keyed scoped services:

```csharp
builder.Services.AddKeyedScoped<IBotCommand, StartCommand>("start");
```

`CommandFactory` разрешает команду из текущего scope. Это позволяет безопасно
внедрять в команды scoped-зависимости, например контекст базы данных.

Свойство `IBotCommand.Name` используется для диагностики и сценариев, но само по
себе не регистрирует команду. Ключ DI и `Name` следует держать одинаковыми.

## Middleware

Каждый зарегистрированный `IBotMiddleware` оборачивает следующий элемент
конвейера. Порядок соответствует порядку регистрации в `IServiceCollection`.

```csharp
builder.Services.AddBotEngine();
builder.Services.AddRateLimiting();
```

Middleware может завершить обработку, не вызывая `next`, как это делает rate
limiter при превышении лимита.

## Сессии

`BotContext.SetSessionAsync()` записывает имя команды и произвольный словарь
данных. Стандартный TTL — 10 минут.

Стандартный `ISessionKeyProvider` формирует версионированный ключ из
`InstanceId`, платформы, чата и пользователя. Это изолирует личные и групповые
диалоги, а также несколько ботов в одном Redis. Генератор можно заменить своей
реализацией через DI.

Доступны две реализации:

- `InMemoryUserSessionStore` — один процесс, фоновая очистка каждые 5 минут;
- `RedisUserSessionStore` — общее состояние нескольких экземпляров приложения.

## Возможности платформ

`IMessagingPlatform` описывает общий набор операций. Методы, не поддерживаемые
адаптером, по умолчанию завершаются с `NotSupportedException`. Команда, которая
использует редактирование, удаление или отправку фото, должна учитывать поддержку
операции целевой платформой.

История принятых решений находится в каталоге [decisions](decisions/README.md).

## Диагностика

Ядро публикует `ActivitySource` и `Meter` с именем `BotEngine.Core`. Доступны
метрики:

- `botengine.messages.received`;
- `botengine.messages.routed`;
- `botengine.commands.failed`;
- `botengine.dispatch.duration`.

Адаптеры задают `IncomingMessage.CorrelationId`, а polling workers и диспетчер
добавляют его в logging scope. Исходный текст и callback payload не записываются
в информационный лог; вместо них фиксируются длина, наличие callback и число
вложений.
