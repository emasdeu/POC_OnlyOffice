# Multi-stage build for OnlyOffice Storage Server
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

WORKDIR /app

# Copy project file and restore
COPY OnlyOfficeStorageServer.csproj .
RUN dotnet restore

# Copy source code
COPY Program.cs .

# Build and publish
RUN dotnet publish -c Release -o /app/out

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app

# Create storage directory
RUN mkdir -p /var/lib/onlyoffice-storage && \
    chmod 755 /var/lib/onlyoffice-storage

# Copy published application from builder
COPY --from=builder /app/out .

# Expose port
EXPOSE 8000

# Set environment variables
ENV STORAGE_PATH=/var/lib/onlyoffice-storage
ENV LISTEN_PORT=8000

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD dotnet OnlyOfficeStorageServer.dll || exit 1

# Run the application
ENTRYPOINT ["dotnet", "OnlyOfficeStorageServer.dll"]
