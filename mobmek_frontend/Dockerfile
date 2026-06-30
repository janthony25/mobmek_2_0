# --- Build stage ---
FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies first so they cache independently of source changes.
# Use `npm install` rather than `npm ci`: the lock file is generated on the host
# (often macOS/arm64) and can't record the Linux-only native binaries (Tailwind
# oxide, lightningcss) and their transitive optional deps, which `npm ci` requires
# to be present exactly. `npm install` respects the lock and fills in the rest.
COPY package*.json ./
RUN npm install --no-audit --no-fund

COPY . .
RUN npm run build

# --- Runtime stage ---
FROM nginx:1.27-alpine AS runtime

# SPA routing + /api reverse proxy to the backend service.
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
