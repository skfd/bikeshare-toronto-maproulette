# Multi-stage build for optimal image size
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /source

# Copy project files
COPY src/*.csproj ./src/
COPY tests/*.csproj ./tests/

# Restore dependencies
RUN dotnet restore src/prepareBikeParking.csproj
RUN dotnet restore tests/prepareBikeParking.Tests.csproj

# Copy source code
COPY . .

# Run tests
RUN dotnet test tests/prepareBikeParking.Tests.csproj \
    --configuration Release \
    --no-restore \
    --verbosity normal

# Build and publish
RUN dotnet publish src/prepareBikeParking.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    --self-contained true \
    --runtime linux-musl-x64 \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine
WORKDIR /app

# Install required tools
RUN apk add --no-cache \
    git \
    ca-certificates \
    tzdata

# Create non-root user
RUN addgroup -g 1000 bikeshare && \
    adduser -u 1000 -G bikeshare -s /bin/sh -D bikeshare

# Copy published app
COPY --from=build /app /app

# Create data directory
RUN mkdir -p /data && \
    chown -R bikeshare:bikeshare /data /app

# Switch to non-root user
USER bikeshare

# Set environment variables
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

# Volume for data persistence
VOLUME ["/data"]
WORKDIR /data

# Entry point
ENTRYPOINT ["/app/prepareBikeParking"]
CMD ["--help"]