# syntax=docker/dockerfile:1

# ---- Build stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GBS_Web.csproj .
RUN dotnet restore GBS_Web.csproj

COPY . .
RUN dotnet publish GBS_Web.csproj -c Release -o /app/publish --no-restore

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
