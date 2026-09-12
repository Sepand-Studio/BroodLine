FROM node:22-slim AS deps
WORKDIR /app
RUN corepack enable
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY services/api/package.json services/api/
RUN pnpm install --frozen-lockfile --prod

FROM node:22-slim
WORKDIR /app
ENV NODE_ENV=production
COPY --from=deps /app/node_modules ./node_modules
COPY --from=deps /app/services/api/node_modules ./services/api/node_modules
COPY services/api ./services/api
COPY config ./config

# Node 22 strips TypeScript types natively, so there is no build step and no
# dist/ to go stale against the source. The tradeoff is that the container
# carries .ts files; at this size that is cheaper than a bundler in the loop.
EXPOSE 8080
CMD ["node", "--experimental-strip-types", "services/api/src/index.ts"]
