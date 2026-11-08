# Build from the repository root: docker build -f backend/UniversityHelpdesk.Api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY backend/UniversityHelpdesk.Api/UniversityHelpdesk.Api.csproj backend/UniversityHelpdesk.Api/
RUN dotnet restore backend/UniversityHelpdesk.Api/UniversityHelpdesk.Api.csproj
COPY backend/UniversityHelpdesk.Api/ backend/UniversityHelpdesk.Api/
RUN dotnet publish backend/UniversityHelpdesk.Api/UniversityHelpdesk.Api.csproj \
    --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
# The same image serves HTTP by default and runs one-off commands such as migrate-database.
ENTRYPOINT ["dotnet", "UniversityHelpdesk.Api.dll"]
