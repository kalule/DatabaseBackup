# Use the .NET 8 SDK to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the .csproj first to leverage Docker layer caching
COPY DatabaseBackup.csproj ./
RUN dotnet restore DatabaseBackup.csproj

# Copy the rest of the project files
COPY . ./
RUN dotnet publish DatabaseBackup.csproj -c Release -o /app/publish

# Use the .NET 8 ASP.NET runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install PostgreSQL and SQL Server tools
#RUN apt-get update && \
    #apt-get install -y gnupg postgresql-client curl apt-transport-https && \
    #curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - && \
    #curl https://packages.microsoft.com/config/debian/10/prod.list > /etc/apt/sources.list.d/mssql-release.list && \
    #apt-get update && \
    #ACCEPT_EULA=Y apt-get install -y msodbcsql17 mssql-tools && \
    #ln -sfn /opt/mssql-tools/bin/sqlcmd /usr/bin/sqlcmd && \
    #apt-get clean
#
    #RUN apt-get update && \
    #apt-get install -y curl gnupg lsb-release && \
    #curl -sSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /usr/share/keyrings/postgresql.gpg && \
    #echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] http://apt.postgresql.org/pub/repos/apt/ $(lsb_release -cs)-pgdg main" \
        #> /etc/apt/sources.list.d/pgdg.list && \
    #apt-get update && \
    #apt-get install -y postgresql-client-17

    RUN apt-get update && \
    apt-get install -y curl gnupg lsb-release apt-transport-https && \
    curl -sSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | gpg --dearmor -o /usr/share/keyrings/postgresql.gpg && \
    echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] http://apt.postgresql.org/pub/repos/apt/ $(lsb_release -cs)-pgdg main" \
        > /etc/apt/sources.list.d/pgdg.list && \
    apt-get update && \
    apt-get install -y postgresql-client-17 && \
    curl https://packages.microsoft.com/keys/microsoft.asc | apt-key add - && \
    curl https://packages.microsoft.com/config/debian/10/prod.list > /etc/apt/sources.list.d/mssql-release.list && \
    apt-get update && \
    ACCEPT_EULA=Y apt-get install -y msodbcsql17 mssql-tools && \
    ln -sfn /opt/mssql-tools/bin/sqlcmd /usr/bin/sqlcmd && \
    apt-get clean



# Copy the build artifacts from the previous stage
COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "DatabaseBackup.dll"]
