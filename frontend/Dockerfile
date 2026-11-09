# The Compose gateway: nginx serving the production React build and forwarding API requests.
# Build from the repository root: docker build -f frontend/Dockerfile .
FROM node:22.22-alpine AS build
WORKDIR /src
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npm run lint && npm run build

FROM nginx:1.28-alpine
COPY deploy/gateway.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 8080
