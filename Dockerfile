# Используем .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копируем всё и собираем
COPY . .
RUN dotnet publish -c Release -o out

# Финальный образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Указываем команду запуска
ENTRYPOINT ["dotnet", "OfficeLunchBot.dll"]

