# syntax=docker/dockerfile:1

# ---- Build stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GBS_Web.csproj .
RUN dotnet restore GBS_Web.csproj

COPY . .
# NOT --no-restore: the early restore above only saw GBS_Web.csproj (for
# layer caching), before wwwroot/Components existed. Publishing with
# --no-restore then skips re-evaluating static web assets against the real
# source tree, silently dropping the Blazor JS runtime (_framework/
# blazor.web.js) from the output — every server-side interactive control
# (buttons, filters, forms bound via @onclick/@bind) then does nothing,
# with no client-side error, because the SignalR circuit script never
# loads. Confirmed by reproducing the restore-then-copy-then-publish
# sequence locally: dropping --no-restore here fixes it.
RUN dotnet publish GBS_Web.csproj -c Release -o /app/publish

# ---- Runtime stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Oracle.ManagedDataAccess.Core, iText7 and ClosedXML are fully managed
# (no native OCI client / libgdiplus needed), so the plain aspnet runtime
# image is enough — no extra OS packages required.

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GBS_Web.dll"]
