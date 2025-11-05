# Используем .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем все файлы
COPY . ./
# Восстанавливаем зависимости
RUN dotnet restore ./OfficeLunchBot/OfficeLunchBot.csproj
# Собираем проект
RUN dotnet publish ./OfficeLunchBot/OfficeLunchBot.csproj -c Release -o out

ENV BOT_TOKEN=${BOT_TOKEN}

# --- Запуск ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "OfficeLunchBot.dll"]



