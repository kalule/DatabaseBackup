# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DatabaseBackup.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish DatabaseBackup.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install PostgreSQL client and SQL Server tools
RUN apt-get update && \
    apt-get install -y curl gnupg lsb-release apt-transport-https && \
    curl -sSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /usr/share/keyrings/postgresql.gpg && \
    echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] http://apt.postgresql.org/pub/repos/apt/ $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list && \
    curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - && \
    curl https://packages.microsoft.com/config/debian/10/prod.list > /etc/apt/sources.list.d/mssql-release.list && \
    apt-get update && \
    ACCEPT_EULA=Y apt-get install -y postgresql-client-17 msodbcsql17 mssql-tools unixodbc && \
    ln -s /opt/mssql-tools/bin/sqlcmd /usr/bin/sqlcmd && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "DatabaseBackup.dll"]
