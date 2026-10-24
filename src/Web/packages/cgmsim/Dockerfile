# Multi-stage Dockerfile for @nocturne/cgmsim
# Build from repository root:
# docker build -t nocturne-cgmsim -f src/Web/packages/cgmsim/Dockerfile .

FROM --platform=$BUILDPLATFORM node:24-alpine AS builder
WORKDIR /app

RUN npm install -g pnpm@11.5.0

# Copy monorepo manifests for lockfile caching
COPY src/Web/package.json src/Web/pnpm-lock.yaml src/Web/pnpm-workspace.yaml ./
COPY src/Web/patches ./patches
COPY src/Web/packages/cgmsim/package.json ./packages/cgmsim/

RUN pnpm --filter @nocturne/cgmsim install --frozen-lockfile

# Copy cgmsim source code and compile TypeScript
COPY src/Web/packages/cgmsim ./packages/cgmsim
RUN pnpm --filter @nocturne/cgmsim build

# --- Runtime stage ---
FROM node:24-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

RUN npm install -g pnpm@11.5.0

COPY --from=builder /app/package.json ./
COPY --from=builder /app/pnpm-lock.yaml ./
COPY --from=builder /app/pnpm-workspace.yaml ./
COPY --from=builder /app/patches ./patches
COPY --from=builder /app/packages/cgmsim/package.json ./packages/cgmsim/
COPY --from=builder /app/packages/cgmsim/dist ./packages/cgmsim/dist

# Install only production dependencies with world-traversable store
RUN pnpm --filter @nocturne/cgmsim install --prod --frozen-lockfile --store-dir /usr/local/share/pnpm

WORKDIR /app/packages/cgmsim

# Default environment variables
ENV SIMULATION_ENABLED=true
ENV SIMULATION_INTERVAL_MS=300000
ENV INSTANCE_SERVICE_NAME=cgmsim

USER node

CMD ["node", "dist/index.js"]
