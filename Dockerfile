# Multi-stage Dockerfile for .NET 9 application with layered architecture
# Stage 1: Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution file (if exists) for better dependency resolution
COPY *.sln ./

# Copy project files in the correct order to maintain project references
# Copy API project (entry point)
COPY API/API.csproj API/
# Copy Application project (depends on Domain and Persistent)
COPY Application/Application.csproj Application/
# Copy Domain project (base layer)
COPY Domain/Domain.csproj Domain/
# Copy Persistent project (depends on Domain)
COPY Persistent/Persistent.csproj Persistent/

# Restore NuGet packages for all projects
# This layer is cached if project files don't change
RUN dotnet restore

# Copy remaining source code for all projects
COPY API/ API/
COPY Application/ Application/
COPY Domain/ Domain/
COPY Persistent/ Persistent/

# Build and publish the API project (entry point)
# This automatically builds all dependencies due to project references
WORKDIR /src/API
RUN dotnet publish "API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --verbosity minimal

# Stage 2: Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final

# Set working directory
WORKDIR /app

# Expose port 8080
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_ENVIRONMENT=Production

# Install curl for health checks
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        curl \
    && rm -rf /var/lib/apt/lists/*

# Create a non-root user for better security
RUN groupadd --gid 1000 appgroup && \
    useradd --uid 1000 --gid appgroup -m appuser

# Copy published application from build stage
COPY --from=build /app/publish .

# Change ownership to non-root user
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Set entry point to run the application
# API.dll is the compiled assembly from API.csproj
ENTRYPOINT ["dotnet", "API.dll"]