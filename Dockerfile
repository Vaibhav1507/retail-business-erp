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
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       tesseract-ocr \
       tesseract-ocr-eng \
    && tesseract --version \
    && tesseract --list-langs \
    && rm -rf /var/lib/apt/lists/*

COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Rbos.Api.dll"]
