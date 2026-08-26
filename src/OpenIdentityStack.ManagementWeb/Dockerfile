ARG BUILDPLATFORM
ARG TARGETPLATFORM

FROM --platform=$BUILDPLATFORM node:22-alpine AS build

WORKDIR /workspace

COPY src/frontend-packages/admin-api-client ./src/frontend-packages/admin-api-client
COPY src/OpenIdentityStack.ManagementWeb/package.json ./src/OpenIdentityStack.ManagementWeb/package.json
COPY src/OpenIdentityStack.ManagementWeb/package-lock.json ./src/OpenIdentityStack.ManagementWeb/package-lock.json

WORKDIR /workspace/src/OpenIdentityStack.ManagementWeb
RUN npm ci --ignore-scripts

COPY src/OpenIdentityStack.ManagementWeb ./
RUN npm run build

FROM --platform=$TARGETPLATFORM nginx:1.29-alpine AS final

RUN apk add --no-cache libcap \
    && setcap 'cap_net_bind_service=+ep' /usr/sbin/nginx \
    && apk del libcap \
    && chown -R nginx:nginx /var/cache/nginx /run

COPY src/OpenIdentityStack.ManagementWeb/nginx.conf /etc/nginx/conf.d/default.conf
COPY --chmod=755 src/OpenIdentityStack.ManagementWeb/docker-entrypoint.sh /docker-entrypoint.d/40-openidentitystack-runtime-config.sh
COPY --chown=nginx:nginx --from=build /workspace/src/OpenIdentityStack.ManagementWeb/dist /usr/share/nginx/html

EXPOSE 80

USER nginx

CMD ["nginx", "-g", "daemon off;"]
