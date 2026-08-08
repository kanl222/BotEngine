FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

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

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем файлы проектов для восстановления зависимостей
COPY ["BotEngine.Example/BotEngine.Example.csproj", "BotEngine.Example/"]
COPY ["BotEngine.Core/BotEngine.Core.csproj", "BotEngine.Core/"]
COPY ["BotEngine.Telegram/BotEngine.Telegram.csproj", "BotEngine.Telegram/"]
COPY ["BotEngine.Max/BotEngine.Max.csproj", "BotEngine.Max/"]
RUN dotnet restore "BotEngine.Example/BotEngine.Example.csproj"

# Копируем все исходники и собираем приложение
COPY . .
WORKDIR "/src/BotEngine.Example"
RUN dotnet build "BotEngine.Example.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BotEngine.Example.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BotEngine.Example.dll"]
