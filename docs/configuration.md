# Конфигурация

.NET загружает настройки из `appsettings.json`, user secrets и переменных
окружения. Переменные окружения используют двойное подчёркивание вместо `:`:
`Telegram__Token` соответствует `Telegram:Token`.

## Параметры

| Ключ | Обязателен | Значение по умолчанию | Использование |
| --- | --- | --- | --- |
| `BotEngine:InstanceId` | нет | `default` | Изоляция сессий разных ботов или арендаторов |
| `Telegram:Token` | при вызове `AddTelegram()` | — | Токен Telegram Bot API |
| `Max:Token` | при вызове `AddMax()` | — | Токен MAX Bot API |
| `Max:TimeoutSeconds` | нет | `30` | Таймаут HTTP-клиента MAX |
| `BotEngine:RateLimit:MaxMessages` | нет | `30` | Число сообщений на пользователя в окне |
| `BotEngine:RateLimit:WindowSeconds` | нет | `60` | Размер окна rate limiter |
| `BotEngine:RateLimit:ThrottleMessage` | нет | встроенный текст | Ответ при превышении лимита; `null` отключает ответ |

Rate limiter не включается автоматически. Его нужно зарегистрировать после ядра:

```csharp
builder.Services.AddBotEngine();
builder.Services.AddRateLimiting();
```

Для production задайте стабильный и уникальный `BotEngine:InstanceId`. Смена
значения создаёт новое пространство сессий, поэтому старые состояния перестанут
находиться.

Настройки можно задать кодом:

```csharp
builder.Services.AddRateLimiting(options =>
{
    options.MaxMessages = 20;
    options.WindowSeconds = 60;
});
```

## Локальные секреты

Для примера используйте Secret Manager:

```bash
dotnet user-secrets set "Telegram:Token" "<токен>" --project BotEngine.Example
dotnet user-secrets set "Max:Token" "<токен>" --project BotEngine.Example
dotnet user-secrets list --project BotEngine.Example
```

Файл `appsettings.json` содержит только шаблонные значения. Не заменяйте их
реальными токенами в отслеживаемом файле.

## Переменные окружения

Пример для PowerShell:

```powershell
$env:Telegram__Token = "<токен>"
dotnet run --project BotEngine.Example
```

Пример для Bash:

```bash
Telegram__Token='<токен>' dotnet run --project BotEngine.Example
```

## Redis

По умолчанию `AddBotEngine()` регистрирует `InMemoryUserSessionStore`. Он подходит
для разработки и единственного экземпляра приложения. Для общей сессии между
несколькими экземплярами зарегистрируйте Redis после `AddBotEngine()`:

```csharp
builder.Services.AddBotEngine();

var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis не задан");

builder.Services.AddRedisSessionStore(redisConnection);
```

Конфигурация:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

Эквивалентная переменная окружения:

```text
ConnectionStrings__Redis=redis:6379
```

Redis-ключ имеет префикс `bot:session:v2:`. Ошибки чтения, записи и удаления
логируются и затем передаются вызывающему коду: потеря соединения больше не
выглядит как пустая сессия. Операции также учитывают переданный
`CancellationToken`.

## Правила работы с секретами

- не добавляйте `.env`, `secrets.json` и локальные `appsettings.*.json` в Git;
- передавайте секреты контейнеру через окружение или хранилище секретов;
- не публикуйте вывод `dotnet user-secrets list`;
- при случайном коммите токена отзовите его у провайдера, а не только удалите из
  истории Git.
