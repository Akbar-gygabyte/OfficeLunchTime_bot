# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем всё в контейнер
COPY . .

# Восстанавливаем зависимости и публикуем приложение
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Используем легкий образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Копируем собранные файлы из предыдущего этапа
COPY --from=build /app/out .

# Указываем команду для запуска бота
ENTRYPOINT ["dotnet", "OfficeLunchBot.dll"]
