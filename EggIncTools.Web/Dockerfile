# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.2 AS build
WORKDIR /src
ENV NUGET_PACKAGES=/root/.nuget/packages

COPY nuget.config global.json Directory.Build.props Directory.Build.targets Directory.Packages.props .editorconfig ./
COPY EggIncTools.Web/EggIncTools.Web.csproj EggIncTools.Web/
ARG GITHUB_PACKAGES_USER
RUN --mount=type=secret,id=github_token \
    dotnet nuget update source github \
      --username "$GITHUB_PACKAGES_USER" \
      --password "$(cat /run/secrets/github_token)" \
      --store-password-in-clear-text \
      --configfile nuget.config \
    && dotnet restore EggIncTools.Web/EggIncTools.Web.csproj

COPY EggIncTools.Web/ EggIncTools.Web/

ARG BUILD_HEIGHT
ARG BUILD_SHA
RUN BUILD_HEIGHT="$BUILD_HEIGHT" BUILD_SHA="$BUILD_SHA" \
    dotnet publish EggIncTools.Web/EggIncTools.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.2 AS runtime
ARG BUILD_HEIGHT
ARG BUILD_SHA
ENV GIT_SHA=$BUILD_SHA
ENV APP_VERSION=${BUILD_HEIGHT}.0.0
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8090
ENTRYPOINT ["dotnet", "EggIncTools.Web.dll"]
