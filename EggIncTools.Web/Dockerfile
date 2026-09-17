# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1 AS build
WORKDIR /src
ENV NUGET_PACKAGES=/root/.nuget/packages

COPY nuget.config global.json Directory.Build.props Directory.Build.targets Directory.Packages.props .editorconfig ./
COPY EggIncTools.Shell/EggIncTools.Shell.csproj EggIncTools.Shell/
COPY EggIncTools.Web.CssBuild/EggIncTools.Web.CssBuild.csproj EggIncTools.Web.CssBuild/
COPY EggIncTools.Web/EggIncTools.Web.csproj EggIncTools.Web/
ARG GITHUB_PACKAGES_USER
RUN --mount=type=secret,id=github_token \
    dotnet nuget update source github \
      --username "$GITHUB_PACKAGES_USER" \
      --password "$(cat /run/secrets/github_token)" \
      --store-password-in-clear-text \
      --configfile nuget.config \
    && dotnet restore EggIncTools.Web.CssBuild/EggIncTools.Web.CssBuild.csproj \
    && dotnet restore EggIncTools.Web/EggIncTools.Web.csproj

COPY EggIncTools.Shell/ EggIncTools.Shell/
COPY EggIncTools.Web.CssBuild/ EggIncTools.Web.CssBuild/
COPY EggIncTools.Web/ EggIncTools.Web/

ARG GIT_SHA
ARG APP_VERSION
RUN set -eux; \
    STAMP=""; \
    [ -n "$GIT_SHA" ] && STAMP="$STAMP -p:SourceRevisionId=$GIT_SHA"; \
    [ -n "$APP_VERSION" ] && STAMP="$STAMP -p:MinVerVersionOverride=$APP_VERSION"; \
    dotnet publish EggIncTools.Web/EggIncTools.Web.csproj -c Release -o /app/publish $STAMP; \
    test -s /app/publish/wwwroot/styles.css

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.1 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ARG GIT_SHA
ENV GIT_SHA=$GIT_SHA
EXPOSE 8090
ENTRYPOINT ["dotnet", "EggIncTools.Web.dll"]
