# Разработка и диагностика

## Сборка

Из корня репозитория:

```bash
git submodule update --init --recursive
dotnet restore BotEngine.slnx
dotnet build BotEngine.slnx --no-restore
```

Ядро и Telegram можно проверить независимо от MAX SDK:

```bash
dotnet build BotEngine.Core/BotEngine.Core.csproj
dotnet build BotEngine.Telegram/BotEngine.Telegram.csproj
```

Unit-тесты маршрутизации и ключей сессий находятся в `BotEngine.Core.Tests`:

```bash
dotnet test BotEngine.Core.Tests/BotEngine.Core.Tests.csproj
```

Workflow `.github/workflows/ci.yml` выполняет те же шаги на GitHub Actions. Он
клонирует сабмодули рекурсивно и завершает работу ошибкой, если сабмодуль не
инициализирован, изменён или находится не на закреплённом коммите.

## Docker

Шаблоны `.env.example`, `docker-compose.example.yml` и
`BotEngine.Example/Dockerfile` предназначены для примера, а не для готового
production-развёртывания.

Перед сборкой обязательно инициализируйте MAX-сабмодуль. Dockerfile копирует его
проект до `dotnet restore`, поэтому образ можно проверить из корня репозитория:

```bash
docker build --file BotEngine.Example/Dockerfile --tag botengine-example .
```

Образ устанавливает российские корневые сертификаты, необходимые текущей
интеграции MAX. Перед production-развёртыванием отдельно проверьте источник,
срок действия и принятую в вашей организации политику доверия сертификатам.

## Типовые ошибки

### Не найден `MAX.Bot.csproj`

Признаки: `MSB3202`, ошибки пространства имён `MAX` или сообщение о пропущенном
проекте.

```bash
git submodule update --init --recursive
```

После команды проверьте наличие
`external/max-bot-dotnet/MAX.Bot/MAX.Bot.csproj`.

### Telegram отклоняет токен

Убедитесь, что `Telegram:Token` переопределяет шаблон из `appsettings.json`:

```bash
dotnet user-secrets list --project BotEngine.Example
```

Не вставляйте токен в issue, лог или команду, вывод которой будет опубликован.

### Зарегистрированная команда прерывает диалог

Это соответствует порядку маршрутизации: callback и зарегистрированная
slash-команда проверяются до активной сессии. Так `/start` или `/help` могут быть
командами выхода. Команда должна явно очистить сессию, если сценарий завершён.

### Ошибка Redis завершает обработку сообщения

`RedisUserSessionStore` работает в режиме fail-fast: логирует и повторно
выбрасывает исключение. Проверьте соединение Redis и записи категории
`BotEngine.Core.Services.RedisUserSessionStore`; не трактуйте сбой как отсутствие
состояния.

## Перед изменением публичного API

1. Обновите пример использования и соответствующий раздел документации.
2. Проверьте сборку Core и обоих адаптеров.
3. Если изменение затрагивает архитектурное решение, добавьте новый ADR вместо
   переписывания причин старого решения.
4. Не добавляйте реальные токены и локальные файлы конфигурации.
