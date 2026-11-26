# syntax=docker/dockerfile:1
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY NuGet.Config ./
COPY src/ src/
COPY db/ db/
RUN dotnet publish src/Rbos.Api/Rbos.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Scanned-PDF OCR is performed by the Tesseract executable at runtime.
# Install it in the final image (not only in the SDK/build stage).
# The Ubuntu Noble image defaults to HTTP apt mirrors; Cloud Build egress
# timed out on port 80. Use HTTPS and retry the full update+install sequence:
# Ubuntu archive mirrors can briefly publish an index that does not match its
# Release metadata while a mirror sync is in progress. APT's transport retries
# alone do not recover from that hash/size mismatch.
RUN set -eu; \
    find /etc/apt -type f \( -name '*.sources' -o -name '*.list' \) \
      -exec sed -i \
        -e 's#http://archive.ubuntu.com/ubuntu#https://archive.ubuntu.com/ubuntu#g' \
        -e 's#http://security.ubuntu.com/ubuntu#https://security.ubuntu.com/ubuntu#g' {} +; \
    attempt=1; \
    while [ "$attempt" -le 5 ]; do \
      rm -rf /var/lib/apt/lists/*; \
      if apt-get -o Acquire::Retries=3 -o Acquire::https::Timeout=30 update \
        && apt-get install -y --no-install-recommends \
          tesseract-ocr \
          tesseract-ocr-eng \
          fonts-noto-core \
          libfontconfig1; then \
        break; \
      fi; \
      if [ "$attempt" -eq 5 ]; then \
        echo "Ubuntu package mirrors remained inconsistent after $attempt attempts." >&2; \
        exit 1; \
      fi; \
      echo "Ubuntu package mirror sync incomplete; retrying ($attempt/5)." >&2; \
      sleep "$((attempt * 10))"; \
      attempt=$((attempt + 1)); \
    done; \
    tesseract --version; \
    tesseract --list-langs | grep -qx 'eng'; \
    rm -rf /var/lib/apt/lists/*

COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Production Ocr__TesseractPath=/usr/bin/tesseract
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Rbos.Api.dll"]
