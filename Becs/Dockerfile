FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
RUN mkdir -p /data && chown -R app:app /data

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "Becs.csproj"
RUN dotnet publish "Becs.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# ensure the app runs as non-root
USER app

# writable data dir
RUN mkdir -p /data && chown -R app:app /data
USER app
ENV ConnectionStrings__DefaultConnection="Data Source=/data/becs.db"
ENV ASPNETCORE_URLS=http://0.0.0.0:8080


ENTRYPOINT ["dotnet", "Becs.dll"]
