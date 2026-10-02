# syntax=docker/dockerfile:1

FROM node:22-alpine AS build
WORKDIR /src

COPY frontend/devforge-web/package.json frontend/devforge-web/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY frontend/devforge-web/ ./
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/devforge-web/browser /usr/share/nginx/html
EXPOSE 80
