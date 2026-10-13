# Начало работы

## Требования

- .NET 10 SDK;
- Git;
- токен Telegram-бота от [BotFather](https://t.me/BotFather);
- Docker — только для контейнерного запуска;
- Redis — только если выбран `RedisUserSessionStore`.

## Клонирование

MAX SDK хранится в Git-сабмодуле. Клонируйте репозиторий вместе с ним:

```bash
git clone --recurse-submodules https://github.com/kanl222/BotEngine.git
cd BotEngine
```

Если репозиторий уже клонирован:

```bash
git submodule update --init --recursive
```

Проверка: файл `external/max-bot-dotnet/MAX.Bot/MAX.Bot.csproj` должен
существовать. Без него `BotEngine.Max`, `BotEngine.Example` и всё решение не
соберутся.

## Настройка Telegram

В проекте примера включено хранилище user secrets. Запишите токен вне исходного
кода:

```bash
dotnet user-secrets set "Telegram:Token" "<токен>" --project BotEngine.Example
dotnet user-secrets set "BotEngine:InstanceId" "my-bot" --project BotEngine.Example
```

Запустите приложение из корня репозитория:

```bash
dotnet run --project BotEngine.Example
```

Проверьте команды:

1. `/start` возвращает клавиатуру.
2. `/ping` отвечает `Pong!`.
3. `/echo` запрашивает следующее сообщение и повторяет его.

## Подключение MAX

1. Задайте секрет:

   ```bash
   dotnet user-secrets set "Max:Token" "<токен>" --project BotEngine.Example
   ```

2. Раскомментируйте регистрацию в `BotEngine.Example/Program.cs`:

   ```csharp
   builder.Services.AddMax();
   ```

3. Запустите `BotEngine.Example` обычной командой `dotnet run`.

Telegram и MAX можно зарегистрировать одновременно. Команды создаются один раз
и получают адаптер платформы через `BotContext`.

## Создание команды

Команда реализует `IBotCommand` и регистрируется в DI с совпадающим строковым
ключом:

```csharp
public sealed class HelpCommand : IBotCommand
{
    public string Name => "help";

    public Task ExecuteAsync(
        BotContext context,
        IncomingMessage message,
        CancellationToken ct = default)
        => context.ReplyAsync("Доступны /start, /ping и /echo", ct: ct);
}

builder.Services.AddKeyedScoped<IBotCommand, HelpCommand>("help");
```

Для callback используйте payload вида `command:arguments`. Диспетчер берёт часть
до первого двоеточия как ключ команды. Разбор оставшихся аргументов выполняет
сама команда.

## Многошаговый диалог

```csharp
var state = await context.GetSessionAsync(ct);

if (state is null)
{
    await context.ReplyAsync("Как вас зовут?", ct: ct);
    await context.SetSessionAsync(Name, ct: ct);
    return;
}

await context.ReplyAsync($"Здравствуйте, {message.Text}!", ct: ct);
await context.ClearSessionAsync(ct);
```

По умолчанию состояние хранится в памяти 10 минут. Для нескольких экземпляров
приложения используйте Redis, описанный в [конфигурации](configuration.md#redis).
