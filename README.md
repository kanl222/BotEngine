# BotEngine

BotEngine — модульный фреймворк на .NET 10 для ботов, работающих через несколько
мессенджеров. Команды зависят от общего API `BotEngine.Core`, а взаимодействие с
Telegram и MAX выполняют отдельные адаптеры.

## Возможности

- маршрутизация slash-команд и callback-кнопок через Keyed DI;
- многошаговые диалоги с хранением состояния в памяти или Redis;
- middleware-конвейер, включая встроенный rate limiter;
- текст, клавиатуры, геопозиция и файловые вложения;
- long polling для Telegram и MAX;
- общий код команд для обеих платформ.

## Состав решения

| Проект | Назначение |
| --- | --- |
| `BotEngine.Core` | Контракты, модели, диспетчер команд, middleware и хранилища сессий |
| `BotEngine.Core.Tests` | Unit-тесты маршрутизации и ключей сессий |
| `BotEngine.Telegram` | Адаптер и polling worker для Telegram |
| `BotEngine.Max` | Адаптер и polling worker для MAX |
| `BotEngine.Example` | Исполняемый пример с командами `/start`, `/ping` и `/echo` |

MAX SDK подключён как Git-сабмодуль. Поэтому полная сборка решения и запуск
`BotEngine.Example` требуют загруженного каталога `external/max-bot-dotnet`, даже
если `AddMax()` не включён.

## Быстрый старт

Понадобится [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) и Git.

```bash
git clone --recurse-submodules https://github.com/kanl222/BotEngine.git
cd BotEngine
dotnet user-secrets set "Telegram:Token" "<токен от BotFather>" --project BotEngine.Example
dotnet run --project BotEngine.Example
```

Для уже клонированного репозитория сначала загрузите сабмодуль:

```bash
git submodule update --init --recursive
```

После запуска отправьте боту `/start`. Токены не следует записывать в
`appsettings.json` или коммитить в репозиторий.

Подробная инструкция и решение типовых ошибок находятся в
[руководстве по запуску](docs/getting-started.md).

## Простая команда

```csharp
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

public sealed class HelloCommand : IBotCommand
{
    public string Name => "hello";

    public Task ExecuteAsync(
        BotContext context,
        IncomingMessage message,
        CancellationToken ct = default)
        => context.ReplyAsync("Привет!", ct: ct);
}
```

Команда регистрируется под тем же ключом, который возвращает `Name`:

```csharp
builder.Services.AddKeyedScoped<IBotCommand, HelloCommand>("hello");
```

После этого диспетчер направит `/hello` в `HelloCommand`.

## Документация

- [Начало работы](docs/getting-started.md)
- [Конфигурация и секреты](docs/configuration.md)
- [Архитектура и обработка сообщений](docs/architecture.md)
- [Разработка, сборка и известные ограничения](docs/development.md)
- [Архитектурные решения](docs/decisions/README.md)

## Статус проекта

Полная сборка зависит от MAX-сабмодуля. GitHub Actions проверяет состояние
сабмодулей, собирает решение, запускает unit-тесты и проверяет Docker-образ.
Команды для тех же проверок локально приведены в
[руководстве разработчика](docs/development.md).

## Лицензия

[MIT](LICENSE)
