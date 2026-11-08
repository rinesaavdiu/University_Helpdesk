# Build from the repository root: docker build -f backend/UniversityHelpdesk.NotificationWorker/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY backend/UniversityHelpdesk.NotificationWorker/UniversityHelpdesk.NotificationWorker.csproj backend/UniversityHelpdesk.NotificationWorker/
RUN dotnet restore backend/UniversityHelpdesk.NotificationWorker/UniversityHelpdesk.NotificationWorker.csproj
COPY backend/UniversityHelpdesk.NotificationWorker/ backend/UniversityHelpdesk.NotificationWorker/
RUN dotnet publish backend/UniversityHelpdesk.NotificationWorker/UniversityHelpdesk.NotificationWorker.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "UniversityHelpdesk.NotificationWorker.dll"]
