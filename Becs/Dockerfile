# --- base runtime image ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# writable DB directory (owned by the non-root 'app' user in the aspnet image)
RUN mkdir -p /data && chown -R app:app /data

# --- build image ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "Becs.csproj"
RUN dotnet publish "Becs.csproj" -c Release -o /app/publish

# --- final image ---
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Copy default ML models into the image (can be overridden by a bind mount)
COPY MLModels /app/MLModels

# run as non-root
USER app

# environment
ENV ConnectionStrings__DefaultConnection="Data Source=/data/becs.db"
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

# Point the app to model paths (overridable at runtime)
ENV Models__Demand="/MLModels/demandModel.zip"
ENV Models__Eligibility="/MLModels/eligibilityModel.zip"

# (optional) EXPOSE for clarity in local testing
EXPOSE 8080

ENTRYPOINT ["dotnet", "Becs.dll"]
