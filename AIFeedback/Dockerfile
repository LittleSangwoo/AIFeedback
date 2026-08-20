# Базовый образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Копируем сертификаты (переименовываем .cer в .crt для корректной установки в Linux)
COPY ["AIFeedback/Certificates/russian_trusted_root_ca.cer", "/usr/local/share/ca-certificates/russian_trusted_root_ca.crt"]
COPY ["AIFeedback/Certificates/russian_trusted_sub_ca.cer", "/usr/local/share/ca-certificates/russian_trusted_sub_ca.crt"]
RUN apt-get update && apt-get install -y ca-certificates && update-ca-certificates

USER $APP_UID

# Образ для сборки проекта
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AIFeedback/AIFeedback.csproj", "AIFeedback/"]
RUN dotnet restore "./AIFeedback/AIFeedback.csproj"
COPY . .
WORKDIR "/src/AIFeedback"
RUN dotnet build "./AIFeedback.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AIFeedback.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Финальная сборка
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Выдаем права пользователю app на запись в папку /app (критично для работы локальной SQLite базы)
USER root
RUN chown -R app:app /app

USER app
ENTRYPOINT ["dotnet", "AIFeedback.dll"]