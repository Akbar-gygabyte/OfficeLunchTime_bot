# Этап сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY . .
RUN dotnet restore ./OfficeLunchBot/OfficeLunchBot.csproj
RUN dotnet publish ./OfficeLunchBot/OfficeLunchBot.csproj -c Release -o out

# Этап запуска
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out ./
ENV BOT_TOKEN=8345872765:AAFCkGFu7Hlx0KG9r3lRIkjeTFQ5aPL15kU
ENTRYPOINT ["dotnet", "OfficeLunchBot.dll"]
